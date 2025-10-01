using Kestrun.Hosting;
using Kestrun.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using Microsoft.Extensions.Options;

namespace KestrunTests.Middleware;

public class CommonAccessLogMiddlewareTests
{
    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }

    private static (ILogger Logger, CapturingSink Sink) CreateLogger(LogEventLevel level = LogEventLevel.Information)
    {
        var sink = new CapturingSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (logger, sink);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task CommonAccessLog_WritesEntry_WithDefaultOptions()
    {
        var (logger, sink) = CreateLogger();
        var services = new ServiceCollection();
        _ = services.AddOptions();
        _ = services.AddSingleton(logger);
        var sp = services.BuildServiceProvider();

        var app = new ApplicationBuilder(sp);
        _ = app.UseMiddleware<CommonAccessLogMiddleware>();
        app.Run(async ctx =>
        {
            ctx.Response.StatusCode = 204;
            await ctx.Response.CompleteAsync();
        });
        var pipeline = app.Build();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");
        httpContext.Request.Path = "/test";
        httpContext.Response.Body = new MemoryStream();

        await pipeline(httpContext);

        var evt = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Information, evt.Level);
        var rendered = evt.RenderMessage();
        Assert.Contains("GET /test", rendered);
        Assert.Contains(" 204 ", rendered); // status code emitted
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCommonAccessLog_RegistersOptionsAndMiddleware()
    {
        var (logger, _) = CreateLogger(LogEventLevel.Debug);
        var host = new KestrunHost("TestApp", logger, AppContext.BaseDirectory);

        _ = host.AddCommonAccessLog(o =>
        {
            o.IncludeProtocol = true;
            o.IncludeQueryString = true;
            o.UseUtcTimestamp = true;
        });

        var app = host.Build();
        var sp = app.Services;
        var monitor = sp.GetService<IOptionsMonitor<CommonAccessLogOptions>>();
        Assert.NotNull(monitor);
        var resolvedLogger = sp.GetService<ILogger>();
        Assert.NotNull(resolvedLogger);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task CommonAccessLog_RespectsLevelFiltering()
    {
        var (logger, sink) = CreateLogger(LogEventLevel.Warning); // higher than Information
        var services = new ServiceCollection();
        _ = services.AddOptions();
        _ = services.AddSingleton(logger);
        var sp = services.BuildServiceProvider();

        var app = new ApplicationBuilder(sp);
        _ = app.UseMiddleware<CommonAccessLogMiddleware>();
        app.Run(async ctx =>
        {
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync("OK");
        });
        var pipeline = app.Build();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/quiet";
        httpContext.Response.Body = new MemoryStream();

        await pipeline(httpContext);

        // No events because Information < Warning
        Assert.Empty(sink.Events);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task CommonAccessLog_AppendsElapsedMilliseconds_WhenEnabled()
    {
        var (logger, sink) = CreateLogger();
        var services = new ServiceCollection();
        _ = services.AddOptions();
        _ = services.Configure<CommonAccessLogOptions>(o => o.IncludeElapsedMilliseconds = true);
        _ = services.AddSingleton(logger);
        var sp = services.BuildServiceProvider();

        var app = new ApplicationBuilder(sp);
        _ = app.UseMiddleware<CommonAccessLogMiddleware>();
        app.Run(async ctx =>
        {
            ctx.Response.StatusCode = 200;
            await Task.Delay(5); // ensure some elapsed time
            await ctx.Response.WriteAsync("OK");
        });
        var pipeline = app.Build();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/elapsed";
        httpContext.Response.Body = new MemoryStream();

        await pipeline(httpContext);

        var evt = Assert.Single(sink.Events);
        // Access log line is stored as the single property value for the template {CommonAccessLogLine}
        var raw = evt.Properties.TryGetValue("CommonAccessLogLine", out var sv) ? sv.ToString() : evt.RenderMessage();
        // Remove surrounding quotes if Serilog scalar formatting added them
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
        {
            raw = raw[1..^1];
        }
        // Split and inspect last segment for milliseconds (allow integer or decimal)
        var lastSegment = raw.Split(' ').Last();
        Assert.Matches(@"^\d+(\.\d+)?$", lastSegment);
    }
}
