using Kestrun.Hosting.Compression;
using Kestrun.Hosting.Options;
using Kestrun.TBuilder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using Kestrun.Hosting;
using Moq;

namespace KestrunTests.Hosting.Compression;

/// <summary>
/// Tests covering new compression feature surface (provider, service registration, endpoint metadata, and options flag).
/// </summary>
public class CompressionFeatureTests
{
    private static KestrunResponseCompressionProvider CreateProvider(out ServiceProvider sp)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddOptions();
        _ = services.Configure<ResponseCompressionOptions>(opts =>
        {
            // Register at least gzip so the inner provider has something to negotiate.
            opts.Providers.Add(new GzipCompressionProvider(new GzipCompressionProviderOptions()));
            opts.EnableForHttps = true; // does not impact tests directly but exercises pass-through
        });
        sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<ResponseCompressionOptions>>();
        return new KestrunResponseCompressionProvider(sp, options);
    }

    [Fact]
    [Trait("Category", "Compression")]
    public void ServiceExtension_ReplacesProvider()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        // Manually provide required options dependency instead of full AddResponseCompression (we only
        // need DI to resolve the decorator and its inner provider can function with empty options).
        _ = services.AddKestrunCompressionOptOut();
        // Assert that the service collection now contains the singleton registration for our provider
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IResponseCompressionProvider));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(KestrunResponseCompressionProvider), descriptor.ImplementationType);
    }

    [Fact]
    [Trait("Category", "Compression")]
    public void Provider_AllowsCompression_WhenNoOptOutMetadataPresent()
    {
        var provider = CreateProvider(out var sp);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Accept-Encoding"] = "gzip"; // negotiate gzip
        // No endpoint set -> no opt-out metadata
        Assert.True(provider.CheckRequestAcceptsCompression(ctx));
        var cp = provider.GetCompressionProvider(ctx);
        Assert.NotNull(cp); // underlying provider should be returned
        // ShouldCompressResponse may be false if size heuristics apply, but call should not throw.
        _ = provider.ShouldCompressResponse(ctx); // No assertion; just ensure delegation path executed.
        sp.Dispose();
    }

    [Fact]
    [Trait("Category", "Compression")]
    public void Provider_DisabledByEndpointMetadata_SuppressesCompression()
    {
        var provider = CreateProvider(out var sp);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Accept-Encoding"] = "gzip";
        // Build endpoint with opt-out metadata
        var endpoint = new RouteEndpoint(
            context => Task.CompletedTask,
            RoutePatternFactory.Parse("/test"),
            order: 0,
            new EndpointMetadataCollection(EndpointDisablingCompressionExtensions.DisableResponseCompressionKey),
            displayName: "test-endpoint");
        ctx.SetEndpoint(endpoint);

        Assert.False(provider.CheckRequestAcceptsCompression(ctx));
        Assert.Null(provider.GetCompressionProvider(ctx));
        Assert.False(provider.ShouldCompressResponse(ctx));
        sp.Dispose();
    }

    private sealed class TestEndpointConventionBuilder : IEndpointConventionBuilder
    {
        public List<Action<EndpointBuilder>> Conventions { get; } = [];
        public void Add(Action<EndpointBuilder> convention) => Conventions.Add(convention);
    }

    [Fact]
    [Trait("Category", "Compression")]
    public void EndpointExtension_AddsDisableMetadata()
    {
        var builder = new TestEndpointConventionBuilder();
        _ = builder.DisableResponseCompression();
        // Simulate endpoint build
        var eb = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/x"), 0);
        foreach (var conv in builder.Conventions)
        {
            conv(eb);
        }
        Assert.Contains(EndpointDisablingCompressionExtensions.DisableResponseCompressionKey, eb.Metadata);
    }

    [Fact]
    [Trait("Category", "Compression")]
    public void MapRouteOptions_DisableResponseCompression_DefaultsFalse_AndSettable()
    {
        var opts = new MapRouteOptions();
        Assert.False(opts.DisableResponseCompression); // default
        opts.DisableResponseCompression = true;
        Assert.True(opts.DisableResponseCompression);
    }

    [Fact]
    [Trait("Category", "Compression")]
    public void ApplyKestrunConventions_WithDisableResponseCompression_SetsMetadata()
    {
        var hostLogger = new Mock<Serilog.ILogger>();
        _ = hostLogger.Setup(l => l.IsEnabled(It.IsAny<Serilog.Events.LogEventLevel>())).Returns(false);
        var host = new KestrunHost("ConvHost", hostLogger.Object);

        var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/conv"), 0);
        var ecb = new TestEndpointConventionBuilder();
        _ = host.ApplyKestrunConventions(ecb, o => o.DisableResponseCompression = true);
        // materialize conventions
        foreach (var c in ecb.Conventions)
        {
            c(builder);
        }
        Assert.Contains(EndpointDisablingCompressionExtensions.DisableResponseCompressionKey, builder.Metadata);
    }
}
