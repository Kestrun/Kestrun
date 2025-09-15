using System.Collections.Concurrent;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Kestrun.Logging;

/// <summary>
/// Convenience extensions for hooking Serilog loggers into <see cref="LoggerManager"/>.
/// </summary>
public static class LoggerConfigurationExtensions
{
    private static readonly ConcurrentDictionary<LoggerConfiguration, LoggingLevelSwitch> _switches = new();

    private static LoggingLevelSwitch NewEnsureSwitch(LoggerConfiguration config, LogEventLevel initial = LogEventLevel.Information)
           => _switches.GetOrAdd(config, _ => new LoggingLevelSwitch(initial));
    /// <summary>
    /// Create a logger from this configuration and register it by name.
    /// </summary>
    public static Serilog.ILogger Register(this LoggerConfiguration config, string name, bool setAsDefault = false)
    {
        var logger = config.CreateLogger();
        _ = _switches.TryRemove(config, out var levelSwitch);

        _ = LoggerManager.Register(name, logger, setAsDefault, levelSwitch);

        return logger;
    }

    /// <summary>
    /// Ensure that the logger configuration has a logging level switch controlling its minimum level.
    /// </summary>
    /// <param name="config">The logger configuration to modify.</param>
    /// <param name="initial">The initial minimum level for the switch.</param>
    /// <returns>The modified logger configuration.</returns>
    public static LoggerConfiguration EnsureSwitch(this LoggerConfiguration config, LogEventLevel initial = LogEventLevel.Information)
    {
        var levelSwitch = NewEnsureSwitch(config, initial);
        _ = config.MinimumLevel.ControlledBy(levelSwitch);
        return config;
    }
}
