using System.Net;
using System.Text.Json;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Routing;
using Serilog;
using Xunit;

namespace KestrunTests.Hosting;

public class KestrunHostForwardedHeadersIntegrationTests
{
    private static KestrunHost CreateHostWithForwardedHeaders()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestFwd", logger, AppContext.BaseDirectory);

        // Bind to dynamic port on loopback
        host.ConfigureListener(0, IPAddress.Loopback, useConnectionLogging: false);

        // Configure ForwardedHeaders to trust loopback (our HttpClient connection)
        var fwd = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.All,
            ForwardLimit = 2
        };
        fwd.KnownProxies.Add(IPAddress.Loopback);
        fwd.KnownProxies.Add(IPAddress.IPv6Loopback);
        host.ForwardedHeaderOptions = fwd;

        // Add a tiny endpoint after built-in middleware to observe effective values
        _ = host.Use(app =>
        {
            var endpoints = (IEndpointRouteBuilder)app;
            _ = endpoints.MapGet("/fwd", async ctx =>
            {
                var payload = new
                {
                    scheme = ctx.Request.Scheme,
                    host = ctx.Request.Host.Value,
                    remoteIp = ctx.Connection.RemoteIpAddress?.ToString(),
                    pathBase = ctx.Request.PathBase.Value ?? string.Empty
                };
                ctx.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(ctx.Response.Body, payload);
            });
        });

        host.EnableConfiguration();
        return host;
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task ForwardedHeaders_Applied_UpdatesSchemeHostAndRemoteIp()
    {
        var host = CreateHostWithForwardedHeaders();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(cts.Token);

        try
        {
            var appField = typeof(KestrunHost).GetField("_app", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var app = (WebApplication)appField.GetValue(host)!;
            var port = app.Urls.Select(u => new Uri(u).Port).First();

            using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}/") };

            // Send X-Forwarded-* headers simulating a proxy
            var req = new HttpRequestMessage(HttpMethod.Get, "fwd");
            _ = req.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
            _ = req.Headers.TryAddWithoutValidation("X-Forwarded-Host", "example.com");
            _ = req.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.10");

            using var resp = await client.SendAsync(req, cts.Token);
            _ = resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("https", root.GetProperty("scheme").GetString());
            Assert.Equal("example.com", root.GetProperty("host").GetString());
            Assert.Equal("203.0.113.10", root.GetProperty("remoteIp").GetString());
        }
        finally
        {
            await host.StopAsync(cts.Token);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task ForwardedHeaders_XForwardedPrefix_UpdatesPathBase()
    {
        var host = CreateHostWithForwardedHeaders();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StartAsync(cts.Token);

        try
        {
            var appField = typeof(KestrunHost).GetField("_app", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var app = (WebApplication)appField.GetValue(host)!;
            var port = app.Urls.Select(u => new Uri(u).Port).First();

            using var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}/") };

            var req = new HttpRequestMessage(HttpMethod.Get, "fwd");
            _ = req.Headers.TryAddWithoutValidation("X-Forwarded-Prefix", "/app");

            using var resp = await client.SendAsync(req, cts.Token);
            _ = resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.Equal("/app", root.GetProperty("pathBase").GetString());
        }
        finally
        {
            await host.StopAsync(cts.Token);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ForwardedHeaderOptions_ModifyAfterConfigured_Throws()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestFwdGuard", logger, AppContext.BaseDirectory);
        host.ConfigureListener(0, IPAddress.Loopback, useConnectionLogging: false);

        // Set before configuration OK
        host.ForwardedHeaderOptions = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.All };

        // Apply configuration
        host.EnableConfiguration();

        // Attempt to modify after configuration must throw
        _ = Assert.Throws<InvalidOperationException>(() => host.ForwardedHeaderOptions = new ForwardedHeadersOptions());
    }
}
