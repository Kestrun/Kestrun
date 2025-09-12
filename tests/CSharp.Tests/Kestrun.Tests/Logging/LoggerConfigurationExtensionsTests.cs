using Kestrun.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace KestrunTests.Logging;

[Collection("SharedStateSerial")] // interacts with global Log.Logger
public class LoggerConfigurationExtensionsTests
{
    private sealed class CaptureSink : ILogEventSink, IDisposable
    {
        public LogEvent? Last;
        public bool Disposed;
        public void Emit(LogEvent logEvent) => Last = logEvent;
        public void Dispose() => Disposed = true;
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Register_Creates_Registers_And_OptionallySetsDefault()
    {
        var previous = Log.Logger;
        try
        {
            LoggerManager.Clear();
            using var sink = new CaptureSink();

            var logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(sink)
                .Register("ext-reg", setAsDefault: true);

            Assert.Same(logger, LoggerManager.Get("ext-reg"));
            Assert.Same(logger, Log.Logger);

            logger.Information("hello");
            Assert.NotNull(sink.Last);
        }
        finally
        {
            Log.Logger = previous;
            LoggerManager.Clear();
        }
    }
}
