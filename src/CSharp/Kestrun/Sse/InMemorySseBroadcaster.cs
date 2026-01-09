using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Kestrun.Sse;

/// <summary>
/// In-memory implementation of <see cref="ISseBroadcaster"/>.
/// Tracks connected clients and broadcasts formatted SSE payloads via per-client channels.
/// </summary>
public sealed class InMemorySseBroadcaster(Serilog.ILogger logger) : ISseBroadcaster
{
    private readonly ConcurrentDictionary<string, Channel<string>> _clients = new();
    private readonly Serilog.ILogger _logger = logger;

    /// <inheritdoc />
    public int ConnectedCount => _clients.Count;

    /// <inheritdoc />
    public SseClientSubscription Subscribe(CancellationToken cancellationToken)
    {
        var clientId = Guid.NewGuid().ToString("n");

        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity: 256)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        if (!_clients.TryAdd(clientId, channel))
        {
            // Extremely unlikely; retry once with a different id.
            clientId = Guid.NewGuid().ToString("n");
            if (!_clients.TryAdd(clientId, channel))
            {
                throw new InvalidOperationException("Failed to register SSE client.");
            }
        }

        if (cancellationToken.CanBeCanceled)
        {
            _ = cancellationToken.Register(() => RemoveClient(clientId));
        }

        _logger.Debug("SSE client subscribed: {ClientId} (total={Count})", clientId, ConnectedCount);
        return new SseClientSubscription(clientId, channel.Reader);
    }

    /// <inheritdoc />
    public ValueTask BroadcastAsync(string? eventName, string data, string? id = null, int? retryMs = null, CancellationToken cancellationToken = default)
    {
        if (_clients.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        var payload = SseEventFormatter.Format(eventName, data, id, retryMs);

        foreach (var kvp in _clients)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!kvp.Value.Writer.TryWrite(payload))
            {
                RemoveClient(kvp.Key);
            }
        }

        return ValueTask.CompletedTask;
    }

    private void RemoveClient(string clientId)
    {
        if (_clients.TryRemove(clientId, out var channel))
        {
            _ = channel.Writer.TryComplete();
            _logger.Debug("SSE client removed: {ClientId} (total={Count})", clientId, ConnectedCount);
        }
    }
}
