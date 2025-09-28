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
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _local;
        private readonly DateTimeOffset _utc;

        public FixedTimeProvider(DateTimeOffset local)
        {
            _local = local;
            _utc = local.ToUniversalTime();
        }

        public override DateTimeOffset GetLocalNow() => _local;

        public override DateTimeOffset GetUtcNow() => _utc;
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
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "alice")
        }, authenticationType: "Test"));

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
}
