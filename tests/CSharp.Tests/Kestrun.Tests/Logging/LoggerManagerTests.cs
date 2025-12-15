using Kestrun.Health;
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

            @new.Information(ProbeStatusLabels.STATUS_OK);
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
    public void GetDefault_ReturnsGlobalDefaultLogger()
    {
        var previous = Log.Logger;
        try
        {
            var defaultLogger = LoggerManager.GetDefault();
            Assert.Same(Log.Logger, defaultLogger);
        }
        finally
        {
            Log.Logger = previous;
        }
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void DefaultLogger_Property_Get_ReturnsCurrentDefault()
    {
        var previous = Log.Logger;
        try
        {
            LoggerManager.Clear();
            var logger = new LoggerConfiguration().CreateLogger();
            LoggerManager.Register("test", logger, setAsDefault: true);

            Assert.Same(logger, LoggerManager.DefaultLogger);
        }
        finally
        {
            Log.Logger = previous;
            LoggerManager.Clear();
        }
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void DefaultLogger_Property_Set_ChangesDefault()
    {
        var previous = Log.Logger;
        try
        {
            var newLogger = new LoggerConfiguration().CreateLogger();
            LoggerManager.DefaultLogger = newLogger;

            Assert.Same(newLogger, Log.Logger);
        }
        finally
        {
            Log.Logger = previous;
        }
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void DefaultLoggerName_Get_ReturnsNameOfDefault()
    {
        var previous = Log.Logger;
        try
        {
            LoggerManager.Clear();
            var logger = new LoggerConfiguration().CreateLogger();
            LoggerManager.Register("mylogger", logger, setAsDefault: true);

            Assert.Equal("mylogger", LoggerManager.DefaultLoggerName);
        }
        finally
        {
            Log.Logger = previous;
            LoggerManager.Clear();
        }
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void DefaultLoggerName_Set_ChangesDefault()
    {
        var previous = Log.Logger;
        try
        {
            LoggerManager.Clear();
            var logger1 = new LoggerConfiguration().CreateLogger();
            var logger2 = new LoggerConfiguration().CreateLogger();
            LoggerManager.Register("first", logger1, setAsDefault: true);
            LoggerManager.Register("second", logger2, setAsDefault: false);

            LoggerManager.DefaultLoggerName = "second";
            Assert.Same(logger2, Log.Logger);
        }
        finally
        {
            Log.Logger = previous;
            LoggerManager.Clear();
        }
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void DefaultLoggerName_Set_WithNonexistent_ThrowsArgumentException()
    {
        var previous = Log.Logger;
        try
        {
            LoggerManager.Clear();

            Assert.Throws<ArgumentException>(() => LoggerManager.DefaultLoggerName = "nonexistent");
        }
        finally
        {
            Log.Logger = previous;
            LoggerManager.Clear();
        }
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void SetLevelSwitch_CreatesOrUpdatesSwitch()
    {
        LoggerManager.Clear();

        LoggerManager.SetLevelSwitch("test", LogEventLevel.Warning);
        var level = LoggerManager.GetLevelSwitch("test");

        Assert.NotNull(level);
        Assert.Equal(LogEventLevel.Warning, level.Value);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void SetLevelSwitch_Updates_ExistingSwitch()
    {
        LoggerManager.Clear();

        LoggerManager.SetLevelSwitch("logger", LogEventLevel.Information);
        LoggerManager.SetLevelSwitch("logger", LogEventLevel.Debug);

        var level = LoggerManager.GetLevelSwitch("logger");
        Assert.NotNull(level);
        Assert.Equal(LogEventLevel.Debug, level.Value);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void GetLevelSwitch_ReturnsNull_WhenNotFound()
    {
        LoggerManager.Clear();

        var level = LoggerManager.GetLevelSwitch("nonexistent");

        Assert.Null(level);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void ListLevels_ReturnsAllSwitches()
    {
        LoggerManager.Clear();

        LoggerManager.SetLevelSwitch("logger1", LogEventLevel.Information);
        LoggerManager.SetLevelSwitch("logger2", LogEventLevel.Warning);
        LoggerManager.SetLevelSwitch("logger3", LogEventLevel.Error);

        var levels = LoggerManager.ListLevels();

        Assert.Contains("logger1", levels.Keys);
        Assert.Contains("logger2", levels.Keys);
        Assert.Contains("logger3", levels.Keys);
        Assert.Equal(LogEventLevel.Information, levels["logger1"]);
        Assert.Equal(LogEventLevel.Warning, levels["logger2"]);
        Assert.Equal(LogEventLevel.Error, levels["logger3"]);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void CloseAndFlush_ByName_RemovesLogger()
    {
        LoggerManager.Clear();
        var logger = new LoggerConfiguration().CreateLogger();
        LoggerManager.Register("test", logger);

        var result = LoggerManager.CloseAndFlush("test");

        Assert.True(result);
        Assert.Null(LoggerManager.Get("test"));
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void CloseAndFlush_ByName_WithNonexistent_ReturnsFalse()
    {
        LoggerManager.Clear();

        var result = LoggerManager.CloseAndFlush("nonexistent");

        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void CloseAndFlush_ByInstance_RemovesLogger()
    {
        LoggerManager.Clear();
        var logger = new LoggerConfiguration().CreateLogger();
        LoggerManager.Register("test", logger);

        var result = LoggerManager.CloseAndFlush(logger);

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void CloseAndFlush_ByInstance_WithNonexistent_ReturnsFalse()
    {
        LoggerManager.Clear();
        var logger = new LoggerConfiguration().CreateLogger();

        var result = LoggerManager.CloseAndFlush(logger);

        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void GetName_ReturnsLoggerName()
    {
        LoggerManager.Clear();
        var logger = new LoggerConfiguration().CreateLogger();
        LoggerManager.Register("mylogger", logger);

        var name = LoggerManager.GetName(logger);

        Assert.Equal("mylogger", name);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void GetName_ReturnsNull_WhenNotFound()
    {
        LoggerManager.Clear();
        var logger = new LoggerConfiguration().CreateLogger();

        var name = LoggerManager.GetName(logger);

        Assert.Null(name);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void TryGetName_ReturnsTrueAndName_WhenFound()
    {
        LoggerManager.Clear();
        var logger = new LoggerConfiguration().CreateLogger();
        LoggerManager.Register("test", logger);

        var found = LoggerManager.TryGetName(logger, out var name);

        Assert.True(found);
        Assert.Equal("test", name);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void TryGetName_ReturnsFalseAndNull_WhenNotFound()
    {
        LoggerManager.Clear();
        var logger = new LoggerConfiguration().CreateLogger();

        var found = LoggerManager.TryGetName(logger, out var name);

        Assert.False(found);
        Assert.Null(name);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Contains_ByName_ReturnsTrueWhenExists()
    {
        LoggerManager.Clear();
        var logger = new LoggerConfiguration().CreateLogger();
        LoggerManager.Register("test", logger);

        Assert.True(LoggerManager.Contains("test"));
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Contains_ByName_ReturnsFalseWhenNotExists()
    {
        LoggerManager.Clear();

        Assert.False(LoggerManager.Contains("nonexistent"));
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Contains_ByInstance_ReturnsTrueWhenExists()
    {
        LoggerManager.Clear();
        var logger = new LoggerConfiguration().CreateLogger();
        LoggerManager.Register("test", logger);

        Assert.True(LoggerManager.Contains(logger));
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Contains_ByInstance_ReturnsFalseWhenNotExists()
    {
        LoggerManager.Clear();
        var logger = new LoggerConfiguration().CreateLogger();

        Assert.False(LoggerManager.Contains(logger));
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Contains_ByConfig_ReturnsTrueWhenExists()
    {
        LoggerManager.Clear();
        var config = LoggerManager.New("test");
        var logger = config.CreateLogger();
        LoggerManager.Register("test", logger);

        Assert.True(LoggerManager.Contains(config));
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Get_ReturnsLogger_WhenExists()
    {
        LoggerManager.Clear();
        var logger = new LoggerConfiguration().CreateLogger();
        LoggerManager.Register("test", logger);

        var retrieved = LoggerManager.Get("test");

        Assert.Same(logger, retrieved);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Get_ReturnsNull_WhenNotExists()
    {
        LoggerManager.Clear();

        var retrieved = LoggerManager.Get("nonexistent");

        Assert.Null(retrieved);
    }

}
