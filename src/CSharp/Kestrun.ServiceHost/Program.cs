using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using Microsoft.PowerShell;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Text;

namespace Kestrun.ServiceHost;

internal static class Program
{
    private static readonly object AssemblyLoadSync = new();
    private static string? s_kestrunModuleLibPath;
    private static bool s_dependencyResolverRegistered;

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
    {
        var framework = RuntimeInformation.FrameworkDescription;
        if (!framework.Contains(".NET 10", StringComparison.OrdinalIgnoreCase))
        {
            throw new RuntimeException($"kestrun-service-host requires .NET 10 runtime. Current runtime: {framework}");
        }
    }

    /// <summary>
    /// Ensures Kestrun.dll from the selected module root is loaded into the default context.
    /// </summary>
    /// <param name="moduleManifestPath">Absolute path to Kestrun.psd1.</param>
    private static void EnsureKestrunAssemblyPreloaded(string moduleManifestPath)
    {
        var manifestDirectory = Path.GetDirectoryName(moduleManifestPath);
        if (string.IsNullOrWhiteSpace(manifestDirectory))
        {
            throw new RuntimeException($"Unable to resolve manifest directory from: {moduleManifestPath}");
        }

        var moduleLibPath = Path.Combine(manifestDirectory, "lib", "net10.0");
        var expectedAssemblyPath = Path.Combine(moduleLibPath, "Kestrun.dll");
        if (!File.Exists(expectedAssemblyPath))
        {
            throw new RuntimeException($"Kestrun assembly not found at expected path: {expectedAssemblyPath}");
        }

        var expectedFullPath = Path.GetFullPath(expectedAssemblyPath);
        lock (AssemblyLoadSync)
        {
            s_kestrunModuleLibPath = moduleLibPath;
            if (!s_dependencyResolverRegistered)
            {
                AssemblyLoadContext.Default.Resolving += ResolveKestrunModuleDependency;
                s_dependencyResolverRegistered = true;
            }

            var alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "Kestrun", StringComparison.Ordinal));

            if (alreadyLoaded is not null)
            {
                var loadedPath = string.IsNullOrWhiteSpace(alreadyLoaded.Location) ? string.Empty : Path.GetFullPath(alreadyLoaded.Location);
                if (string.Equals(loadedPath, expectedFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                throw new RuntimeException($"Kestrun assembly already loaded from unexpected path: {loadedPath}");
            }

            _ = AssemblyLoadContext.Default.LoadFromAssemblyPath(expectedFullPath);
        }
    }

