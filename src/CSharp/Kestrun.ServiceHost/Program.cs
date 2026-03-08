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
        string? ServiceLogPath,
        bool DirectRunMode,
        bool DiscoverPowerShellHome);

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
        var directRunMode = false;
        var discoverPowerShellHome = false;

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

            if (current is "--run" && index + 1 < args.Length)
            {
                scriptPath = args[index + 1];
                directRunMode = true;
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

            if (current is "--discover-pshome")
            {
                discoverPowerShellHome = true;
                index += 1;
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

        if (string.IsNullOrWhiteSpace(serviceName) && !string.IsNullOrWhiteSpace(scriptPath))
        {
            serviceName = BuildDefaultServiceNameFromScriptPath(scriptPath, directRunMode);
        }

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            error = "Missing --name.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(runnerExecutablePath))
        {
            runnerExecutablePath = ResolveCurrentExecutablePath();
        }

        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            error = "Missing --script or --run.";
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
            serviceLogPath,
            directRunMode,
            discoverPowerShellHome);
        return true;
    }

    private static void PrintUsage() => Console.WriteLine("Usage: kestrun-service-host [--name <service>] [--runner-exe <path>] (--script <path> | --run <path>) --kestrun-manifest <path> [--service-log-path <path>] [--discover-pshome] [--arguments ...]");

    /// <summary>
    /// Resolves the path of the current executable for diagnostic and compatibility metadata.
    /// </summary>
    /// <returns>Absolute executable path when available; otherwise a fallback token.</returns>
    private static string ResolveCurrentExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return Path.GetFullPath(Environment.ProcessPath);
        }
        // Fall back to a best-guess based on the current executable name and platform conventions.
        return OperatingSystem.IsWindows()
            ? Path.Combine(AppContext.BaseDirectory, "kestrun-service-host.exe")
            : Path.Combine(AppContext.BaseDirectory, "kestrun-service-host");
    }

    /// <summary>
    /// Builds the default service name when callers omit <c>--name</c>.
    /// </summary>
    /// <param name="scriptPath">Script path provided by the caller.</param>
    /// <param name="directRunMode">True when running in direct script mode.</param>
    /// <returns>Service name default derived from script path.</returns>
    private static string BuildDefaultServiceNameFromScriptPath(string scriptPath, bool directRunMode)
    {
        var stem = Path.GetFileNameWithoutExtension(scriptPath);
        if (string.IsNullOrWhiteSpace(stem))
        {
            return directRunMode ? "kestrun-direct" : "kestrun-service";
        }
        // Sanitize the stem to ensure it's a valid filename segment, since it will be used in the default log file name.
        return directRunMode
            ? $"kestrun-direct-{stem}"
            : stem;
    }

    private static int RunForegroundDaemon(ParsedOptions options)
    {
        var logPath = ResolveBootstrapLogPath(options.ServiceLogPath, options.ServiceName);
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
            _host = new ScriptExecutionHost(options, ResolveBootstrapLogPath(options.ServiceLogPath, options.ServiceName));
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
            WriteBootstrapLog($"Initialized script host for service '{_options.ServiceName}' (directRun={_options.DirectRunMode}, log='{_bootstrapLogPath}').");
        }

        public bool HasExited => _executionTask?.IsCompleted == true;

        public int ExitCode => _exitCode ?? 0;

        public int Start()
        {
            if (_executionTask is not null)
            {
                WriteBootstrapLog("Start requested while execution task is already running.");
                return 0;
            }

            WriteBootstrapLog(
                $"Validating startup inputs. script='{_options.ScriptPath}', manifest='{_options.ModuleManifestPath}', runner='{_options.RunnerExecutablePath}', args=[{FormatScriptArguments(_options.ScriptArguments)}]");

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
                WriteBootstrapLog("Starting script execution task.");
                _executionTask = Task.Run(() => ExecuteScript(
                    _options.ScriptPath,
                    _options.ScriptArguments,
                    _options.ModuleManifestPath,
                    _options.DiscoverPowerShellHome,
                    WriteBootstrapLog,
                    _shutdown.Token));

                _ = _executionTask.ContinueWith(task =>
                {
                    var code = task.IsFaulted ? 1 : task.Result;
                    if (task.IsFaulted)
                    {
                        WriteBootstrapLog($"Script execution failed: {task.Exception?.GetBaseException()}");
                    }
                    else
                    {
                        WriteBootstrapLog($"Script execution task completed with exit code {code}.");
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
                WriteBootstrapLog("Stop requested: cancelling execution task and attempting managed host shutdown.");
                _shutdown.Cancel();

                if (!RequestManagedStopAsync().Wait(5000))
                {
                    WriteBootstrapLog("Managed host stop timed out after 5000ms.");
                }
                else
                {
                    WriteBootstrapLog("Managed host stop completed.");
                }

                if (_executionTask is not null)
                {
                    if (!_executionTask.Wait(15000))
                    {
                        WriteBootstrapLog("Execution task did not complete within 15000ms after stop request.");
                    }
                    else
                    {
                        WriteBootstrapLog("Execution task completed after stop request.");
                    }
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
        bool discoverPowerShellHome,
        Action<string> log,
        CancellationToken stopToken)
    {
        log($"Preparing script execution. script='{scriptPath}', manifest='{moduleManifestPath}', args=[{FormatScriptArguments(scriptArguments)}]");
        EnsureNet10Runtime();
        log("Verified .NET 10 runtime.");
        ConfigurePowerShellHome(discoverPowerShellHome, moduleManifestPath, log);
        EnsurePowerShellRuntimeHome();
        var psHome = Environment.GetEnvironmentVariable("PSHOME");
        var psModulePath = Environment.GetEnvironmentVariable("PSModulePath");
        log($"PowerShell runtime home prepared. PSHOME='{(string.IsNullOrWhiteSpace(psHome) ? "<null>" : psHome)}', PSModulePath='{(string.IsNullOrWhiteSpace(psModulePath) ? "<null>" : psModulePath)}'.");
        EnsureKestrunAssemblyPreloaded(moduleManifestPath);
        log("Kestrun assembly preload completed.");

        var sessionState = InitialSessionState.CreateDefault2();
        if (OperatingSystem.IsWindows())
        {
            sessionState.ExecutionPolicy = ExecutionPolicy.Unrestricted;
        }

        sessionState.ImportPSModule([moduleManifestPath]);
        log($"Imported module manifest '{moduleManifestPath}'.");

        using var runspace = RunspaceFactory.CreateRunspace(sessionState);
        runspace.Open();
        log("Runspace opened.");

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
        log("PowerShell invocation configured. Starting asynchronous execution.");

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
        log($"Script invocation completed. HadErrors={powershell.HadErrors}.");

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
    /// Configures <c>PSHOME</c> for service-host script execution.
    /// </summary>
    /// <param name="discoverPowerShellHome">When true, does not set <c>PSHOME</c> and lets runtime discovery resolve it.</param>
    /// <param name="moduleManifestPath">Absolute path to Kestrun.psd1.</param>
    /// <param name="log">Best-effort service-host logger.</param>
    private static void ConfigurePowerShellHome(bool discoverPowerShellHome, string moduleManifestPath, Action<string> log)
    {
        if (discoverPowerShellHome)
        {
            log("PSHOME discovery mode enabled; skipping PSHOME override.");
            return;
        }

        var serviceRoot = ResolveServiceRootFromManifestPath(moduleManifestPath);
        Environment.SetEnvironmentVariable("PSHOME", serviceRoot);
        log($"PSHOME set to service root '{serviceRoot}'.");
    }

    /// <summary>
    /// Resolves the service root path from the staged Kestrun module manifest.
    /// </summary>
    /// <param name="moduleManifestPath">Absolute path to Kestrun.psd1 under <c>Modules/Kestrun</c>.</param>
    /// <returns>Absolute service root path.</returns>
    private static string ResolveServiceRootFromManifestPath(string moduleManifestPath)
    {
        var manifestDirectory = Path.GetDirectoryName(moduleManifestPath);
        if (string.IsNullOrWhiteSpace(manifestDirectory))
        {
            return AppContext.BaseDirectory;
        }

        var moduleRoot = Directory.GetParent(manifestDirectory);
        var serviceRoot = moduleRoot?.Parent;
        return serviceRoot?.FullName ?? AppContext.BaseDirectory;
    }

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

    private static string ResolveBootstrapLogPath(string? configuredPath, string serviceName)
        => RunnerRuntime.ResolveBootstrapLogPath(configuredPath, BuildDefaultServiceLogFileName(serviceName));

    /// <summary>
    /// Builds a default service log file name using the configured service name.
    /// </summary>
    /// <param name="serviceName">Configured service name.</param>
    /// <returns>Service-specific log file name.</returns>
    private static string BuildDefaultServiceLogFileName(string serviceName)
        => $"kestrun-tool-service-{SanitizeFileNameSegment(serviceName)}.log";

    /// <summary>
    /// Converts arbitrary service names to a filesystem-safe filename segment.
    /// </summary>
    /// <param name="value">Raw value to sanitize.</param>
    /// <returns>Safe filename segment.</returns>
    private static string SanitizeFileNameSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "default";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string([.. value.Select(c => invalidChars.Contains(c) ? '-' : c)])
            .Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "default" : sanitized;
    }

    /// <summary>
    /// Formats script arguments for diagnostic logging.
    /// </summary>
    /// <param name="scriptArguments">Script argument values.</param>
    /// <returns>Comma-separated argument list with shell-safe quoting.</returns>
    private static string FormatScriptArguments(IReadOnlyList<string> scriptArguments)
        => scriptArguments.Count == 0
            ? ""
            : string.Join(", ",
                scriptArguments.Select(static arg =>
                    string.IsNullOrEmpty(arg)
                        ? "\"\""
                        : arg.Contains(' ') ? $"\"{arg}\"" : arg));
}
