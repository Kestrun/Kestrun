namespace Kestrun.SignalR;

/// <summary>
/// Provides an interface for broadcasting real-time messages to connected SignalR clients.
/// </summary>
public interface IRealtimeBroadcaster
{
    /// <summary>
    /// Broadcasts a log message to all connected clients.
    /// </summary>
    /// <param name="level">The log level (e.g., Information, Warning, Error).</param>
    /// <param name="message">The log message to broadcast.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task BroadcastLogAsync(string level, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a custom event to all connected clients.
    /// </summary>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="data">The event data to broadcast.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task BroadcastEventAsync(string eventName, object? data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a message to a specific group of clients.
    /// </summary>
    /// <param name="groupName">The name of the group to broadcast to.</param>
    /// <param name="method">The SignalR method name to invoke on clients.</param>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task BroadcastToGroupAsync(string groupName, string method, object? message, CancellationToken cancellationToken = default);
}
