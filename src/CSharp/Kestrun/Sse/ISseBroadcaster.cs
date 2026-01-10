using System.Threading.Channels;

namespace Kestrun.Sse;

/// <summary>
/// Provides a broadcast-style Server-Sent Events (SSE) publisher.
/// </summary>
public interface ISseBroadcaster
{
    /// <summary>
    /// Gets the number of currently connected SSE clients.
    /// </summary>
    int ConnectedCount { get; }

    /// <summary>
    /// Subscribes a client to broadcast SSE events.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token used to disconnect the client.</param>
    /// <returns>A subscription containing the client ID and a channel reader for SSE payloads.</returns>
    SseClientSubscription Subscribe(CancellationToken cancellationToken);

    /// <summary>
    /// Broadcasts an SSE event to all connected clients.
    /// </summary>
    /// <param name="eventName">The event name (optional).</param>
    /// <param name="data">The event payload.</param>
    /// <param name="id">Optional event ID.</param>
    /// <param name="retryMs">Optional client reconnect interval in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask BroadcastAsync(string? eventName, string data, string? id = null, int? retryMs = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a connected SSE client subscription.
/// </summary>
/// <param name="ClientId">Unique client identifier.</param>
/// <param name="Reader">Channel reader producing formatted SSE payloads.</param>
public readonly record struct SseClientSubscription(string ClientId, ChannelReader<string> Reader);
