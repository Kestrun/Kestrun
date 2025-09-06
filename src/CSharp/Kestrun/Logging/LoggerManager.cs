using System.Collections.Concurrent;
using Serilog;

namespace Kestrun.Logging;

/// <summary>
/// Manages a collection of named Serilog loggers and their configurations.
/// </summary>
public static class LoggerManager
{
    private static readonly ConcurrentDictionary<string, Serilog.ILogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, LoggerConfiguration> _configs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Add or replace a logger by name and optionally set it as the default logger.
    /// </summary>
    /// <param name="name">The name of the logger.</param>
    /// <param name="config">An optional configuration action to customize the logger.</param>
    /// <param name="setAsDefault">If true, sets the added logger as the Serilog default logger.</param>
    /// <returns>The created logger instance.</returns>
    public static Serilog.ILogger Add(string name, Action<LoggerConfiguration>? config = null, bool setAsDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        var cfg = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Debug();

        config?.Invoke(cfg);

        var logger = cfg.CreateLogger();
        _configs[name] = cfg;

        if (_loggers.TryGetValue(name, out var oldLogger) && oldLogger is IDisposable d)
        {
            d.Dispose();
        }

        _loggers[name] = logger;
        if (setAsDefault)
        {
            Log.Logger = logger;
        }

        return logger;
    }

    /// <summary>
    /// Register an existing Serilog logger instance under a name.
    /// </summary>
    /// <param name="name">The name of the logger.</param>
    /// <param name="logger">The logger instance to register.</param>
    /// <param name="setAsDefault">If true, sets the registered logger as the Serilog default logger.</param>
    /// <returns>The registered logger instance.</returns>
    public static Serilog.ILogger Register(string name, Serilog.ILogger logger, bool setAsDefault = false)
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

        return logger;
    }
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

    /// <summary>CloseAndFlush a logger by name.</summary>
    /// <param name="name">The name of the logger to close and flush.</param>
    /// <returns> True if the logger was found and closed; otherwise, false.</returns>
    public static bool CloseAndFlush(string name)
    {
        if (_loggers.TryRemove(name, out var logger))
        {
            if (logger is IDisposable d)
            {
                d.Dispose();
            }

            _ = _configs.TryRemove(name, out _);
            return true;
        }
        return false;
    }

    /// <summary>
    /// CloseAndFlush a logger instance.
    /// </summary>
    /// <param name="logger">The logger instance to close and flush.</param>
    /// <returns>True if the logger was found and closed; otherwise, false.</returns>
    public static bool CloseAndFlush(Serilog.ILogger logger)
    {
        if (logger is IDisposable d)
        {
            d.Dispose();
        }

        var removed = false;
        // Find all registered names that reference this logger and remove them.
        var keys = _loggers.Where(kv => kv.Value == logger).Select(kv => kv.Key).ToList();
        foreach (var key in keys)
        {
            _ = _loggers.TryRemove(key, out _);
            _ = _configs.TryRemove(key, out _);
            removed = true;
        }

        return removed;
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

    /// <summary>Remove and dispose all registered loggers.</summary>
    /// <remarks>Also clears the default logger.</remarks>
    public static void Clear()
    {
        foreach (var (_, logger) in _loggers)
        {
            if (logger is IDisposable d)
            {
                d.Dispose();
            }
        }

        _loggers.Clear();
        _configs.Clear();
    }
}
