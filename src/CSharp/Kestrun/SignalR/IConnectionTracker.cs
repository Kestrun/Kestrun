using System.Collections.Generic;

namespace Kestrun.SignalR;

/// <summary>
/// Tracks connected SignalR clients for the Kestrun hub.
/// </summary>
public interface IConnectionTracker
{
    /// <summary>
    /// Gets the current number of connected clients.
    /// </summary>
    int ConnectedCount { get; }

    /// <summary>
    /// Records a new connection.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    void OnConnected(string connectionId);

    /// <summary>
    /// Records a disconnection.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    void OnDisconnected(string connectionId);

    /// <summary>
    /// Returns a snapshot of current connection identifiers.
    /// </summary>
    IReadOnlyCollection<string> GetConnections();
}
