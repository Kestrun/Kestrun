using System.Collections.Concurrent;

namespace Kestrun.Utilities;

/// <summary>
/// Provides a registry of named <see cref="SemaphoreSlim"/> instances used to
/// synchronize access to shared resources within the current process.
/// The same key always returns the same semaphore instance.
/// </summary>
public static class KestrunLockRegistry
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new() { ["default"] = new SemaphoreSlim(1, 1) };

    /// <summary>
    /// Gets an existing semaphore for the specified key, or creates a new one with an initial and maximum count of 1.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <returns>A <see cref="SemaphoreSlim"/> that can be awaited to serialize access to the resource identified by <paramref name="key"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
    public static SemaphoreSlim GetOrCreate(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _locks.GetOrAdd(key.Trim().ToLowerInvariant(), static _ => Create());
    }

    /// <summary>
    /// Attempts to get the semaphore associated with the specified key without creating a new one if it doesn't exist.
    /// </summary>
    /// <param name="key">The resource key.</param>
    /// <param name="semaphore">The semaphore associated with the specified key, if it exists.</param>
    /// <returns>True if the semaphore exists; otherwise, false.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    public static bool TryGet(string key, out SemaphoreSlim? semaphore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        key = key.Trim().ToLowerInvariant();
        return _locks.TryGetValue(key, out semaphore);
    }

    /// <summary>
    /// Gets a default semaphore instance that can be used for general synchronization needs when a specific key is not required.
    /// This is useful for simple scenarios where a single lock is sufficient to protect a critical section of code or a shared resource without the need for multiple named locks.
    /// </summary>
    public static SemaphoreSlim Default => _locks["default"];

    private static SemaphoreSlim Create() => new(1, 1);
}
