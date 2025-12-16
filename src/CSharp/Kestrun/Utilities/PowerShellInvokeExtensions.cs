using System.Management.Automation;
namespace Kestrun.Utilities;

/// <summary>
/// Utilities for invoking PowerShell with cancellation support.
/// </summary>
internal static class PowerShellInvokeExtensions
{
    /// <summary>
    /// Invokes a PowerShell instance asynchronously, supporting cancellation via a CancellationToken.
    /// </summary>
    /// <param name="ps">The PowerShell instance to invoke.</param>
    /// <param name="requestAborted">The CancellationToken to observe for cancellation.</param>
    /// <param name="onAbortLog">Optional action to log when an abort is requested.</param>
    /// <returns>A task representing the asynchronous operation, with the PowerShell results.</returns>
    public static async Task<PSDataCollection<PSObject>> InvokeWithRequestAbortAsync(
        this PowerShell ps,
        CancellationToken requestAborted,
        Action? onAbortLog = null)
    {
        requestAborted.ThrowIfCancellationRequested();

        // If the request aborts, stop the PS pipeline.
        using var reg = requestAborted.Register(() =>
        {
            try
            {
                onAbortLog?.Invoke();

                // Stop is the canonical cancellation mechanism for hosted PS.
                // Safe to call even if invocation hasn't started yet.
                ps.Stop();
            }
            catch
            {
                // Intentionally swallow: abort paths must be "best effort"
            }
        });

        try
        {
            // Your current style
            return await ps.InvokeAsync().ConfigureAwait(false);
        }
        catch (PipelineStoppedException) when (requestAborted.IsCancellationRequested)
        {
            // Treat as cancellation, not an error.
            throw new OperationCanceledException(requestAborted);
        }
    }
}
