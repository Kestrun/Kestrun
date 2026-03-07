using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Kestrun.Runner;
using Microsoft.PowerShell;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Text;

namespace Kestrun.ServiceHost;

internal static class Program
{
    private sealed record ParsedOptions(
        string ServiceName,
        string RunnerExecutablePath,
        string ScriptPath,
        string ModuleManifestPath,
        string[] ScriptArguments,
        string? ServiceLogPath);

    private static int Main(string[] args)
    {
        if (!TryParseArguments(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 2;
        }
        // If running on Windows and not in an interactive session, run as a Windows Service.
        if (OperatingSystem.IsWindows() && !Environment.UserInteractive)
        {
            return RunWindowsService(options!);
        }
        // For non-Windows or interactive sessions, run as a foreground daemon.
        return RunForegroundDaemon(options!);
    }

    [SupportedOSPlatform("windows")]
    private static int RunWindowsService(ParsedOptions options)
    {
        ServiceBase.Run(new KestrunWindowsService(options));
        return 0;
    }

    private static bool TryParseArguments(string[] args, out ParsedOptions? options, out string error)
    {
        options = null;
        error = string.Empty;

        var serviceName = string.Empty;
        var runnerExecutablePath = string.Empty;
        var scriptPath = string.Empty;
        var moduleManifestPath = string.Empty;
        var scriptArguments = Array.Empty<string>();
        string? serviceLogPath = null;

        var index = 0;
        while (index < args.Length)
        {
            var current = args[index];
            if (current is "--name" && index + 1 < args.Length)
            {
                serviceName = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--runner-exe" && index + 1 < args.Length)
            {
                runnerExecutablePath = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--script" && index + 1 < args.Length)
            {
                scriptPath = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--kestrun-manifest" && index + 1 < args.Length)
            {
                moduleManifestPath = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--service-log-path" && index + 1 < args.Length)
            {
                serviceLogPath = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--arguments" or "--")
            {
                scriptArguments = [.. args.Skip(index + 1)];
                break;
            }

            error = $"Unknown option: {current}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            error = "Missing --name.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(runnerExecutablePath))
        {
            error = "Missing --runner-exe.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            error = "Missing --script.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(moduleManifestPath))
        {
            error = "Missing --kestrun-manifest.";
            return false;
        }

        options = new ParsedOptions(
            serviceName,
            Path.GetFullPath(runnerExecutablePath),
            Path.GetFullPath(scriptPath),
            Path.GetFullPath(moduleManifestPath),
            scriptArguments,
            serviceLogPath);
        return true;
    }

    private static void PrintUsage() => Console.WriteLine("Usage: kestrun-service-host --name <service> --runner-exe <path> --script <path> --kestrun-manifest <path> [--service-log-path <path>] [--arguments ...]");

    private static int RunForegroundDaemon(ParsedOptions options)
    {
        var logPath = ResolveBootstrapLogPath(options.ServiceLogPath);
        var host = new ScriptExecutionHost(options, logPath);

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (!shutdown.IsCancellationRequested)
            {
                shutdown.Cancel();
            }
        };

        host.WriteBootstrapLog($"Daemon '{options.ServiceName}' starting.");

        var startCode = host.Start();
        if (startCode != 0)
        {
            return startCode;
        }

        while (!shutdown.IsCancellationRequested)
        {
            if (host.HasExited)
            {
                var exitCode = host.ExitCode;
                host.WriteBootstrapLog($"Runner process exited with code {exitCode}.");
                return exitCode;
            }

            Thread.Sleep(250);
        }

        host.WriteBootstrapLog($"Daemon '{options.ServiceName}' stopping.");
        host.Stop();
        host.WriteBootstrapLog($"Daemon '{options.ServiceName}' stopped.");
        return 0;
    }

    [SupportedOSPlatform("windows")]
    private sealed class KestrunWindowsService : ServiceBase
    {
        private readonly ScriptExecutionHost _host;

        public KestrunWindowsService(ParsedOptions options)
        {
            ServiceName = options.ServiceName;
            CanStop = true;
            AutoLog = true;
            _host = new ScriptExecutionHost(options, ResolveBootstrapLogPath(options.ServiceLogPath));
        }

        protected override void OnStart(string[] args)
        {
            _host.WriteBootstrapLog($"Service '{ServiceName}' starting.");
            var exitCode = _host.Start();
            if (exitCode != 0)
            {
                ExitCode = exitCode;
                Stop();
                return;
            }

            _host.RegisterOnExit(code =>
            {
                _host.WriteBootstrapLog($"Runner process exited with code {code}.");
                ExitCode = code;
                Stop();
            });
        }

        protected override void OnStop()
        {
            _host.WriteBootstrapLog($"Service '{ServiceName}' stopping.");
            _host.Stop();
            _host.WriteBootstrapLog($"Service '{ServiceName}' stopped.");
        }
    }

    private sealed class ScriptExecutionHost : IDisposable
    {
        private readonly ParsedOptions _options;
        private readonly string _bootstrapLogDirectory;
        private readonly string _bootstrapLogPath;
        private readonly Lock _sync = new();
        private readonly CancellationTokenSource _shutdown = new();
        private Action<int>? _onExit;
        private Task<int>? _executionTask;
        private int? _exitCode;

        public ScriptExecutionHost(ParsedOptions options, string bootstrapLogPath)
        {
            _options = options;
            _bootstrapLogPath = bootstrapLogPath;
            _bootstrapLogDirectory = Path.GetDirectoryName(_bootstrapLogPath) ?? Path.GetTempPath();
        }

        public bool HasExited => _executionTask?.IsCompleted == true;

        public int ExitCode => _exitCode ?? 0;

        public int Start()
        {
            if (_executionTask is not null)
            {
                return 0;
            }

            if (!File.Exists(_options.ScriptPath))
            {
                WriteBootstrapLog($"Script file not found: {_options.ScriptPath}");
                return 2;
            }

            if (!File.Exists(_options.ModuleManifestPath))
            {
                WriteBootstrapLog($"Kestrun manifest file not found: {_options.ModuleManifestPath}");
                return 2;
            }

            try
            {
                _executionTask = Task.Run(() => ExecuteScript(
                    _options.ScriptPath,
                    _options.ScriptArguments,
                    _options.ModuleManifestPath,
                    WriteBootstrapLog,
                    _shutdown.Token));

                _ = _executionTask.ContinueWith(task =>
                {
                    var code = task.IsFaulted ? 1 : task.Result;
                    if (task.IsFaulted)
                    {
                        WriteBootstrapLog($"Script execution failed: {task.Exception?.GetBaseException()}");
                    }

                    Action<int>? callback;
                    lock (_sync)
                    {
                        _exitCode = code;
                        callback = _onExit;
                    }

                    callback?.Invoke(code);
                }, TaskScheduler.Default);

                return 0;
            }
            catch (Exception ex)
            {
                WriteBootstrapLog($"Failed to start script execution: {ex}");
                return 1;
            }
        }

        public void RegisterOnExit(Action<int> onExit)
        {
            lock (_sync)
            {
                _onExit = onExit;
                if (_exitCode.HasValue)
                {
                    onExit(_exitCode.Value);
                }
            }
        }

        public void Stop()
        {
            try
            {
                _shutdown.Cancel();
                _ = RequestManagedStopAsync().Wait(5000);

                if (_executionTask is not null)
                {
                    _ = _executionTask.Wait(15000);
                }
            }
            catch (InvalidOperationException ex)
            {
                WriteBootstrapLog($"Failed to stop script execution: {ex.Message}");
            }
            catch (AggregateException ex)
            {
                WriteBootstrapLog($"Failed to stop script execution: {ex.GetBaseException().Message}");
            }
        }

        public void WriteBootstrapLog(string message)
        {
            try
            {
                _ = Directory.CreateDirectory(_bootstrapLogDirectory);
                var line = $"{DateTime.UtcNow:O} {message}{Environment.NewLine}";
                File.AppendAllText(_bootstrapLogPath, line, Encoding.UTF8);
            }
            catch
            {
                // Best-effort logging only.
            }
        }

        public void Dispose() => _shutdown.Dispose();
    }

    /// <summary>
    /// Executes the target script in a runspace that has Kestrun imported by manifest path.
    /// </summary>
    /// <param name="scriptPath">Absolute path to the script to execute.</param>
    /// <param name="scriptArguments">Command-line arguments passed to the target script.</param>
    /// <param name="moduleManifestPath">Absolute path to Kestrun.psd1.</param>
    /// <param name="log">Best-effort service-host logger.</param>
    /// <param name="stopToken">Cancellation token signaled during service shutdown.</param>
    /// <returns>Process exit code.</returns>
    private static int ExecuteScript(
        string scriptPath,
        IReadOnlyList<string> scriptArguments,
        string moduleManifestPath,
        Action<string> log,
        CancellationToken stopToken)
    {
        EnsureNet10Runtime();
        EnsurePowerShellRuntimeHome();
        EnsureKestrunAssemblyPreloaded(moduleManifestPath);

        var sessionState = InitialSessionState.CreateDefault2();
        if (OperatingSystem.IsWindows())
        {
            sessionState.ExecutionPolicy = ExecutionPolicy.Unrestricted;
        }

        sessionState.ImportPSModule([moduleManifestPath]);

        using var runspace = RunspaceFactory.CreateRunspace(sessionState);
        runspace.Open();

        if (!HasKestrunHostManagerType())
        {
            throw new RuntimeException("Failed to import Kestrun module: type Kestrun.KestrunHostManager was not loaded.");
        }

        runspace.SessionStateProxy.SetVariable("__krRunnerScriptPath", scriptPath);
        runspace.SessionStateProxy.SetVariable("__krRunnerScriptArgs", scriptArguments.ToArray());
        runspace.SessionStateProxy.SetVariable("__krRunnerQuiet", true);
        runspace.SessionStateProxy.SetVariable("__krRunnerManagedConsole", true);

        using var powershell = PowerShell.Create();
        powershell.Runspace = runspace;
        // Dot-source the script into the current scope so function metadata used by OpenAPI discovery remains visible.
        _ = powershell.AddScript(". $__krRunnerScriptPath @__krRunnerScriptArgs", useLocalScope: false);

        IEnumerable<PSObject> output;
        var asyncResult = powershell.BeginInvoke();
        var stopRequested = false;

        while (!asyncResult.IsCompleted)
        {
            _ = asyncResult.AsyncWaitHandle.WaitOne(200);
            if (!stopToken.IsCancellationRequested || stopRequested)
            {
                continue;
            }

            stopRequested = true;
            log("Stop requested. Stopping Kestrun server...");
            _ = Task.Run(RequestManagedStopAsync);
        }

        output = powershell.EndInvoke(asyncResult);

        WriteOutput(output, log);
        WriteStreams(powershell.Streams, log);

        return powershell.HadErrors ? 1 : 0;
    }

    /// <summary>
    /// Ensures the runner is executing on .NET 10.
    /// </summary>
    private static void EnsureNet10Runtime()
        => RunnerRuntime.EnsureNet10Runtime("kestrun-service-host");

    /// <summary>
    /// Ensures Kestrun.dll from the selected module root is loaded into the default context.
    /// </summary>
    /// <param name="moduleManifestPath">Absolute path to Kestrun.psd1.</param>
    private static void EnsureKestrunAssemblyPreloaded(string moduleManifestPath)
        => RunnerRuntime.EnsureKestrunAssemblyPreloaded(moduleManifestPath);

    /// <summary>
    /// Ensures PowerShell built-in modules are discoverable for embedded runspace execution.
    /// </summary>
    private static void EnsurePowerShellRuntimeHome()
        => RunnerRuntime.EnsurePowerShellRuntimeHome(createFallbackDirectories: false);

    /// <summary>
    /// Verifies that the loaded Kestrun assembly contains the expected host manager type.
    /// </summary>
    /// <returns>True when the expected Kestrun host manager type is available.</returns>
    private static bool HasKestrunHostManagerType()
        => RunnerRuntime.HasKestrunHostManagerType();

    /// <summary>
    /// Requests a graceful stop for all running Kestrun hosts managed in the current process.
    /// </summary>
    /// <returns>A task representing the stop attempt.</returns>
    private static Task RequestManagedStopAsync()
        => RunnerRuntime.RequestManagedStopAsync();

    /// <summary>
    /// Writes PowerShell pipeline output to stdout and service log.
    /// </summary>
    /// <param name="output">Pipeline output collection.</param>
    /// <param name="log">Best-effort service-host logger.</param>
    private static void WriteOutput(IEnumerable<PSObject> output, Action<string> log)
        => RunnerRuntime.DispatchPowerShellOutput(
            output,
            value =>
            {
                Console.WriteLine(value);
                log($"output: {value}");
            },
            skipWhitespace: true);

    /// <summary>
    /// Writes non-output streams in a console-friendly format.
    /// </summary>
    /// <param name="streams">PowerShell data streams.</param>
    /// <param name="log">Best-effort service-host logger.</param>
    private static void WriteStreams(PSDataStreams streams, Action<string> log)
    {
        RunnerRuntime.DispatchPowerShellStreams(
            streams,
            onWarning: message =>
            {
                Console.Error.WriteLine(message);
                log($"warning: {message}");
            },
            onVerbose: message =>
            {
                Console.WriteLine(message);
                log($"verbose: {message}");
            },
            onDebug: message =>
            {
                Console.WriteLine(message);
                log($"debug: {message}");
            },
            onInformation: message =>
            {
                Console.WriteLine(message);
                log($"info: {message}");
            },
            onError: message =>
            {
                Console.Error.WriteLine(message);
                log($"error: {message}");
            },
            skipWhitespace: true);
    }

    private static string ResolveBootstrapLogPath(string? configuredPath)
        => RunnerRuntime.ResolveBootstrapLogPath(configuredPath, "kestrun-tool-service.log");
}
