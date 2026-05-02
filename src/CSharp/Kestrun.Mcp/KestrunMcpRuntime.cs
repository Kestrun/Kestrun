using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Kestrun.Hosting;
using Kestrun.Runner;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kestrun.Mcp.ServerHost;

/// <summary>
/// Holds the live Kestrun host for MCP tool access.
/// </summary>
internal sealed class KestrunMcpRuntime
{
    private readonly TaskCompletionSource<KestrunHost> _hostSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completes the runtime with a resolved Kestrun host.
    /// </summary>
    /// <param name="host">The resolved host.</param>
    public void SetHost(KestrunHost host) => _hostSource.TrySetResult(host);

    /// <summary>
    /// Completes the runtime with an error.
    /// </summary>
    /// <param name="exception">The startup exception.</param>
    public void SetException(Exception exception) => _hostSource.TrySetException(exception);

    /// <summary>
    /// Waits for the Kestrun host to become available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved host.</returns>
    public Task<KestrunHost> WaitForHostAsync(CancellationToken cancellationToken)
        => _hostSource.Task.WaitAsync(cancellationToken);
}

/// <summary>
/// Executes the target Kestrun script in-process and publishes the resulting host.
/// </summary>
internal sealed class KestrunScriptSessionHostedService(
    KestrunMcpCommandLine options,
    KestrunMcpRuntime runtime,
    ILogger<KestrunScriptSessionHostedService> logger,
    IHostApplicationLifetime appLifetime) : IHostedService
{
    private Task? _executionTask;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _executionTask = Task.Run(() => ExecuteAsync(appLifetime.ApplicationStopping), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await RunnerRuntime.RequestManagedStopAsync().ConfigureAwait(false);
        if (_executionTask is not null)
        {
            await _executionTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes the configured script and waits for the host to become available.
    /// </summary>
    /// <param name="stopToken">Application stop token.</param>
    /// <returns>A task that completes when the script exits.</returns>
    private async Task ExecuteAsync(CancellationToken stopToken)
    {
        try
        {
            RunnerRuntime.EnsureNet10Runtime("Kestrun.Mcp");
            ConfigurePowerShellHome();
            RunnerRuntime.EnsurePowerShellRuntimeHome(createFallbackDirectories: true);
            RunnerRuntime.EnsureKestrunAssemblyPreloaded(options.ModuleManifestPath, message => logger.LogWarning("{Message}", message));

            using var runspace = CreateRunspace();
            using var powershell = PowerShell.Create();
            powershell.Runspace = runspace;
            runspace.SessionStateProxy.SetVariable("__krRunnerScriptPath", options.ScriptPath);
            runspace.SessionStateProxy.SetVariable("__krRunnerManagedConsole", true);
            runspace.SessionStateProxy.SetVariable("__krRunnerQuiet", true);
            _ = powershell.AddScript(". $__krRunnerScriptPath", useLocalScope: false);

            logger.LogInformation("Starting Kestrun script '{ScriptPath}'.", options.ScriptPath);
            var asyncResult = powershell.BeginInvoke();

            while (!asyncResult.IsCompleted)
            {
                ResolveHostIfAvailable();
                _ = asyncResult.AsyncWaitHandle.WaitOne(200);
                if (stopToken.IsCancellationRequested)
                {
                    await RunnerRuntime.RequestManagedStopAsync().ConfigureAwait(false);
                }
            }

            _ = powershell.EndInvoke(asyncResult);
            ResolveHostIfAvailable();

            if (powershell.HadErrors)
            {
                var errorMessage = string.Join(Environment.NewLine, powershell.Streams.Error.Select(static error => error.ToString()));
                throw new InvalidOperationException($"Kestrun script completed with errors:{Environment.NewLine}{errorMessage}");
            }

            EnsureHostResolved();
        }
        catch (Exception ex)
        {
            runtime.SetException(ex);
            logger.LogError(ex, "Failed to start the Kestrun MCP runtime.");
            appLifetime.StopApplication();
        }
    }

    /// <summary>
    /// Creates the runspace used for script execution.
    /// </summary>
    /// <returns>An opened runspace.</returns>
    private Runspace CreateRunspace()
    {
        var sessionState = InitialSessionState.CreateDefault2();
        sessionState.ImportPSModule([options.ModuleManifestPath]);
        var runspace = RunspaceFactory.CreateRunspace(sessionState);
        runspace.Open();
        return runspace;
    }

    /// <summary>
    /// Resolves the configured host from <see cref="KestrunHostManager"/> when available.
    /// </summary>
    private void ResolveHostIfAvailable()
    {
        var host = TryResolveHost();

        if (host is not null)
        {
            runtime.SetHost(host);
        }
    }

    /// <summary>
    /// Ensures the configured host was discovered after script execution completed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the expected host was never registered.</exception>
    private void EnsureHostResolved()
    {
        if (TryResolveHost() is not null)
        {
            return;
        }

        var hostLabel = string.IsNullOrWhiteSpace(options.HostName)
            ? "the default Kestrun host"
            : $"Kestrun host '{options.HostName}'";

        throw new InvalidOperationException(
            $"The script '{options.ScriptPath}' completed without registering {hostLabel}. " +
            "Ensure the script creates and starts the expected Kestrun host.");
    }

    /// <summary>
    /// Resolves the configured host from <see cref="KestrunHostManager"/>.
    /// </summary>
    /// <returns>The resolved host when available; otherwise, <see langword="null"/>.</returns>
    private KestrunHost? TryResolveHost()
        => string.IsNullOrWhiteSpace(options.HostName)
            ? KestrunHostManager.Default
            : KestrunHostManager.Get(options.HostName);

    /// <summary>
    /// Configures PSHOME for embedded execution.
    /// </summary>
    private void ConfigurePowerShellHome()
    {
        if (options.DiscoverPowerShellHome)
        {
            logger.LogInformation("PSHOME discovery mode enabled.");
            return;
        }

        var manifestDirectory = Path.GetDirectoryName(options.ModuleManifestPath);
        var moduleRoot = manifestDirectory is null ? null : Directory.GetParent(manifestDirectory);
        var serviceRoot = moduleRoot?.Parent?.FullName ?? AppContext.BaseDirectory;
        Environment.SetEnvironmentVariable("PSHOME", serviceRoot);
    }
}
