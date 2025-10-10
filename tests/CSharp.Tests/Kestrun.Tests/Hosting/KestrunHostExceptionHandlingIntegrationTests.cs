using System.Net;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.Runtime;
using Kestrun.Scripting;
using Serilog;
using Xunit;
using Microsoft.AspNetCore.Builder;
using Kestrun.Utilities;
using Microsoft.AspNetCore.Http;

namespace KestrunTests.Hosting;

/// <summary>
/// Integration-style tests for Kestrun exception handling behaviors using a live in-process server.
/// </summary>
public class KestrunHostExceptionHandlingIntegrationTests
{
    private static KestrunHost CreateHost(Action<KestrunHost>? configureBeforeBuild, Action<KestrunHost>? configureAfterBuild)
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestExceptions", logger, AppContext.BaseDirectory);
        host.ConfigureListener(0, IPAddress.Loopback, useConnectionLogging: false);
        // Configure options/middleware prior to building the app
        configureBeforeBuild?.Invoke(host);
        host.EnableConfiguration();
        // Map routes after the WebApplication has been built
        configureAfterBuild?.Invoke(host);
        return host;
    }

    private static async Task<(KestrunHost host, HttpClient client, int port)> StartAsync(KestrunHost host, CancellationToken ct)
    {
        await host.StartAsync(ct);
        var appField = typeof(KestrunHost).GetField("_app", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var app = (WebApplication)appField.GetValue(host)!;
        var port = app.Urls.Select(u => new Uri(u).Port).First();
        var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}/") };
        return (host, client, port);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task UseJsonExceptionHandler_ProblemDetails_ContentAndStatus()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var host = CreateHost(
            // Configure exception handling BEFORE build
            h =>
            {
                h.ExceptionOptions = new(h);
                h.ExceptionOptions.UseJsonExceptionHandler(useProblemDetails: true, includeDetailsInDevelopment: false);
            },
            // Map routes AFTER build
            h =>
            {
                _ = h.AddMapRoute("/oops", HttpVerb.Get, "throw new Exception(\"boom\");", ScriptLanguage.CSharp);
                _ = h.AddMapRoute("/ok", HttpVerb.Get, "Context.Response.StatusCode = 200;", ScriptLanguage.CSharp);
            });

        try
        {
            var (_, client, _) = await StartAsync(host, cts.Token);

            var ok = await client.GetAsync("ok", cts.Token);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

            var resp = await client.GetAsync("oops", cts.Token);
            Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
            Assert.NotNull(resp.Content.Headers.ContentType);
            Assert.Contains("application/problem+json", resp.Content.Headers.ContentType!.ToString());
            var body = await resp.Content.ReadAsStringAsync(cts.Token);
            Assert.Contains("\"status\": 500", body.Replace(" ", string.Empty));
            Assert.Contains("instance", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("traceId", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await host.StopAsync(cts.Token);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task ExceptionHandlingPath_ReExecutes_ToErrorEndpoint()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var host = CreateHost(
            // Configure exception handler path BEFORE build
            h =>
            {
                h.ExceptionOptions = new(h)
                {
                    ExceptionHandlingPath = new PathString("/error")
                };
            },
            // Map routes AFTER build
            h =>
            {
                _ = h.AddMapRoute("/oops", HttpVerb.Get, "throw new Exception(\"fail\");", ScriptLanguage.CSharp);
                _ = h.AddMapRoute("/error", HttpVerb.Get, "Context.Response.WriteJsonResponse(new{ok=false}, 500);", ScriptLanguage.CSharp);
            });

        try
        {
            var (_, client, _) = await StartAsync(host, cts.Token);
            var resp = await client.GetAsync("oops", cts.Token);
            Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
            var ct = resp.Content.Headers.ContentType?.MediaType;
            Assert.Equal("application/json", ct);
            var body = await resp.Content.ReadAsStringAsync(cts.Token);
            Assert.Contains("\"ok\":false", body.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await host.StopAsync(cts.Token);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task ScriptedCSharpExceptionHandler_HandlesAndReturnsJson500()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var host = CreateHost(
            // Configure scripted C# exception handler BEFORE build
            h =>
            {
                h.ExceptionOptions = new(h)
                {
                    LanguageOptions = new LanguageOptions
                    {
                        Language = ScriptLanguage.CSharp,
                        Code = "Context.Response.WriteJsonResponse(new{handled=true}, 500);"
                    }
                };
            },
            // Map throwing route AFTER build
            h =>
            {
                _ = h.AddMapRoute("/oops", HttpVerb.Get, "throw new InvalidOperationException(\"bad\");", ScriptLanguage.CSharp);
            });

        try
        {
            var (_, client, _) = await StartAsync(host, cts.Token);
            var resp = await client.GetAsync("oops", cts.Token);
            Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync(cts.Token);
            Assert.Contains("handled", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await host.StopAsync(cts.Token);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task ScriptedVBNetExceptionHandler_HandlesAndReturnsJson500()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var host = CreateHost(
            // Configure scripted VB.NET exception handler BEFORE build
            h =>
            {
                h.ExceptionOptions = new(h)
                {
                    LanguageOptions = new LanguageOptions
                    {
                        Language = ScriptLanguage.VBNet,
                        Code = "Context.Response.WriteJsonResponse(New With {.handled = True}, 500)"
                    }
                };
            },
            // Map throwing route AFTER build
            h =>
            {
                _ = h.AddMapRoute("/oops", HttpVerb.Get, "throw new Exception(\"vbboom\");", ScriptLanguage.CSharp);
            });

        try
        {
            var (_, client, _) = await StartAsync(host, cts.Token);
            var resp = await client.GetAsync("oops", cts.Token);
            Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync(cts.Token);
            Assert.Contains("handled", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await host.StopAsync(cts.Token);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task DeveloperExceptionPage_WhenEnabled_RendersHtmlError()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        // Ensure dev environment for richer output
        EnvironmentHelper.SetOverrideName("Development");

        var host = CreateHost(
            // Enable developer exception page BEFORE build
            h => { h.ExceptionOptions = new(h, developerExceptionPage: true); },
            // Map throwing route AFTER build
            h => { _ = h.AddMapRoute("/oops", HttpVerb.Get, "throw new Exception(\"devpage\");", ScriptLanguage.CSharp); });

        try
        {
            var (_, client, _) = await StartAsync(host, cts.Token);
            var resp = await client.GetAsync("oops", cts.Token);
            Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
            var contentType = resp.Content.Headers.ContentType?.MediaType;
            Assert.Equal("text/html", contentType);
            var body = await resp.Content.ReadAsStringAsync(cts.Token);
            Assert.Contains("Exception", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Unhandled", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            EnvironmentHelper.ClearOverride();
            await host.StopAsync(cts.Token);
        }
    }
}
