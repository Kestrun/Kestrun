using System.Net;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Xunit;

namespace KestrunTests.Middleware;

public class HostFilteringExtensionsTests
{
    // Build a minimal pipeline using HostFiltering middleware to validate behavior without a running server
    private static RequestDelegate BuildFilteredPipeline(Action<HostFilteringOptions> cfg)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddOptions();
        _ = services.AddHostFiltering(cfg);

        var sp = services.BuildServiceProvider();
        var app = new ApplicationBuilder(sp);
        _ = app.UseHostFiltering();
        app.Run(async context =>
        {
            // Terminal delegate invoked only if HostFiltering allows the request
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await context.Response.WriteAsync("ok");
        });
        return app.Build();
    }

    [Fact]
    public async Task Allows_Whitelisted_Hosts()
    {
        var pipeline = BuildFilteredPipeline(o =>
        {
            o.AllowedHosts.Clear();
            o.AllowedHosts.Add("example.com");
            o.AllowedHosts.Add("www.example.com");
            o.AllowEmptyHosts = false;
        });

        // example.com
        var ctx1 = new DefaultHttpContext();
        ctx1.Request.Method = HttpMethods.Get;
        ctx1.Request.Scheme = "http";
        ctx1.Request.Path = "/hello";
        ctx1.Request.Headers.Host = "example.com";
        await pipeline(ctx1);
        Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)ctx1.Response.StatusCode);

        // www.example.com
        var ctx2 = new DefaultHttpContext();
        ctx2.Request.Method = HttpMethods.Get;
        ctx2.Request.Scheme = "http";
        ctx2.Request.Path = "/hello";
        ctx2.Request.Headers.Host = "www.example.com";
        await pipeline(ctx2);
        Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)ctx2.Response.StatusCode);
    }

    [Fact]
    public async Task Blocks_Disallowed_Host()
    {
        var pipeline = BuildFilteredPipeline(o =>
        {
            o.AllowedHosts.Clear();
            o.AllowedHosts.Add("example.com");
            o.AllowEmptyHosts = false;
        });

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Get;
        ctx.Request.Scheme = "http";
        ctx.Request.Path = "/hello";
        ctx.Request.Headers.Host = "blocked.example";
        await pipeline(ctx);
        Assert.Equal(HttpStatusCode.BadRequest, (HttpStatusCode)ctx.Response.StatusCode);
    }

    // Note: Empty Host header scenarios are validated in PowerShell (socket-level) tests.

    [Fact]
    public void Options_Overload_Copies_Values()
    {
        var opts = new HostFilteringOptions
        {
            AllowEmptyHosts = true,
            IncludeFailureMessage = true
        };
        opts.AllowedHosts.Add("*.example.com");

        var logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();
        var host = new KestrunHost("TestHostFilteringOptions", logger);
        _ = host.AddHostFiltering(opts);
        var app = host.Build();

        var configured = app.Services.GetRequiredService<IOptions<HostFilteringOptions>>().Value;
        Assert.True(configured.AllowEmptyHosts);
        Assert.True(configured.IncludeFailureMessage);
        Assert.Contains("*.example.com", configured.AllowedHosts);
    }
}
