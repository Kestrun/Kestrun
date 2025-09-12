using System.Collections.Concurrent;
using Kestrun.Hosting;
using Serilog;
using Serilog.Events;

namespace Kestrun;

/// <summary>
/// Provides management functionality for KestrunHost instances, including creation, retrieval, starting, stopping, and destruction.
/// </summary>
public static class KestrunHostManager
{
    private static readonly ConcurrentDictionary<string, KestrunHost> _instances = new(StringComparer.OrdinalIgnoreCase);
    private static string? _defaultName;

    /// <summary>
    /// Gets the collection of names for all KestrunHost instances.
    /// </summary>
    public static IReadOnlyCollection<string> InstanceNames => (IReadOnlyCollection<string>)_instances.Keys;

    private static string? _kestrunRoot;
    /// <summary>
    /// Gets or sets the root path for Kestrun operations.
    /// </summary>
    public static string? KestrunRoot
    {
        get => _kestrunRoot;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Kestrun root path cannot be null or empty.", nameof(value));
            }

            if (Directory.GetCurrentDirectory() != value)
            {
                Directory.SetCurrentDirectory(value);
            }
            _kestrunRoot = value;
        }
    }


    /// <summary>
    /// Creates a new KestrunHost instance using the provided factory function.
    /// </summary>
    /// <param name="name">The name of the KestrunHost instance to create.</param>
    /// <param name="factory">A factory function that returns a new KestrunHost instance.</param>
    /// <param name="setAsDefault">Whether to set this instance as the default.</param>
    /// <returns>The created KestrunHost instance.</returns>
    public static KestrunHost Create(string name, Func<KestrunHost> factory, bool setAsDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Instance name is required.", nameof(name));
        }

        if (_instances.ContainsKey(name))
        {
            throw new InvalidOperationException($"A KestrunHost instance with the name '{name}' already exists.");
        }

        var host = factory();
        _instances[name] = host;

        if (setAsDefault || _defaultName == null)
        {
            _defaultName = name;
        }

        return host;
    }
    /// <summary>
    /// Creates a new KestrunHost instance with the specified name and optional module paths, using the default logger.
    /// </summary>
    /// <param name="name">The name of the KestrunHost instance to create.</param>
    /// <param name="modulePathsObj">Optional array of module paths to load.</param>
    /// <param name="setAsDefault">Whether to set this instance as the default.</param>
    /// <returns>The created KestrunHost instance.</returns>
    public static KestrunHost Create(string name,
         string[]? modulePathsObj = null, bool setAsDefault = false) =>
        // Call the overload with a default logger (null or a default instance as appropriate)
        Create(name, Log.Logger, modulePathsObj, setAsDefault);

    /// <summary>
    /// Creates a new KestrunHost instance with the specified name, logger, root path, and optional module paths.
    /// </summary>
    /// <param name="name">The name of the KestrunHost instance to create.</param>
    /// <param name="logger">The Serilog logger to use for the host.</param>
    /// <param name="modulePathsObj">Optional array of module paths to load.</param>
    /// <param name="setAsDefault">Whether to set this instance as the default.</param>
    /// <returns>The created KestrunHost instance.</returns>
    public static KestrunHost Create(string name, Serilog.ILogger logger,
         string[]? modulePathsObj = null, bool setAsDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Instance name is required.", nameof(name));
        }

        if (_instances.ContainsKey(name))
        {
            throw new InvalidOperationException($"A KestrunHost instance with the name '{name}' already exists.");
        }

        if (KestrunRoot is null)
        {
            throw new InvalidOperationException("Kestrun root path must be set before creating a KestrunHost instance.");
        }

        var host = new KestrunHost(name, logger, KestrunRoot, modulePathsObj);
        _instances[name] = host;

        if (setAsDefault || _defaultName == null)
        {
            _defaultName = name;
        }

        return host;
    }

    /// <summary>
    /// Determines whether a KestrunHost instance with the specified name exists.
    /// </summary>
    /// <param name="name">The name of the KestrunHost instance to check for existence.</param>
    /// <returns>True if an instance with the specified name exists; otherwise, false.</returns>
    public static bool Contains(string name) => _instances.ContainsKey(name);

    /// <summary>
    /// Attempts to retrieve a KestrunHost instance by its name.
    /// </summary>
    /// <param name="name">The name of the KestrunHost instance to retrieve.</param>
    /// <param name="host">When this method returns, contains the KestrunHost instance associated with the specified name, if found; otherwise, null.</param>
    /// <returns>True if the instance was found; otherwise, false.</returns>
    public static bool TryGet(string name, out KestrunHost? host) =>
        _instances.TryGetValue(name, out host);

    /// <summary>
    /// Retrieves a KestrunHost instance by its name.
    /// </summary>
    /// <param name="name">The name of the KestrunHost instance to retrieve.</param>
    /// <returns>The KestrunHost instance if found; otherwise, null.</returns>
    public static KestrunHost? Get(string name) =>
        _instances.TryGetValue(name, out var host) ? host : null;

    /// <summary>
    /// Gets the default KestrunHost instance, if one has been set.
    /// </summary>
    public static KestrunHost? Default => _defaultName != null && _instances.TryGetValue(_defaultName, out var host) ? host : null;

    /// <summary>
    /// Sets the specified KestrunHost instance as the default.
    /// </summary>
    /// <param name="name">The name of the KestrunHost instance to set as default.</param>
    /// <exception cref="InvalidOperationException">Thrown if no instance with the specified name exists.</exception>
    public static void SetDefault(string name)
    {
        if (!_instances.ContainsKey(name))
        {
            throw new InvalidOperationException($"No KestrunHost instance named '{name}' exists.");
        }

        _defaultName = name;
    }

    /// <summary>
    /// Determines whether the specified KestrunHost instance is currently running.
    /// </summary>
    /// <param name="name">The name of the KestrunHost instance to check.</param>
    /// <returns>True if the instance is running; otherwise, false.</returns>
    public static bool IsRunning(string name) => TryGet(name, out var host) && host != null && host.IsRunning;

    /// <summary>
    /// Starts the specified KestrunHost instance asynchronously.
    /// </summary>
    /// <param name="name">The name of the KestrunHost instance to start.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    public static async Task StartAsync(string name, CancellationToken ct = default)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Starting (Async) KestrunHost instance '{Name}'", name);
        }

        if (TryGet(name, out var host))
        {
            if (host is not null)
            {
                await host.StartAsync(ct);
            }
            else
            {
                throw new InvalidOperationException($"KestrunHost instance '{name}' is null.");
            }
        }
        else
        {
            throw new InvalidOperationException($"No KestrunHost instance named '{name}' exists.");
        }
    }

    /// <summary>
    /// Stops the specified KestrunHost instance asynchronously.
    /// </summary>
    /// <param name="name">The name of the KestrunHost instance to stop.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    public static async Task StopAsync(string name, CancellationToken ct = default)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Stopping (Async) KestrunHost instance '{Name}'", name);
        }

        if (TryGet(name, out var host))
        {
            if (host is not null)
            {
                await host.StopAsync(ct);
            }
            else
            {
                throw new InvalidOperationException($"KestrunHost instance '{name}' is null.");
            }
        }
        else
        {
            throw new InvalidOperationException($"No KestrunHost instance named '{name}' exists.");
        }
    }

    /// <summary>
    /// Stops the specified KestrunHost instance synchronously.
    /// </summary>
    /// <param name="name">The name of the KestrunHost instance to stop.</param>
    public static void Stop(string name)
    {
        if (_instances.TryGetValue(name, out var host))
        {
            host.Stop();
        }
    }

    /// <summary>
    /// Stops all KestrunHost instances asynchronously.
    /// </summary>
    /// <param name="ct">A cancellation token to observe while waiting for the tasks to complete.</param>
    public static async Task StopAllAsync(CancellationToken ct = default)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Stopping all KestrunHost instances (Async)");
        }

        foreach (var kv in _instances)
        {
            await kv.Value.StopAsync(ct);
        }
    }

    /// <summary>
    /// Destroys the specified KestrunHost instance and disposes its resources.
    /// </summary>
    /// <param name="name">The name of the KestrunHost instance to destroy.</param>
    /// <param name="disposeLogger">Whether to dispose the Serilog logger if this was the last instance.</param>
    /// <remarks>
    /// If this is the last instance, the logger will be disposed to release any resources.
    /// </remarks>
    public static void Destroy(string name, bool disposeLogger = false)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Destroying KestrunHost instance '{Name}'", name);
        }

        if (_instances.TryRemove(name, out var host))
        {
            host.Dispose();
            if (_defaultName == name)
            {
                _defaultName = _instances.Keys.FirstOrDefault();
            }


            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("KestrunHost instance '{Name}' destroyed", name);
            }
        }
        else
        {
            if (Log.IsEnabled(LogEventLevel.Warning))
            {
                Log.Warning("No KestrunHost instance named '{Name}' exists to destroy", name);
            }
        }

        // If no more instances, clear default and close logger
        if (_instances.IsEmpty)
        {
            _defaultName = null;
            // If no more instances, close logger
            if (disposeLogger) { Log.CloseAndFlush(); }
        }
    }

    /// <summary>
    /// Destroys all KestrunHost instances and disposes their resources.
    /// </summary>
    public static void DestroyAll()
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Destroying all KestrunHost instances");
        }

        foreach (var name in _instances.Keys.ToList())
        {
            Destroy(name);
        }
    }
}
