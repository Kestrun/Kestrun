namespace Kestrun.Hosting;

/// <summary>
/// Minimal <see cref="IHostLifetime"/> that performs no blocking operations.
/// Useful for scenarios like testing where the host lifecycle is externally managed.
/// </summary>
public class NoopHostLifetime : IHostLifetime
{
    /// <summary>
    /// Waits for the host to start. This implementation does nothing and completes immediately.
    /// </summary>
    /// <param name="cancellationToken">A token to signal cancellation.</param>
    /// <returns>A completed task.</returns>
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    /// <summary>
    /// Stops the host. This implementation does nothing and completes immediately.
    /// </summary>
    /// <param name="cancellationToken">A token to signal cancellation.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
