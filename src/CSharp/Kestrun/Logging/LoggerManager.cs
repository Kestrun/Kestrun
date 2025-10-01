using System.Collections.Concurrent;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Kestrun.Logging;

/// <summary>
/// Manages a collection of named Serilog loggers and their configurations.
/// </summary>
public static class LoggerManager
{
    private static readonly ConcurrentDictionary<string, Serilog.ILogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, LoggerConfiguration> _configs = new(StringComparer.OrdinalIgnoreCase);
    // Synchronization object to guard global default logger transitions and coordinated disposal
    private static readonly Lock _sync = new();

    /// <summary>
    /// A collection of named logging level switches for dynamic log level control.
    /// </summary>
    private static readonly ConcurrentDictionary<string, LoggingLevelSwitch> _switches = new(StringComparer.OrdinalIgnoreCase);


    internal static LoggingLevelSwitch EnsureSwitch(string name, LogEventLevel initial = LogEventLevel.Information)
        => _switches.GetOrAdd(name, _ => new LoggingLevelSwitch(initial));

    /// <summary>
    /// Register an existing Serilog logger instance under a name.
    /// </summary>
    /// <param name="name">The name of the logger.</param>
    /// <param name="logger">The logger instance to register.</param>
    /// <param name="setAsDefault">If true, sets the registered logger as the Serilog default logger.</param>
    /// <param name="levelSwitch">An optional logging level switch to associate with the logger for dynamic level control.</param>
    /// <returns>The registered logger instance.</returns>
    public static Serilog.ILogger Register(string name, Serilog.ILogger logger, bool setAsDefault = false, LoggingLevelSwitch? levelSwitch = null)
    {
        if (_loggers.TryGetValue(name, out var oldLogger) && oldLogger is IDisposable d)
        {
            d.Dispose();
        }

        _loggers[name] = logger;
        if (setAsDefault)
        {
            Log.Logger = logger;
        }
        if (levelSwitch != null)
        {
            _switches[name] = levelSwitch;
        }
        return logger;
    }

    /// <summary>
    /// Returns the current Serilog default logger (Log.Logger).
    /// </summary>
    public static Serilog.ILogger GetDefault() => Log.Logger;

    private static Serilog.ILogger CreateBaselineLogger() => new LoggerConfiguration().CreateLogger();

    /// <summary>
    /// Create a new <see cref="LoggerConfiguration"/> associated with a name.
    /// </summary>
    /// <param name="name">The name of the logger configuration.</param>
    /// <returns>The new logger configuration.</returns>
    public static LoggerConfiguration New(string name)
    {
        var cfg = new LoggerConfiguration()
            .Enrich.WithProperty("LoggerName", name);
        _configs[name] = cfg;
        return cfg;
    }


    /// <summary>Set the minimum level for a named logger’s switch.</summary>
    /// <param name="name">The name of the logger.</param>
    /// <param name="level">The new minimum level to set.</param>
    /// <remarks>
    /// If the logger or switch does not exist, they will be created.
    /// </remarks>
    public static void SetLevelSwitch(string name, LogEventLevel level)
    {
        var sw = EnsureSwitch(name, level);
        sw.MinimumLevel = level;
    }

    /// <summary>Get the current minimum level for a named logger’s switch.</summary>
    /// <param name="name">The name of the logger.</param>
    /// <returns>The current minimum level, or null if the logger or switch is not found.</returns>
    public static LogEventLevel? GetLevelSwitch(string name)
        => _switches.TryGetValue(name, out var sw) ? sw.MinimumLevel : null;

    /// <summary>List all switches and their current levels.</summary>
    /// <returns>A dictionary of logger names and their minimum levels.</returns>
    public static IReadOnlyDictionary<string, LogEventLevel> ListLevels()
        => _switches.ToDictionary(kv => kv.Key, kv => kv.Value.MinimumLevel, StringComparer.OrdinalIgnoreCase);


    /// <summary>CloseAndFlush a logger by name.</summary>
    /// <param name="name">The name of the logger to close and flush.</param>
    /// <returns> True if the logger was found and closed; otherwise, false.</returns>
    public static bool CloseAndFlush(string name)
    {
        if (!_loggers.TryRemove(name, out var logger))
        {
            return false;
        }

        bool wasDefault;
        // Capture & decide inside lock to avoid race with other threads mutating Log.Logger
        lock (_sync)
        {
            wasDefault = ReferenceEquals(Log.Logger, logger);
        }

        if (logger is IDisposable d)
        {
            // Dispose outside lock (Serilog flush/dispose can perform I/O)
            d.Dispose();
        }
        _ = _configs.TryRemove(name, out _);
        _ = _switches.TryRemove(name, out _);

        if (wasDefault)
        {
            lock (_sync)
            {
                // Re-check in case default changed while disposing
                if (ReferenceEquals(Log.Logger, logger))
                {
                    Log.Logger = CreateBaselineLogger();
                }
            }
        }
        return true;
    }

