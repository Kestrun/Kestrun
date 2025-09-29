using System.Net;
using System.Security.Claims;
using Kestrun.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Serilog.Events;
using Xunit;

namespace KestrunTests.Middleware;

public class CommonAccessLogMiddlewareTests
{
    private sealed class FixedTimeProvider(DateTimeOffset local) : TimeProvider
    {
        private readonly DateTimeOffset _local = local;
        private readonly DateTimeOffset _utc = local.ToUniversalTime();

        public new DateTimeOffset GetLocalNow() => _local;

        public new DateTimeOffset GetUtcNow() => _utc;
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_Writes_Combined_Log_Line()
    {
        var options = new CommonAccessLogOptions
        {
            IncludeElapsedMilliseconds = false,
            IncludeProtocol = true,
            IncludeQueryString = true,
            TimestampFormat = CommonAccessLogOptions.DefaultTimestampFormat,
            TimeProvider = new FixedTimeProvider(new DateTimeOffset(2024, 1, 2, 13, 45, 0, TimeSpan.FromHours(-5)))
        };

        var optionsMonitor = Mock.Of<IOptionsMonitor<CommonAccessLogOptions>>(m => m.CurrentValue == options);

        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.ForContext(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>()))
                   .Returns(logger.Object);
        _ = logger.Setup(l => l.ForContext<CommonAccessLogMiddleware>())
                   .Returns(logger.Object);
        _ = logger.Setup(l => l.IsEnabled(LogEventLevel.Information)).Returns(true);

        string? captured = null;
        _ = logger.Setup(l => l.Write(LogEventLevel.Information, "{CommonAccessLogLine}", It.IsAny<object?[]>()))
                   .Callback<LogEventLevel, string, object?[]>((_, _, values) =>
                   {
                       captured = values.Length > 0 ? values[0]?.ToString() : null;
                   });

