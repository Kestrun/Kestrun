using System.Net;
using System.Net.Http.Json;
using System.Text;
using Kestrun.Forms;
using Kestrun.Hosting;
using Kestrun.Models;
using Microsoft.AspNetCore.Builder;
using Serilog;
using Xunit;

namespace KestrunTests.Forms;

public class KrFormEndpointsIntegrationTests
{
    private static KestrunHost CreateHost(Action<WebApplication> mapRoutes)
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestFormEndpoints", logger, AppContext.BaseDirectory);
        host.ConfigureListener(0, IPAddress.Loopback, useConnectionLogging: false);
        host.EnableConfiguration();

        var appField = typeof(KestrunHost).GetField("_app", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var app = (WebApplication)appField.GetValue(host)!;
        mapRoutes(app);

        return host;
    }

    private static async Task<(KestrunHost host, HttpClient client)> StartAsync(KestrunHost host, CancellationToken ct)
    {
        await host.StartAsync(ct);
        var appField = typeof(KestrunHost).GetField("_app", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var app = (WebApplication)appField.GetValue(host)!;
        var port = app.Urls.Select(u => new Uri(u).Port).First();
        var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}/") };
        return (host, client);
    }

    [Fact]
    [Trait("Category", "Forms")]
    public async Task MapKestrunFormRoute_OnCompleted_ReturnsOk_AndSkipsHandler()
    {
        var handlerCalled = false;
        var options = new KrFormOptions
        {
            OnCompleted = ctx =>
            {
                var http = ctx.KestrunContext.HttpContext;
                Assert.True(http.Items.TryGetValue("KrFormContext", out var storedCtx));
                Assert.Same(ctx, storedCtx);
                Assert.True(http.Items.TryGetValue("KrFormPayload", out var storedPayload));
                Assert.Same(ctx.Payload, storedPayload);

                var payload = Assert.IsType<KrFormData>(ctx.Payload);
                Assert.True(payload.Fields.TryGetValue("a", out var values));
                Assert.Equal("1", values.Single());

                return ValueTask.FromResult<object?>(new { ok = true });
            }
        };
        options.AllowedRequestContentTypes.Add("application/x-www-form-urlencoded");

        var host = CreateHost(app =>
        {
            _ = app.MapKestrunFormRoute("/form", options, _ =>
            {
                handlerCalled = true;
                return Task.CompletedTask;
            });
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var started = await StartAsync(host, cts.Token);

        var resp = await started.client.PostAsync("form", new FormUrlEncodedContent(new Dictionary<string, string> { ["a"] = "1" }), cts.Token);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.False(handlerCalled);

        var json = await resp.Content.ReadFromJsonAsync<Dictionary<string, bool>>(cancellationToken: cts.Token);
        Assert.NotNull(json);
        Assert.True(json.TryGetValue("ok", out var ok) && ok);

        await host.StopAsync(cts.Token);
    }

    [Fact]
    [Trait("Category", "Forms")]
    public async Task MapKestrunFormRoute_Handler_WritesKestrunResponse_AndIsApplied()
    {
        var handlerCalled = false;
        var options = new KrFormOptions();
        options.AllowedRequestContentTypes.Add("application/x-www-form-urlencoded");

        var host = CreateHost(app =>
        {
            _ = app.MapKestrunFormRoute("/form2", options, ctx =>
            {
                handlerCalled = true;

                var http = ctx.KestrunContext.HttpContext;
                Assert.True(http.Items.TryGetValue("KrFormContext", out var storedCtx));
                Assert.Same(ctx, storedCtx);
                Assert.True(http.Items.TryGetValue("KrFormPayload", out var storedPayload));
                Assert.Same(ctx.Payload, storedPayload);

                var payload = Assert.IsType<KrFormData>(ctx.Payload);
                var b = payload.Fields["b"].Single();
                ctx.KestrunContext.Response.WriteJsonResponse(new { from = "handler", b });
                return Task.CompletedTask;
            });
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var started = await StartAsync(host, cts.Token);

        var resp = await started.client.PostAsync("form2", new FormUrlEncodedContent(new Dictionary<string, string> { ["b"] = "2" }), cts.Token);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.True(handlerCalled);

        var json = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: cts.Token);
        Assert.NotNull(json);
        Assert.Equal("handler", json["from"]);
        Assert.Equal("2", json["b"]);

        await host.StopAsync(cts.Token);
    }

    [Fact]
    [Trait("Category", "Forms")]
    public async Task MapKestrunFormRoute_UnsupportedContentType_Returns415_AndMessage()
    {
        var handlerCalled = false;
        var options = new KrFormOptions();

        var host = CreateHost(app =>
        {
            _ = app.MapKestrunFormRoute("/form3", options, _ =>
            {
                handlerCalled = true;
                return Task.CompletedTask;
            });
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var started = await StartAsync(host, cts.Token);

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var resp = await started.client.PostAsync("form3", content, cts.Token);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, resp.StatusCode);
        Assert.False(handlerCalled);
        Assert.Equal("Invalid form data.", await resp.Content.ReadAsStringAsync(cts.Token));

        await host.StopAsync(cts.Token);
    }

    [Fact]
    [Trait("Category", "Forms")]
    public async Task MapKestrunFormRoute_HandlerThrows_Returns500_AndMessage()
    {
        var options = new KrFormOptions();
        options.AllowedRequestContentTypes.Add("application/x-www-form-urlencoded");
        var host = CreateHost(app =>
        {
            _ = app.MapKestrunFormRoute("/form4", options, _ => throw new InvalidOperationException("boom"));
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var started = await StartAsync(host, cts.Token);

        var resp = await started.client.PostAsync("form4", new FormUrlEncodedContent(new Dictionary<string, string> { ["a"] = "1" }), cts.Token);

        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
        Assert.Equal("Internal server error.", await resp.Content.ReadAsStringAsync(cts.Token));

        await host.StopAsync(cts.Token);
    }
}
