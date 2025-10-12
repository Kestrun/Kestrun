using Microsoft.AspNetCore.SignalR;

namespace Kestrun.SignalR;

/// <summary>
/// Default SignalR hub for Kestrun providing real-time communication capabilities.
/// Clients can connect to this hub to receive log messages, events, and other real-time updates.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KestrunHub"/> class.
/// </remarks>
/// <param name="logger">The Serilog logger instance.</param>
public class KestrunHub(Serilog.ILogger logger) : Hub
{
    private readonly Serilog.ILogger _logger = logger;

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.Information("SignalR client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnection, if any.</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.Warning(exception, "SignalR client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.Information("SignalR client disconnected: {ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Allows a client to join a specific group.
    /// </summary>
    /// <param name="groupName">The name of the group to join.</param>
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.Information("Client {ConnectionId} joined group {GroupName}", Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Allows a client to leave a specific group.
    /// </summary>
    /// <param name="groupName">The name of the group to leave.</param>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.Information("Client {ConnectionId} left group {GroupName}", Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Echoes a message back to the caller (useful for testing).
    /// </summary>
    /// <param name="message">The message to echo.</param>
    public async Task Echo(string message) => await Clients.Caller.SendAsync("ReceiveEcho", message);
}
