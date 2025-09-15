using System.Text;
using Kestrun.Hosting;
using Serilog;

namespace Kestrun.Utilities;

/// <summary>
/// Provides extension methods for hosting Kestrun servers.
/// </summary>
public static class HostingExtensions
{
    /// <summary>
    /// Starts <paramref name="server"/>, blocks until the process receives
    /// Ctrl-C / SIGTERM (or <paramref name="stopToken"/> is cancelled),
    /// then calls <c>StopAsync</c> and disposes the host.
    /// </summary>
    /// <remarks>
    /// <para>Intended for console apps’ <c>Main</c>; keeps boiler-plate out
    /// of every sample you write.</para>
    /// <para>If <paramref name="configureConsole"/> is <see langword="true"/>
    /// (default) the method hooks <see cref="Console.CancelKeyPress"/>.
    /// In a service/daemon scenario you can pass <see langword="false"/>
    /// and supply your own cancellation token.</para>
    /// </remarks>
    public static async Task RunUntilShutdownAsync(
        this KestrunHost server,
        bool configureConsole = true,
        Encoding? consoleEncoding = null,
        Action? onStarted = null,
        Action<Exception>? onShutdownError = null,
        CancellationToken stopToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        if (consoleEncoding != null)
        {
            Console.OutputEncoding = consoleEncoding;
            Console.InputEncoding = consoleEncoding;
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
        var done = new TaskCompletionSource<object?>(
                       TaskCreationOptions.RunContinuationsAsynchronously);

        // ① optional Ctrl-C handler
        if (configureConsole)
        {
            Console.CancelKeyPress += Handler;
        }

        // ② start + user callback
        await server.StartAsync(linked.Token);
        onStarted?.Invoke();
        Log.Information("Kestrun server started. Press Ctrl+C to stop.");

        // ③ wait for either Ctrl-C or external cancellation
        using (stopToken.Register(() => done.TrySetResult(null)))
        {
            _ = await done.Task;
        }

        // ④ graceful shutdown
        try
        {
            await server.StopAsync();
        }
        catch (Exception ex)
        {
            onShutdownError?.Invoke(ex);
            Log.Debug(ex, "Ignored exception during server shutdown.");
        }
        finally
        {
            server.Dispose();
            if (configureConsole)
            {
                Console.CancelKeyPress -= Handler;
            }
        }

        // local function so we can unregister it
        void Handler(object? _, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;          // prevent immediate process kill
            linked.Cancel();          // wake up the await
            _ = done.TrySetResult(null);
        }
    }
}
