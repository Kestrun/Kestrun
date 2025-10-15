using System.Collections.Concurrent;

namespace Kestrun.SignalR;

/// <summary>
/// In-memory thread-safe implementation of <see cref="IConnectionTracker"/>.
/// </summary>
public sealed class InMemoryConnectionTracker : IConnectionTracker
{
    private readonly ConcurrentDictionary<string, byte> _connections = new();

    /// <summary>
    /// Gets the current number of connected clients.
    /// </summary>
    public int ConnectedCount => _connections.Count;

    /// <summary>
    /// Records a new connection.
    /// </summary>
    /// <param name="connectionId">The ID of the connection.</param>
    public void OnConnected(string connectionId)
    {
        if (!string.IsNullOrEmpty(connectionId))
        {
            _connections[connectionId] = 1;
        }
    }

    /// <summary>
    /// Records a disconnection.
    /// </summary>
    /// <param name="connectionId">The ID of the connection.</param>
    public void OnDisconnected(string connectionId)
    {
        if (!string.IsNullOrEmpty(connectionId))
        {
            _ = _connections.TryRemove(connectionId, out _);
        }
    }

    /// <summary>
    /// Returns a snapshot of current connection identifiers.
    /// </summary>
    /// <returns>A read-only collection of connection IDs.</returns>
    public IReadOnlyCollection<string> GetConnections() => _connections.Keys as IReadOnlyCollection<string> ?? [.. _connections.Keys];
}
