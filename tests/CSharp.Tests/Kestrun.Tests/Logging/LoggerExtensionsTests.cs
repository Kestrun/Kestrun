using Kestrun.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace KestrunTests.Logging;

public class LoggerExtensionsTests
{
    private sealed class CaptureSink : ILogEventSink
    {
        public LogEvent? Last;
        public void Emit(LogEvent logEvent) => Last = logEvent;
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void DebugSanitized_Strips_Control_Characters()
    {
        var sink = new CaptureSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(sink)
            .CreateLogger();

        // contains CR, LF, and bell
        var dirty = "Line1\r\nLine2\a";
        logger.DebugSanitized("msg {val}", dirty);

        Assert.NotNull(sink.Last);
        var prop = sink.Last!.Properties["val"].ToString().Trim('"');
        Assert.Equal("Line1Line2", prop);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void DebugSanitized_WithException_Strips_Control_Characters()
    {
        var sink = new CaptureSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var dirty = "A\r\nB\tC";
        logger.DebugSanitized(new InvalidOperationException("boom"), "err {v}", dirty);

        Assert.NotNull(sink.Last);
        var prop = sink.Last!.Properties["v"].ToString().Trim('"');
        Assert.Equal("ABC", prop);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void DebugSanitized_NoLog_When_Debug_Disabled()
    {
        var sink = new CaptureSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.DebugSanitized("msg {x}", "v");
        Assert.Null(sink.Last);
    }
}