    /// <summary>
    /// Resolves Kestrun module dependencies from the selected module lib folder.
    /// </summary>
    /// <param name="context">Assembly load context that requested the assembly.</param>
    /// <param name="assemblyName">Requested assembly identity.</param>
    /// <returns>Loaded assembly when available; otherwise null.</returns>
    private static Assembly? ResolveKestrunModuleDependency(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName.Name))
        {
            return null;
        }

        var moduleLibPath = s_kestrunModuleLibPath;
        if (string.IsNullOrWhiteSpace(moduleLibPath))
        {
            return null;
        }

        var candidatePath = Path.Combine(moduleLibPath, $"{assemblyName.Name}.dll");
        if (!File.Exists(candidatePath))
        {
            return null;
        }

        try
        {
            return context.LoadFromAssemblyPath(Path.GetFullPath(candidatePath));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Ensures PowerShell built-in modules are discoverable for embedded runspace execution.
    /// </summary>
    private static void EnsurePowerShellRuntimeHome()
    {
        var currentPsHome = Environment.GetEnvironmentVariable("PSHOME");
        if (HasPowerShellManagementModule(currentPsHome))
        {
            EnsurePsModulePathContains(Path.Combine(currentPsHome!, "Modules"));
            return;
        }

        foreach (var candidate in EnumeratePowerShellHomeCandidates())
        {
            if (!HasPowerShellManagementModule(candidate))
            {
                continue;
            }

            Environment.SetEnvironmentVariable("PSHOME", candidate);
            EnsurePsModulePathContains(Path.Combine(candidate, "Modules"));
            return;
        }
    }

    /// <summary>
    /// Ensures a module path exists in PSModulePath.
    /// </summary>
    /// <param name="path">Path to include.</param>
    private static void EnsurePsModulePathContains(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        var modulePath = Environment.GetEnvironmentVariable("PSModulePath") ?? string.Empty;
        var entries = modulePath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var updated = string.IsNullOrWhiteSpace(modulePath)
            ? path
            : string.Join(Path.PathSeparator, new[] { path }.Concat(entries));
        Environment.SetEnvironmentVariable("PSModulePath", updated);
    }

    /// <summary>
    /// Determines whether a path contains the built-in Microsoft.PowerShell.Management module.
    /// </summary>
    /// <param name="psHome">PowerShell home candidate.</param>
    /// <returns>True when the module path exists.</returns>
    private static bool HasPowerShellManagementModule(string? psHome)
    {
        if (string.IsNullOrWhiteSpace(psHome))
        {
            return false;
        }

        var moduleDirectory = Path.Combine(psHome, "Modules", "Microsoft.PowerShell.Management");
        if (!Directory.Exists(moduleDirectory))
        {
            return false;
        }

        var manifestPath = Path.Combine(moduleDirectory, "Microsoft.PowerShell.Management.psd1");
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        try
        {
            var versionDirectories = Directory.EnumerateDirectories(moduleDirectory).ToArray();
            if (versionDirectories.Length == 0)
            {
                return true;
            }

            return versionDirectories.Any(static d => File.Exists(Path.Combine(d, "Microsoft.PowerShell.Management.psd1")));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Enumerates likely PowerShell installation roots.
    /// </summary>
    /// <returns>Distinct absolute PowerShell home candidates.</returns>
    private static IEnumerable<string> EnumeratePowerShellHomeCandidates()
    {
        var candidates = new List<string>();
        var envPsHome = Environment.GetEnvironmentVariable("PSHOME");
        if (!string.IsNullOrWhiteSpace(envPsHome))
        {
            candidates.Add(Path.GetFullPath(envPsHome));
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            candidates.Add(Path.Combine(programFiles, "PowerShell", "7"));
            candidates.Add(Path.Combine(programFiles, "PowerShell", "7-preview"));
        }

        if (OperatingSystem.IsWindows())
        {
            var whereResult = RunProcess("where.exe", ["pwsh"]);
            if (whereResult.ExitCode == 0)
            {
                var discovered = whereResult.Output
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(Path.GetDirectoryName)
                    .Where(static p => !string.IsNullOrWhiteSpace(p))
                    .Select(static p => Path.GetFullPath(p!));
                candidates.AddRange(discovered);
            }
        }
        else
        {
            candidates.Add("/usr/bin/pwsh");
            candidates.Add("/usr/local/bin/pwsh");
            candidates.Add("/opt/microsoft/powershell/7/pwsh");

            var whichResult = RunProcess("which", ["pwsh"]);
            if (whichResult.ExitCode == 0)
            {
                var discovered = whichResult.Output
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                candidates.AddRange(discovered);
            }

            candidates = candidates
                .Select(path => path.EndsWith("pwsh", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(path) ?? path : path)
                .ToList();
        }

        return candidates
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Runs a process and captures output for diagnostics.
    /// </summary>
    /// <param name="fileName">Executable to run.</param>
    /// <param name="arguments">Argument tokens.</param>
    /// <returns>Process result data.</returns>
    private static ProcessResult RunProcess(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return new ProcessResult(1, string.Empty, $"Failed to start process: {fileName}");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, output, error);
    }

    /// <summary>
    /// Captures child process execution results.
    /// </summary>
    /// <param name="ExitCode">Process exit code.</param>
    /// <param name="Output">Captured standard output.</param>
    /// <param name="Error">Captured standard error.</param>
    private sealed record ProcessResult(int ExitCode, string Output, string Error);

    /// <summary>
    /// Verifies that the loaded Kestrun assembly contains the expected host manager type.
    /// </summary>
    /// <returns>True when the expected Kestrun host manager type is available.</returns>
    private static bool HasKestrunHostManagerType()
    {
        var kestrunAssembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, "Kestrun", StringComparison.Ordinal));

        return kestrunAssembly?.GetType("Kestrun.KestrunHostManager", throwOnError: false, ignoreCase: false) is not null;
    }

    /// <summary>
    /// Requests a graceful stop for all running Kestrun hosts managed in the current process.
    /// </summary>
    /// <returns>A task representing the stop attempt.</returns>
    private static async Task RequestManagedStopAsync()
    {
        var hostManagerType = Type.GetType("Kestrun.KestrunHostManager, Kestrun", throwOnError: false, ignoreCase: false);
        if (hostManagerType is null)
        {
            return;
        }

        try
        {
            var stopAllAsyncMethod = hostManagerType.GetMethod(
                "StopAllAsync",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(CancellationToken)],
                modifiers: null);
            if (stopAllAsyncMethod is not null)
            {
                if (stopAllAsyncMethod.Invoke(null, [CancellationToken.None]) is Task stopTask)
                {
                    await stopTask.ConfigureAwait(false);
                }
            }

            var destroyAllMethod = hostManagerType.GetMethod("DestroyAll", BindingFlags.Public | BindingFlags.Static);
            _ = destroyAllMethod?.Invoke(null, null);
        }
        catch
        {
            // Best-effort shutdown: ignore reflection/host state errors.
        }
    }

    /// <summary>
    /// Writes PowerShell pipeline output to stdout and service log.
    /// </summary>
    /// <param name="output">Pipeline output collection.</param>
    /// <param name="log">Best-effort service-host logger.</param>
    private static void WriteOutput(IEnumerable<PSObject> output, Action<string> log)
    {
        foreach (var item in output)
        {
            if (item == null)
            {
                continue;
            }

            var value = item.BaseObject?.ToString() ?? item.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            Console.WriteLine(value);
            log($"output: {value}");
        }
    }

    /// <summary>
    /// Writes non-output streams in a console-friendly format.
    /// </summary>
    /// <param name="streams">PowerShell data streams.</param>
    /// <param name="log">Best-effort service-host logger.</param>
    private static void WriteStreams(PSDataStreams streams, Action<string> log)
    {
        foreach (var record in streams.Warning)
        {
            var message = record.Message;
            if (!string.IsNullOrWhiteSpace(message))
            {
                Console.Error.WriteLine(message);
                log($"warning: {message}");
            }
        }

        foreach (var record in streams.Verbose)
        {
            var message = record.Message;
            if (!string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine(message);
                log($"verbose: {message}");
            }
        }

        foreach (var record in streams.Debug)
        {
            var message = record.Message;
            if (!string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine(message);
                log($"debug: {message}");
            }
        }

        foreach (var record in streams.Information)
        {
            var message = record.MessageData?.ToString() ?? record.ToString();
            if (!string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine(message);
                log($"info: {message}");
            }
        }

        foreach (var record in streams.Error)
        {
            var message = record.ToString();
            if (!string.IsNullOrWhiteSpace(message))
            {
                Console.Error.WriteLine(message);
                log($"error: {message}");
            }
        }
    }

    private static string ResolveBootstrapLogPath(string? configuredPath)
    {
        var defaultDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Kestrun",
            "logs");
        var defaultPath = Path.Combine(defaultDirectory, "kestrun-tool-service.log");

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return defaultPath;
        }

        var fullPath = Path.GetFullPath(configuredPath);
        return Directory.Exists(fullPath)
            || configuredPath.EndsWith('\\')
            || configuredPath.EndsWith('/')
            ? Path.Combine(fullPath, "kestrun-tool-service.log")
            : fullPath;
    }
}