    /// <summary>
    /// CloseAndFlush a logger instance.
    /// </summary>
    /// <param name="logger">The logger instance to close and flush.</param>
    /// <returns>True if the logger was found and closed; otherwise, false.</returns>
    public static bool CloseAndFlush(Serilog.ILogger logger)
    {
        bool wasDefault;
        lock (_sync)
        {
            wasDefault = ReferenceEquals(Log.Logger, logger);
        }

        if (logger is IDisposable d)
        {
            d.Dispose();
        }

        var removed = false;
        var keys = _loggers.Where(kv => ReferenceEquals(kv.Value, logger)).Select(kv => kv.Key).ToList();
        foreach (var key in keys)
        {
            _ = _loggers.TryRemove(key, out _);
            _ = _configs.TryRemove(key, out _);
            _ = _switches.TryRemove(key, out _);
            removed = true;
        }

        if (wasDefault)
        {
            lock (_sync)
            {
                if (ReferenceEquals(Log.Logger, logger))
                {
                    Log.Logger = CreateBaselineLogger();
                }
            }
        }
        return removed;
    }

    /// <summary>
    /// Get the name of a registered logger instance.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <returns>The name of the logger, or null if not found.</returns>
    public static string? GetName(Serilog.ILogger logger)
    {
        foreach (var kv in _loggers)
        {
            if (ReferenceEquals(kv.Value, logger))
            {
                return kv.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// Try to get the name of a registered logger instance.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="name">The name of the logger, if found.</param>
    /// <returns>True if the name was found; otherwise, false.</returns>
    public static bool TryGetName(Serilog.ILogger logger, out string? name)
    {
        name = GetName(logger);
        return name is not null;
    }
    /// <summary>
    /// Check if a logger, configuration, or name exists.
    /// </summary>
    /// <param name="name">The name of the logger.</param>
    /// <returns> True if the logger exists; otherwise, false.</returns>
    public static bool Contains(string name) => _loggers.ContainsKey(name);

    /// <summary>
    /// Check if a logger, configuration, or name exists.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <returns> True if the logger exists; otherwise, false.</returns>
    public static bool Contains(Serilog.ILogger logger) => _loggers.Values.Contains(logger);

    /// <summary>
    /// Check if a logger, configuration, or name exists.
    /// </summary>
    /// <param name="config">The logger configuration instance.</param>
    /// <returns> True if the configuration exists; otherwise, false.</returns>
    public static bool Contains(LoggerConfiguration config) => _configs.Values.Contains(config);


    /// <summary>The name of the logger currently set as the Serilog default.</summary>
    /// <exception cref="ArgumentException">When the specified logger name is not found.</exception>
    public static string DefaultLoggerName
    {
        get => _loggers.FirstOrDefault(x => x.Value == Log.Logger).Key;
        set => Log.Logger = _loggers.TryGetValue(value, out var logger) ? logger :
            throw new ArgumentException($"Logger '{value}' not found.", nameof(value));
    }

    /// <summary>Access the Serilog default logger.</summary>
    /// <remarks>Setting this property to null resets the default logger to a new empty logger.</remarks>
    public static Serilog.ILogger DefaultLogger
    {
        get => Log.Logger;
        set => Log.Logger = value ?? new LoggerConfiguration().CreateLogger();
    }

    /// <summary>Get a logger by name, or null if not found.</summary>
    /// <param name="name">The name of the logger.</param>
    /// <returns>The logger instance, or null if not found.</returns>
    public static Serilog.ILogger? Get(string name) => _loggers.TryGetValue(name, out var logger) ? logger : null;

    /// <summary>List all registered logger names.</summary>
    public static string[] List() => [.. _loggers.Keys];

    /// <summary>
    /// List all registered logger instances.
    /// </summary>
    /// <remarks>
    /// The returned array is a snapshot; subsequent registrations or disposals will not affect it.
    /// </remarks>
    public static Serilog.ILogger[] ListLoggers() => [.. _loggers.Values];

    /// <summary>Remove and dispose all registered loggers.</summary>
    /// <remarks>Also clears the default logger.</remarks>
    public static void Clear()
    {
        // Snapshot keys to minimize time under lock and avoid enumerating while mutated
        var snapshot = _loggers.ToArray();
        foreach (var (_, logger) in snapshot)
        {
            if (logger is IDisposable d)
            {
                try { d.Dispose(); } catch { /* swallow to ensure all loggers attempt disposal */ }
            }
        }
        _loggers.Clear();
        _configs.Clear();
        _switches.Clear();
        lock (_sync)
        {
            Log.Logger = CreateBaselineLogger();
        }
    }
}
