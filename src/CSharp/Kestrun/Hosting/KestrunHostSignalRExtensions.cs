using System;
using System.Threading;
using System.Threading.Tasks;
using Kestrun.Hosting;
using Kestrun.SignalR;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Kestrun.Hosting
{
    public static class KestrunHostSignalRExtensions
    {
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
            IServiceProvider? svcProvider = null;
            if (httpContext != null)
            {
                svcProvider = httpContext.RequestServices;
            }
            else if (host.App != null)
            {
                svcProvider = host.App.Services;
            }
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
                await broadcaster.BroadcastLogAsync(level, message, cancellationToken);
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
        /// Synchronous wrapper for BroadcastLogAsync.
        /// </summary>
        /// <param name="host">The KestrunHost instance.</param>
        /// <param name="level">The log level (e.g., Information, Warning, Error, Debug, Verbose).</param>
        /// <param name="message">The log message to broadcast.</param>
        /// <param name="httpContext">Optional: The current HttpContext, if available.</param>
        /// <param name="cancellationToken">Optional: Cancellation token.</param>
        /// <returns>True if the log was broadcast successfully; otherwise, false.</returns>
        public static bool BroadcastLog(this KestrunHost host, string level, string message, HttpContext? httpContext = null, CancellationToken cancellationToken = default) =>
            BroadcastLogAsync(host, level, message, httpContext, cancellationToken).GetAwaiter().GetResult();
    }
}
