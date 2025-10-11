using Microsoft.AspNetCore.SignalR;

namespace Kestrun.SignalR;

/// <summary>
/// Default implementation of <see cref="IRealtimeBroadcaster"/> that broadcasts messages via SignalR.
/// </summary>
public class RealtimeBroadcaster : IRealtimeBroadcaster
{
    private readonly IHubContext<KestrunHub> _hubContext;
    private readonly Serilog.ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RealtimeBroadcaster"/> class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context for KestrunHub.</param>
    /// <param name="logger">The Serilog logger instance.</param>
    public RealtimeBroadcaster(IHubContext<KestrunHub> hubContext, Serilog.ILogger logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task BroadcastLogAsync(string level, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync(
                "ReceiveLog",
                new
                {
                    level,
                    message,
                    timestamp = DateTime.UtcNow
                },
                cancellationToken);

            _logger.Debug("Broadcasted log message: {Level} - {Message}", level, message);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to broadcast log message: {Level} - {Message}", level, message);
        }
    }

    /// <inheritdoc/>
    public async Task BroadcastEventAsync(string eventName, object? data, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync(
                "ReceiveEvent",
                new
                {
                    eventName,
                    data,
                    timestamp = DateTime.UtcNow
                },
                cancellationToken);

            _logger.Debug("Broadcasted event: {EventName}", eventName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to broadcast event: {EventName}", eventName);
        }
    }

    /// <inheritdoc/>
    public async Task BroadcastToGroupAsync(string groupName, string method, object? message, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.Group(groupName).SendAsync(method, message, cancellationToken);
            _logger.Debug("Broadcasted message to group {GroupName} via method {Method}", groupName, method);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to broadcast to group {GroupName}", groupName);
        }
    }
}
