using System.Reflection;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

public sealed class OpenApiDocDescriptorAnnotatedFunctionsExtensionsTests
{
    private static void InvokeApplyExtensionAttribute(OpenApiDocDescriptor descriptor, OpenAPIPathMetadata metadata, OpenApiExtensionAttribute attribute)
    {
        var method = typeof(OpenApiDocDescriptor).GetMethod(
            "ApplyExtensionAttribute",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        _ = method.Invoke(descriptor, [metadata, attribute]);
    }

    [Fact]
    public void ApplyExtensionAttribute_AddsExtension_WhenJsonObject()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var metadata = new OpenAPIPathMetadata(mapOptions: new MapRouteOptions());
        var attr = new OpenApiExtensionAttribute("x-badges", /*lang=json,strict*/ "{\"name\":\"Beta\",\"position\":\"before\",\"color\":\"purple\"}");

        InvokeApplyExtensionAttribute(descriptor, metadata, attr);

        Assert.NotNull(metadata.Extensions);
        Assert.True(metadata.Extensions.TryGetValue("x-badges", out var extension));
        Assert.NotNull(extension);
        Assert.Equal("JsonNodeExtension", extension.GetType().Name);
    }

    [Fact]
    public void ApplyExtensionAttribute_AddsExtension_WhenValueIsPlainString()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var metadata = new OpenAPIPathMetadata(mapOptions: new MapRouteOptions());
        var attr = new OpenApiExtensionAttribute("x-releaseStage", "beta");

        InvokeApplyExtensionAttribute(descriptor, metadata, attr);

        Assert.NotNull(metadata.Extensions);
        Assert.True(metadata.Extensions.TryGetValue("x-releaseStage", out var extension));
        Assert.NotNull(extension);
        Assert.Equal("JsonNodeExtension", extension.GetType().Name);
    }

    [Fact]
    public void ApplyExtensionAttribute_SkipsExtension_WhenValueIsNull()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var descriptor = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var metadata = new OpenAPIPathMetadata(mapOptions: new MapRouteOptions());
        var attr = new OpenApiExtensionAttribute("x-nullValue", null!);

        InvokeApplyExtensionAttribute(descriptor, metadata, attr);

        Assert.NotNull(metadata.Extensions);
        Assert.Empty(metadata.Extensions);
    }
}