        var middleware = new CommonAccessLogMiddleware(_ => Task.CompletedTask, optionsMonitor, logger.Object);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1");
        context.Request.Method = "GET";
        context.Request.Path = "/items";
        context.Request.QueryString = new QueryString("?page=1");
        context.Request.Protocol = "HTTP/1.1";
        context.Request.Headers["User-Agent"] = "UnitTestAgent";
        context.Request.Headers["Referer"] = "https://example.org/start";
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentLength = 512;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "alice")
        ], authenticationType: "Test"));

        await middleware.InvokeAsync(context);

        Assert.Equal("192.0.2.1 - alice [02/Jan/2024:13:45:00 -0500] \"GET /items?page=1 HTTP/1.1\" 200 512 \"https://example.org/start\" \"UnitTestAgent\"", captured);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_Uses_Forwarded_For_And_Sanitises()
    {
        var options = new CommonAccessLogOptions
        {
            ClientAddressHeader = "X-Forwarded-For",
            IncludeProtocol = false,
            IncludeQueryString = false,
            TimestampFormat = CommonAccessLogOptions.DefaultTimestampFormat,
            TimeProvider = new FixedTimeProvider(new DateTimeOffset(2024, 1, 2, 18, 30, 0, TimeSpan.FromHours(1)))
        };

        var optionsMonitor = Mock.Of<IOptionsMonitor<CommonAccessLogOptions>>(m => m.CurrentValue == options);

        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.ForContext(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>()))
                   .Returns(logger.Object);
        _ = logger.Setup(l => l.ForContext<CommonAccessLogMiddleware>())
                   .Returns(logger.Object);
        _ = logger.Setup(l => l.IsEnabled(LogEventLevel.Information)).Returns(true);

        string? captured = null;
        _ = logger.Setup(l => l.Write(LogEventLevel.Information, "{CommonAccessLogLine}", It.IsAny<object?[]>()))
                   .Callback<LogEventLevel, string, object?[]>((_, _, values) =>
                   {
                       captured = values.Length > 0 ? values[0]?.ToString() : null;
                   });

        var middleware = new CommonAccessLogMiddleware(_ => Task.CompletedTask, optionsMonitor, logger.Object);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.9");
        context.Request.Method = "POST";
        context.Request.Path = "/submit";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.5, 198.51.100.7";
        context.Request.Headers["Referer"] = "https://example.org/start\"<script>";
        context.Request.Headers["User-Agent"] = "BadAgent\r\nTest";
        context.Response.StatusCode = StatusCodes.Status201Created;
        context.Response.Headers.ContentLength = 1024;

        await middleware.InvokeAsync(context);

        Assert.Equal("203.0.113.5 - - [02/Jan/2024:18:30:00 +0100] \"POST /submit\" 201 1024 \"https://example.org/start\\\"<script>\" \"BadAgentTest\"", captured);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_Invalid_Timestamp_Format_Falls_Back_To_Default()
    {
        var options = new CommonAccessLogOptions
        {
            IncludeProtocol = false,
            IncludeQueryString = false,
            TimestampFormat = "invalid {{{", // invalid format triggers fallback
            TimeProvider = new FixedTimeProvider(new DateTimeOffset(2024, 6, 10, 9, 30, 0, TimeSpan.FromHours(2)))
        };

        var optionsMonitor = Mock.Of<IOptionsMonitor<CommonAccessLogOptions>>(m => m.CurrentValue == options);

        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.ForContext(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>()))
                   .Returns(logger.Object);
        _ = logger.Setup(l => l.ForContext<CommonAccessLogMiddleware>())
                   .Returns(logger.Object);
        _ = logger.Setup(l => l.IsEnabled(LogEventLevel.Information)).Returns(true);

        string? captured = null;
        _ = logger.Setup(l => l.Write(LogEventLevel.Information, "{CommonAccessLogLine}", It.IsAny<object?[]>()))
                   .Callback<LogEventLevel, string, object?[]>((_, _, values) =>
                   {
                       captured = values.Length > 0 ? values[0]?.ToString() : null;
                   });

        var middleware = new CommonAccessLogMiddleware(_ => Task.CompletedTask, optionsMonitor, logger.Object);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.11");
        context.Request.Method = "GET";
        context.Request.Path = "/health";
        context.Response.StatusCode = StatusCodes.Status200OK;

        await middleware.InvokeAsync(context);

        Assert.Equal("198.51.100.11 - - [10/Jun/2024:09:30:00 +0200] \"GET /health\" 200 - \"-\" \"-\"", captured);
    }

    [Fact]
    [Trait("Category", "Middleware")]
    public async Task InvokeAsync_Uses_Custom_Logger_From_Options()
    {
        var customLogger = new Mock<Serilog.ILogger>();
        _ = customLogger.Setup(l => l.ForContext(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>()))
                        .Returns(customLogger.Object);
        _ = customLogger.Setup(l => l.ForContext<CommonAccessLogMiddleware>())
                        .Returns(customLogger.Object);
        _ = customLogger.Setup(l => l.IsEnabled(LogEventLevel.Warning)).Returns(true);

        var defaultLogger = new Mock<Serilog.ILogger>();
        _ = defaultLogger.Setup(l => l.ForContext(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>()))
                         .Returns(defaultLogger.Object);
        _ = defaultLogger.Setup(l => l.ForContext<CommonAccessLogMiddleware>())
                         .Returns(defaultLogger.Object);
        _ = defaultLogger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(false);

        var options = new CommonAccessLogOptions
        {
            Level = LogEventLevel.Warning,
            Logger = customLogger.Object,
            IncludeProtocol = false,
            IncludeQueryString = false,
            TimeProvider = new FixedTimeProvider(new DateTimeOffset(2024, 4, 5, 10, 0, 0, TimeSpan.Zero))
        };

        var optionsMonitor = Mock.Of<IOptionsMonitor<CommonAccessLogOptions>>(m => m.CurrentValue == options);

        string? captured = null;
        _ = customLogger.Setup(l => l.Write(LogEventLevel.Warning, "{CommonAccessLogLine}", It.IsAny<object?[]>()))
                        .Callback<LogEventLevel, string, object?[]>((_, _, values) =>
                        {
                            captured = values.Length > 0 ? values[0]?.ToString() : null;
                        });

        var middleware = new CommonAccessLogMiddleware(_ => Task.CompletedTask, optionsMonitor, defaultLogger.Object);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        context.Request.Method = "HEAD";
        context.Request.Path = "/status";
        context.Response.StatusCode = StatusCodes.Status204NoContent;

        await middleware.InvokeAsync(context);

        Assert.Equal("203.0.113.10 - - [05/Apr/2024:10:00:00 +0000] \"HEAD /status\" 204 - \"-\" \"-\"", captured);
        defaultLogger.Verify(l => l.Write(It.IsAny<LogEventLevel>(), It.IsAny<string>(), It.IsAny<object?[]>()), Times.Never);
    }
}
