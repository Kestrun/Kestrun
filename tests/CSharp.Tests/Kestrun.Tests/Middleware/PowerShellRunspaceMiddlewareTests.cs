using Kestrun.Middleware;
using Kestrun.Scripting;
using Kestrun.Languages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Management.Automation;
using Xunit;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace KestrunTests.Middleware;

public class PowerShellRunspaceMiddlewareTests
{
    [Fact]
    public async Task Middleware_InsertsPowerShellAndKestrunContext_AndCleansUp()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);

        // Runspace pool: small for test
        var pool = new KestrunRunspacePoolManager(minRunspaces: 1, maxRunspaces: 1);

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

            var kr = Assert.IsType<Kestrun.Hosting.KestrunContext>(ctx.Items[PowerShellDelegateBuilder.KR_CONTEXT_KEY]!);
            Assert.Equal(ctx, kr.HttpContext);

            // Verify session state variable set
            var ctxVar = ps.Runspace.SessionStateProxy.GetVariable("Context");
            Assert.Same(kr, ctxVar);

            // A simple write to ensure response is writable
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync("");
        });

        var pipeline = app.Build();
        var http = new DefaultHttpContext();
        http.Request.Path = "/test";
        await pipeline(http);

        // After pipeline, PS instance should be removed (disposed and returned to pool)
        Assert.False(http.Items.ContainsKey(PowerShellDelegateBuilder.PS_INSTANCE_KEY));
        Assert.True(http.Items.ContainsKey(PowerShellDelegateBuilder.KR_CONTEXT_KEY));
    }

    [Fact]
    public async Task Middleware_Then_PSDelegate_WritesResponse()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var pool = new KestrunRunspacePoolManager(minRunspaces: 1, maxRunspaces: 1);

        _ = app.UsePowerShellRunspace(pool);

        // Build a PS delegate that writes to KestrunResponse via the injected Context
        var code = "\r\n$Context.Response.WriteTextResponse('ok from ps')\r\n";
        var log = Serilog.Log.Logger;
        var del = Kestrun.Languages.PowerShellDelegateBuilder.Build(code, log, arguments: null);

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
    public async Task Middleware_Then_PSDelegate_CanRedirect()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        var pool = new KestrunRunspacePoolManager(minRunspaces: 1, maxRunspaces: 1);

        _ = app.UsePowerShellRunspace(pool);

        // Ask PS to set a redirect on the KestrunResponse
        var code = "\r\n$Context.Response.WriteRedirectResponse('https://example.org/next')\r\n";
        var log = Serilog.Log.Logger;
        var del = Kestrun.Languages.PowerShellDelegateBuilder.Build(code, log, arguments: null);
        app.Run(del);

        var pipeline = app.Build();
        var http = new DefaultHttpContext();
        await pipeline(http);

        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.True(http.Response.Headers.ContainsKey("Location"));
        Assert.Equal("https://example.org/next", http.Response.Headers["Location"].ToString());
    }

    [Fact]
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
        var pool = new KestrunRunspacePoolManager(minRunspaces: 0, maxRunspaces: 1);
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

        // Should not throw despite disposed pool; middleware catches and logs
        try
        {
            await pipeline(http);

            // Either upstream or middleware sets status; at minimum, the request completed
            Assert.True(http.Response.HasStarted || http.Response.StatusCode >= 200);

            // Verify an error was logged by the middleware
            Assert.Contains(collectingSink.Events, e =>
                e.Level == LogEventLevel.Error &&
                e.MessageTemplate.Text.Contains("Error occurred in PowerShellRunspaceMiddleware"));
        }
        finally
        {
            Log.Logger = previous;
        }
    }

    private sealed class InMemorySink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }
}
