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

    [Fact]
    [Trait("Category", "Logging")]
    public void Register_WithoutSettingDefault_DoesNotChangeGlobalDefault()
    {
        var previous = Log.Logger;
        try
        {
            LoggerManager.Clear();
            using var sink = new CaptureSink();

            var logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(sink)
                .Register("ext-reg", setAsDefault: false);

            Assert.Same(logger, LoggerManager.Get("ext-reg"));
            Assert.NotSame(logger, Log.Logger);
        }
        finally
        {
            Log.Logger = previous;
            LoggerManager.Clear();
        }
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Register_ReturnsCreatedLogger()
    {
        var previous = Log.Logger;
        try
        {
            LoggerManager.Clear();
            using var sink = new CaptureSink();

            var logger = new LoggerConfiguration()
                .WriteTo.Sink(sink)
                .Register("test-logger");

            Assert.NotNull(logger);
            Assert.IsAssignableFrom<Serilog.ILogger>(logger);
        }
        finally
        {
            Log.Logger = previous;
            LoggerManager.Clear();
        }
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void EnsureSwitch_AddsMinimumLevelControlBySwitch()
    {
        var config = new LoggerConfiguration()
            .EnsureSwitch(LogEventLevel.Warning);

        using var sink = new CaptureSink();
        var logger = config
            .WriteTo.Sink(sink)
            .CreateLogger();

        // Log at Warning level - should work
        logger.Warning("warning");
        Assert.NotNull(sink.Last);
        Assert.Equal(LogEventLevel.Warning, sink.Last.Level);

        // Reset sink
        sink.Last = null;

        // Log at Information level - should be filtered out
        logger.Information("info");
        Assert.Null(sink.Last);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void EnsureSwitch_WithDefaultLevel_SetsInformation()
    {
        var config = new LoggerConfiguration()
            .EnsureSwitch(); // Default should be Information

        using var sink = new CaptureSink();
        var logger = config
            .WriteTo.Sink(sink)
            .CreateLogger();

        // Log at Information level - should work
        logger.Information("info");
        Assert.NotNull(sink.Last);

        // Reset sink
        sink.Last = null;

        // Log at Debug level - should be filtered out
        logger.Debug("debug");
        Assert.Null(sink.Last);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void EnsureSwitch_ReturnsConfiguration_ForChaining()
    {
        var config = new LoggerConfiguration();

        var result = config.EnsureSwitch(LogEventLevel.Error);

        // Ensure it returns the same configuration for method chaining
        Assert.Same(config, result);
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void EnsureSwitch_CanBeChained_WithOtherMethods()
    {
        var previous = Log.Logger;
        try
        {
            LoggerManager.Clear();
            using var sink = new CaptureSink();

            var logger = new LoggerConfiguration()
                .EnsureSwitch(LogEventLevel.Debug)
                .WriteTo.Sink(sink)
                .Register("chained", setAsDefault: true);

            Assert.Same(logger, Log.Logger);
            logger.Debug("debug message");
            Assert.NotNull(sink.Last);
        }
        finally
        {
            Log.Logger = previous;
            LoggerManager.Clear();
        }
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Register_WithMultipleSinks_CreatesLogger()
    {
        var previous = Log.Logger;
        try
        {
            LoggerManager.Clear();
            using var sink1 = new CaptureSink();
            using var sink2 = new CaptureSink();

            var logger = new LoggerConfiguration()
                .WriteTo.Sink(sink1)
                .WriteTo.Sink(sink2)
                .Register("multi-sink");

            logger.Information("test");

            Assert.NotNull(sink1.Last);
            Assert.NotNull(sink2.Last);
            Assert.Equal("test", sink1.Last.MessageTemplate.Text);
            Assert.Equal("test", sink2.Last.MessageTemplate.Text);
        }
        finally
        {
            Log.Logger = previous;
            LoggerManager.Clear();
        }
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Register_WithEnsureSwitch_IntegrationTest()
    {
        var previous = Log.Logger;
        try
        {
            LoggerManager.Clear();
            using var sink = new CaptureSink();

            var logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .EnsureSwitch(LogEventLevel.Warning)
                .WriteTo.Sink(sink)
                .Register("with-switch");

            // Should respect the switch level (Warning)
            logger.Information("should not appear");
            Assert.Null(sink.Last);

            logger.Warning("should appear");
            Assert.NotNull(sink.Last);
            Assert.Equal(LogEventLevel.Warning, sink.Last.Level);
        }
        finally
        {
            Log.Logger = previous;
            LoggerManager.Clear();
        }
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void RegisteredLogger_CanBeRetrieved_ByName()
    {
        var previous = Log.Logger;
        try
        {
            LoggerManager.Clear();
            var name = "unique-logger";

            var logger = new LoggerConfiguration()
                .Register(name);

            var retrieved = LoggerManager.Get(name);
            Assert.Same(logger, retrieved);
        }
        finally
        {
            Log.Logger = previous;
            LoggerManager.Clear();
        }
    }

    [Fact]
    [Trait("Category", "Logging")]
    public void Multiple_RegisterCalls_CreateDifferentLoggers()
    {
        var previous = Log.Logger;
        try
        {
            LoggerManager.Clear();
            using var sink1 = new CaptureSink();
            using var sink2 = new CaptureSink();

            var logger1 = new LoggerConfiguration()
                .WriteTo.Sink(sink1)
                .Register("logger1");

            var logger2 = new LoggerConfiguration()
                .WriteTo.Sink(sink2)
                .Register("logger2");

            Assert.NotSame(logger1, logger2);
            Assert.Same(logger1, LoggerManager.Get("logger1"));
            Assert.Same(logger2, LoggerManager.Get("logger2"));
        }
        finally
        {
            Log.Logger = previous;
            LoggerManager.Clear();
        }
    }
}
