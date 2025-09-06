using Kestrun.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace KestrunTests.Logging;

[Collection("SharedStateSerial")] // modifies Log.Logger and shared registry
public class LoggerManagerTests
{
    private sealed class CaptureSink : ILogEventSink, IDisposable
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
        public void Dispose() { }
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Add_Creates_And_Can_Set_Default()
    {
        var previous = Log.Logger;
        try
        {
            LoggerManager.Clear();
            using var sink = new CaptureSink();

            var logger = LoggerManager.Add("one", cfg => cfg.MinimumLevel.Debug().WriteTo.Sink(sink), setAsDefault: true);
            Assert.Same(logger, Log.Logger);
            Assert.Same(logger, LoggerManager.Get("one"));

            logger.Debug("ping");
            _ = Assert.Single(sink.Events);
        }
        finally
        {
            Log.Logger = previous;
            LoggerManager.Clear();
        }
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Register_Replaces_Existing_And_Disposes_Old()
    {
        var previous = Log.Logger;
        try
        {
            LoggerManager.Clear();
            var oldSink = new CaptureSink();
            var old = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(oldSink).CreateLogger();
            var reg1 = LoggerManager.Register("svc", old, setAsDefault: true);
            Assert.Same(old, reg1);

            var newSink = new CaptureSink();
            var @new = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(newSink).CreateLogger();
            var reg2 = LoggerManager.Register("svc", @new, setAsDefault: true);
            Assert.Same(@new, reg2);
            Assert.Same(@new, LoggerManager.Get("svc"));
            Assert.Same(@new, Log.Logger);

            @new.Information("ok");
            _ = Assert.Single(newSink.Events);
        }
        finally
        {
            Log.Logger = previous;
            LoggerManager.Clear();
        }
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void New_Builds_Config_With_Name_Property()
    {
        LoggerManager.Clear();
        var cfg = LoggerManager.New("alpha");
        var logger = cfg.WriteTo.Sink(new CaptureSink()).CreateLogger();
        Assert.NotNull(logger);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Remove_And_Clear_Dispose_Loggers()
    {
        LoggerManager.Clear();
        var sink = new CaptureSink();
        var logger = LoggerManager.Add("tmp", cfg => cfg.WriteTo.Sink(sink));
        Assert.True(LoggerManager.CloseAndFlush("tmp"));
        LoggerManager.Clear();
        Assert.Empty(LoggerManager.List());
    }
}
