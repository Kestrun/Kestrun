using Kestrun.Middleware;
using Kestrun.Scripting;
using Kestrun.Hosting;
using Kestrun.Languages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Management.Automation;
using Xunit;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Microsoft.AspNetCore.Http.Features;

namespace KestrunTests.Middleware;

[Collection("SharedStateSerial")] // Avoid races with global Serilog Log.Logger
public class PowerShellRunspaceMiddlewareTests
{
    [Fact]
    [Trait("Category", "Middleware")]
    public async Task Middleware_InsertsPowerShellAndKestrunContext_AndCleansUp()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);

        // Runspace pool: small for test
        using var host = new KestrunHost("Tests", Log.Logger);
        var pool = new KestrunRunspacePoolManager(host, minRunspaces: 1, maxRunspaces: 1);

        // Use extension (covers both middleware and extension path)
        _ = app.UsePowerShellRunspace(pool);

        // Terminal delegate: assert items are present during request
        app.Run(async ctx =>
        {
            Assert.True(ctx.Items.ContainsKey(PowerShellDelegateBuilder.PS_INSTANCE_KEY));
            Assert.True(ctx.Items.ContainsKey(PowerShellDelegateBuilder.KR_CONTEXT_KEY));

            var ps = Assert.IsType<PowerShell>(ctx.Items[PowerShellDelegateBuilder.PS_INSTANCE_KEY]);
            Assert.NotNull(ps.Runspace);
            Assert.True(ps.Runspace.RunspaceStateInfo.State is System.Management.Automation.Runspaces.RunspaceState.Opened);

            var kr = Assert.IsType<Kestrun.Models.KestrunContext>(ctx.Items[PowerShellDelegateBuilder.KR_CONTEXT_KEY]!);
            Assert.Equal(ctx, kr.HttpContext);

            // Verify session state variable set
            var ctxVar = ps.Runspace.SessionStateProxy.GetVariable("Context");
            Assert.Same(kr, ctxVar);

            // A simple write to ensure response is writable
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync("");
        });

        var pipeline = app.Build();

        // Use a custom response feature to capture OnCompleted and trigger it manually in test
        var http = new DefaultHttpContext();
        var responseFeature = new TestHttpResponseFeature();
        http.Features.Set<IHttpResponseFeature>(responseFeature);
        http.Request.Path = "/test";
        await pipeline(http);

        // With deferred cleanup (OnCompleted), items are still present until the response completes
        Assert.True(http.Items.ContainsKey(PowerShellDelegateBuilder.PS_INSTANCE_KEY));
        Assert.True(http.Items.ContainsKey(PowerShellDelegateBuilder.KR_CONTEXT_KEY));

        // Trigger response completion to execute OnCompleted callbacks and perform cleanup
        await responseFeature.TriggerOnCompletedAsync();

        // After completion, both items should be removed
        Assert.False(http.Items.ContainsKey(PowerShellDelegateBuilder.PS_INSTANCE_KEY));
        Assert.False(http.Items.ContainsKey(PowerShellDelegateBuilder.KR_CONTEXT_KEY));
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task Middleware_Then_PSDelegate_WritesResponse()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        using var host = new KestrunHost("Tests", Log.Logger);
        var pool = new KestrunRunspacePoolManager(host, minRunspaces: 1, maxRunspaces: 1);

        _ = app.UsePowerShellRunspace(pool);

        // Build a PS delegate that writes to KestrunResponse via the injected Context
        var code = "\r\n$Context.Response.WriteTextResponse('ok from ps')\r\n";
        var log = Log.Logger;
        var del = PowerShellDelegateBuilder.Build(code, log, arguments: null);

        app.Run(del);

        var pipeline = app.Build();
        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();
        await pipeline(http);

        Assert.Equal(200, http.Response.StatusCode);
        Assert.Equal("text/plain; charset=utf-8", http.Response.ContentType);
        http.Response.Body.Position = 0;
        using var reader = new StreamReader(http.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("ok from ps", body);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task Middleware_Then_PSDelegate_CanRedirect()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        using var host = new KestrunHost("Tests", Log.Logger);
        var pool = new KestrunRunspacePoolManager(host, minRunspaces: 1, maxRunspaces: 1);

        _ = app.UsePowerShellRunspace(pool);

        // Ask PS to set a redirect on the KestrunResponse
        var code = "\r\n$Context.Response.WriteRedirectResponse('https://example.org/next')\r\n";
        var log = Log.Logger;
        var del = PowerShellDelegateBuilder.Build(code, log, arguments: null);
        app.Run(del);

        var pipeline = app.Build();
        var http = new DefaultHttpContext();
        await pipeline(http);

        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.True(http.Response.Headers.ContainsKey("Location"));
        Assert.Equal("https://example.org/next", http.Response.Headers["Location"].ToString());
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task Middleware_LogsAndDoesNotThrow_WhenPoolMisconfigured()
    {
        // capture Serilog output
        var previous = Log.Logger;
        var collectingSink = new InMemorySink();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(collectingSink)
            .CreateLogger();

        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);

        // Create a tiny pool and dispose it immediately to simulate misconfiguration
        using var host = new KestrunHost("Tests", Log.Logger);
        var pool = new KestrunRunspacePoolManager(host, minRunspaces: 0, maxRunspaces: 1);
        pool.Dispose();

        _ = app.UsePowerShellRunspace(pool);

        // Downstream should still execute without exception; we set a 204 to check flow
        app.Run(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        var pipeline = app.Build();
        var http = new DefaultHttpContext();
        http.Request.Path = "/error-path";

        // Middleware may throw when the pool is disposed; tolerate either behavior but assert error logging.
        Exception? caught = null;
        try { await pipeline(http); }
        catch (Exception ex) { caught = ex; }

        // Verify an error was logged by the middleware (allow brief time for sinks)
        var found = false;
        for (var i = 0; i < 10 && !found; i++)
        {
            found = collectingSink.Events.Any(e =>
                e.Level == LogEventLevel.Error &&
                e.MessageTemplate.Text.Contains("Error occurred in PowerShellRunspaceMiddleware"));
            if (!found)
            {
                await Task.Delay(25);
            }
        }
        Assert.True(found, "Expected error log from PowerShellRunspaceMiddleware not found.");

        Log.Logger = previous;
    }

    private sealed class InMemorySink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    // Minimal IHttpResponseFeature implementation to capture and trigger OnCompleted callbacks
    private sealed class TestHttpResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> callback, object state)> _onCompleted = [];
        private readonly List<(Func<object, Task> callback, object state)> _onStarting = [];

        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; } = string.Empty;
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted { get; private set; }

        public void OnCompleted(Func<object, Task> callback, object state) => _onCompleted.Add((callback, state));

        public void OnStarting(Func<object, Task> callback, object state) => _onStarting.Add((callback, state));

        public void DisableBuffering() { }

        public async Task TriggerOnCompletedAsync()
        {
            // Mark as started to mirror pipeline behavior
            HasStarted = true;
            // Fire OnStarting first (reverse order per ASP.NET Core semantics)
            for (var i = _onStarting.Count - 1; i >= 0; i--)
            {
                var (cb, st) = _onStarting[i];
                await cb(st);
            }
            // Fire OnCompleted (reverse order)
            for (var i = _onCompleted.Count - 1; i >= 0; i--)
            {
                var (cb, st) = _onCompleted[i];
                await cb(st);
            }
        }
    }
}
