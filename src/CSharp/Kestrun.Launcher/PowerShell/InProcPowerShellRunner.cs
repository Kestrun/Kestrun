using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Kestrun.Launcher.Logging;

namespace Kestrun.Launcher.PowerShell;

internal sealed class InProcPowerShellRunner
{
    private readonly SimpleLogger _logger;

    public InProcPowerShellRunner(SimpleLogger logger)
    {
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(string root, string scriptPath, string? scriptArgs, CancellationToken cancellationToken)
    {
        try
        {
            Directory.SetCurrentDirectory(root);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to set working directory to '{root}'", ex);
            return 1;
        }

        using var initialState = InitialSessionState.CreateDefault();
        using var runspace = RunspaceFactory.CreateRunspace(initialState);

        runspace.Open();
        runspace.SessionStateProxy.Path.SetLocation(root);
        runspace.SessionStateProxy.SetVariable("KrStopToken", cancellationToken);

        using var powerShell = System.Management.Automation.PowerShell.Create();
        powerShell.Runspace = runspace;

        AttachStreamLogging(powerShell);

        var invocation = BuildInvocation(scriptPath, scriptArgs);
        _logger.Info($"Starting PowerShell script: {invocation}");
        powerShell.AddScript(invocation);

        var outputBuffer = new List<PSObject>();
        IAsyncResult asyncResult;

        try
        {
            asyncResult = powerShell.BeginInvoke<PSObject, PSObject>(null, outputBuffer);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to start PowerShell invocation", ex);
            return 1;
        }

        try
        {
            while (!asyncResult.IsCompleted)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Info("Cancellation requested; stopping PowerShell.");
                    TryStop(powerShell);
                }

                await Task.Delay(200, CancellationToken.None);
            }

            powerShell.EndInvoke(asyncResult);
        }
        catch (Exception ex)
        {
            _logger.Error("Error while executing PowerShell script", ex);
            return 1;
        }

        foreach (var item in outputBuffer)
        {
            _logger.Info(item?.ToString() ?? string.Empty);
        }

        if (powerShell.Streams.Error.Count > 0)
        {
            foreach (var error in powerShell.Streams.Error)
            {
                _logger.Error(error.ToString());
            }

            return 1;
        }

        _logger.Info("PowerShell script completed.");
        return 0;
    }

    private static string BuildInvocation(string scriptPath, string? scriptArgs)
    {
        var escapedPath = scriptPath.Replace("'", "''");
        var invocation = $". '{escapedPath}'";
        if (!string.IsNullOrWhiteSpace(scriptArgs))
        {
            invocation += $" {scriptArgs}";
        }

        return invocation;
    }

    private void AttachStreamLogging(System.Management.Automation.PowerShell powerShell)
    {
        powerShell.Streams.Information.DataAdded += (_, args) =>
        {
            var record = powerShell.Streams.Information[args.Index];
            _logger.Info(record?.MessageData?.ToString() ?? record?.ToString() ?? string.Empty);
        };

        powerShell.Streams.Warning.DataAdded += (_, args) =>
        {
            var record = powerShell.Streams.Warning[args.Index];
            _logger.Warn(record?.Message ?? record?.ToString() ?? string.Empty);
        };

        powerShell.Streams.Debug.DataAdded += (_, args) =>
        {
            var record = powerShell.Streams.Debug[args.Index];
            _logger.Info(record?.Message ?? record?.ToString() ?? string.Empty);
        };

        powerShell.Streams.Verbose.DataAdded += (_, args) =>
        {
            var record = powerShell.Streams.Verbose[args.Index];
            _logger.Info(record?.Message ?? record?.ToString() ?? string.Empty);
        };
    }

    private void TryStop(System.Management.Automation.PowerShell powerShell)
    {
        try
        {
            powerShell.Stop();
        }
        catch (InvalidOperationException)
        {
            // Already stopped or stopping.
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to stop PowerShell cleanly: {ex.Message}");
        }
    }
}
