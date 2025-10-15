using Kestrun.SignalR;

namespace Kestrun.Hosting;

/// <summary>
/// Extension methods for KestrunHost to support SignalR real-time broadcasting.
/// </summary>
public static class KestrunHostSignalRExtensions
{
    /// <summary>
    /// Gets the number of currently connected SignalR clients if the Kestrun hub is configured.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <returns>The count of connected clients, or null if SignalR hub/connection tracking is not configured.</returns>
    public static int? GetConnectedClientCount(this KestrunHost host)
    {
        var svcProvider = host.App?.Services;
        return svcProvider == null
            ? null
            : svcProvider.GetService(typeof(IConnectionTracker)) is IConnectionTracker tracker ? tracker.ConnectedCount : (int?)null;
    }

    /// <summary>
    /// Broadcasts a log message to all connected SignalR clients using the best available service provider.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="level">The log level (e.g., Information, Warning, Error, Debug, Verbose).</param>
    /// <param name="message">The log message to broadcast.</param>
    /// <param name="httpContext">Optional: The current HttpContext, if available.</param>
    /// <param name="cancellationToken">Optional: Cancellation token.</param>
    public static async Task<bool> BroadcastLogAsync(this KestrunHost host, string level, string message, HttpContext? httpContext = null, CancellationToken cancellationToken = default)
    {
        if (httpContext != null)
        {
            var ctx = new Models.KestrunContext(host, httpContext);
            return await ctx.BroadcastLogAsync(level, message, cancellationToken).ConfigureAwait(false);
        }
        // Fallback to service resolution when no HttpContext
        var svcProvider = host.App?.Services;
        if (svcProvider == null)
        {
            host.Logger.Warning("No service provider available to resolve IRealtimeBroadcaster.");
            return false;
        }
        if (svcProvider.GetService(typeof(IRealtimeBroadcaster)) is not IRealtimeBroadcaster broadcaster)
        {
            host.Logger.Warning("IRealtimeBroadcaster service is not registered. Make sure SignalR is configured with KestrunHub.");
            return false;
        }
        try
        {
            await broadcaster.BroadcastLogAsync(level, message, cancellationToken).ConfigureAwait(false);
            host.Logger.Debug("Broadcasted log message via SignalR: {Level} - {Message}", level, message);
            return true;
        }
        catch (Exception ex)
        {
            host.Logger.Error(ex, "Failed to broadcast log message: {Level} - {Message}", level, message);
            return false;
        }
    }

    /// <summary>
    /// Broadcasts an event to all connected SignalR clients using the best available service provider.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="eventName">The name of the event to broadcast.</param>
    /// <param name="data">Optional data to include with the event.</param>
    /// <param name="httpContext">Optional: The current HttpContext, if available.</param>
    /// <param name="cancellationToken">Optional: Cancellation token.</param>
    public static async Task<bool> BroadcastEventAsync(this KestrunHost host, string eventName, object? data = null, HttpContext? httpContext = null, CancellationToken cancellationToken = default)
    {
        if (httpContext != null)
        {
            var ctx = new Models.KestrunContext(host, httpContext);
            return await ctx.BroadcastEventAsync(eventName, data, cancellationToken).ConfigureAwait(false);
        }
        var svcProvider = host.App?.Services;
        if (svcProvider == null)
        {
            host.Logger.Warning("No service provider available to resolve IRealtimeBroadcaster.");
            return false;
        }
        if (svcProvider.GetService(typeof(IRealtimeBroadcaster)) is not IRealtimeBroadcaster broadcaster)
        {
            host.Logger.Warning("IRealtimeBroadcaster service is not registered. Make sure SignalR is configured with KestrunHub.");
            return false;
        }
        try
        {
            await broadcaster.BroadcastEventAsync(eventName, data, cancellationToken).ConfigureAwait(false);
            host.Logger.Debug("Broadcasted event via SignalR: {EventName}", eventName);
            return true;
        }
        catch (Exception ex)
        {
            host.Logger.Error(ex, "Failed to broadcast event: {EventName}", eventName);
            return false;
        }
    }

    /// <summary>
    /// Broadcasts a message to a specific SignalR group using the best available service provider.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="groupName">The name of the group to broadcast to.</param>
    /// <param name="method">The hub method name to invoke on clients.</param>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="httpContext">Optional: The current HttpContext, if available.</param>
    /// <param name="cancellationToken">Optional: Cancellation token.</param>
    public static async Task<bool> BroadcastToGroupAsync(this KestrunHost host, string groupName, string method, object? message, HttpContext? httpContext = null, CancellationToken cancellationToken = default)
    {
        if (httpContext != null)
        {
            var ctx = new Models.KestrunContext(host, httpContext);
            return await ctx.BroadcastToGroupAsync(groupName, method, message, cancellationToken).ConfigureAwait(false);
        }
        var svcProvider = host.App?.Services;
        if (svcProvider == null)
        {
            host.Logger.Warning("No service provider available to resolve IRealtimeBroadcaster.");
            return false;
        }
        if (svcProvider.GetService(typeof(IRealtimeBroadcaster)) is not IRealtimeBroadcaster broadcaster)
        {
            host.Logger.Warning("IRealtimeBroadcaster service is not registered. Make sure SignalR is configured with KestrunHub.");
            return false;
        }
        try
        {
            await broadcaster.BroadcastToGroupAsync(groupName, method, message, cancellationToken).ConfigureAwait(false);
            host.Logger.Debug("Broadcasted to group {GroupName} via method {Method}", groupName, method);
            return true;
        }
        catch (Exception ex)
        {
            host.Logger.Error(ex, "Failed to broadcast to group {GroupName}", groupName);
            return false;
        }
    }
}
