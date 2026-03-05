using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using Microsoft.PowerShell;
using System.Diagnostics;
using System.ComponentModel;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Runtime.Loader;
using System.Text;

namespace Kestrun.ScriptRunner;

internal static class Program
{
    private const string ModuleManifestFileName = "Kestrun.psd1";
    private const string ModuleName = "Kestrun";
    private const string DefaultScriptFileName = "server.ps1";
    private const string ProductName = "kestrun";
    private static readonly object AssemblyLoadSync = new();
    private static string? s_kestrunModuleLibPath;
    private static bool s_dependencyResolverRegistered;

    private enum CommandMode
    {
        Run,
        ServiceInstall,
        ServiceRemove,
        ServiceStart,
        ServiceStop,
        ServiceQuery,
    }

    private sealed record ParsedCommand(
        CommandMode Mode,
        string ScriptPath,
        string[] ScriptArguments,
        string? KestrunFolder,
        string? KestrunManifestPath,
        string? ServiceName,
        string? ServiceLogPath);

    private sealed record ServiceHostOptions(
        string ServiceName,
        string ScriptPath,
        string[] ScriptArguments,
        string? KestrunFolder,
        string? KestrunManifestPath,
        string? ServiceLogPath);

    private static int Main(string[] args)
    {
        if (TryParseServiceHostArguments(args, out var serviceHostOptions, out var serviceHostError))
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("Internal service host mode is only supported on Windows.");
                return 1;
            }

            return RunWindowsServiceHost(serviceHostOptions!);
        }

        if (!string.IsNullOrWhiteSpace(serviceHostError))
        {
            Console.Error.WriteLine(serviceHostError);
            return 2;
        }

        if (TryHandleMetaCommands(args, out var metaExitCode))
        {
            return metaExitCode;
        }

        if (!TryParseArguments(args, out var parsedCommand, out var parseError))
        {
            Console.Error.WriteLine(parseError);
            PrintUsage();
            return 2;
        }

        if (parsedCommand.Mode == CommandMode.ServiceInstall)
        {
            if (OperatingSystem.IsWindows() && !IsWindowsAdministrator())
            {
                if (!TryPreflightWindowsServiceInstall(parsedCommand, out var preflightExitCode))
                {
                    return preflightExitCode;
                }

                return RelaunchElevatedOnWindows(args);
            }

            return InstallService(parsedCommand);
        }

        if (parsedCommand.Mode == CommandMode.ServiceRemove)
        {
            if (OperatingSystem.IsWindows() && !IsWindowsAdministrator())
            {
                if (!TryPreflightWindowsServiceRemove(parsedCommand, out var preflightExitCode))
                {
                    return preflightExitCode;
                }

                return RelaunchElevatedOnWindows(args);
            }

            return RemoveService(parsedCommand);
        }

        if (parsedCommand.Mode == CommandMode.ServiceStart)
        {
            if (OperatingSystem.IsWindows() && !IsWindowsAdministrator())
            {
                if (!TryPreflightWindowsServiceControl(parsedCommand, out var preflightExitCode))
                {
                    return preflightExitCode;
                }

                return RelaunchElevatedOnWindows(args);
            }

            return StartService(parsedCommand);
        }

        if (parsedCommand.Mode == CommandMode.ServiceStop)
        {
            if (OperatingSystem.IsWindows() && !IsWindowsAdministrator())
            {
                if (!TryPreflightWindowsServiceControl(parsedCommand, out var preflightExitCode))
                {
                    return preflightExitCode;
                }

                return RelaunchElevatedOnWindows(args);
            }

            return StopService(parsedCommand);
        }

        if (parsedCommand.Mode == CommandMode.ServiceQuery)
        {
            return QueryService(parsedCommand);
        }

        var scriptPath = parsedCommand.ScriptPath;
        var scriptArguments = parsedCommand.ScriptArguments;
        var kestrunFolder = parsedCommand.KestrunFolder;
        var kestrunManifestPath = parsedCommand.KestrunManifestPath;

        var fullScriptPath = Path.GetFullPath(scriptPath);
        if (!File.Exists(fullScriptPath))
        {
            Console.Error.WriteLine($"Script file not found: {fullScriptPath}");
            return 2;
        }

        var moduleManifestPath = LocateModuleManifest(kestrunManifestPath, kestrunFolder);
        if (moduleManifestPath is null)
        {
            if (!string.IsNullOrWhiteSpace(kestrunManifestPath))
            {
                Console.Error.WriteLine($"Unable to locate manifest file: {Path.GetFullPath(kestrunManifestPath)}");
            }
            else if (!string.IsNullOrWhiteSpace(kestrunFolder))
            {
                Console.Error.WriteLine($"Unable to locate {ModuleManifestFileName} in folder: {Path.GetFullPath(kestrunFolder)}");
            }
            else
            {
                Console.Error.WriteLine($"Unable to locate {ModuleManifestFileName} under the executable folder or PSModulePath.");
            }

            return 3;
        }

        try
        {
            return ExecuteScript(fullScriptPath, scriptArguments, moduleManifestPath);
        }
        catch (RuntimeException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Execution failed: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Checks whether the current Windows process token has administrator privileges.
    /// </summary>
    /// <returns>True when running elevated as administrator.</returns>
    [SupportedOSPlatform("windows")]
    private static bool IsWindowsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Parses internal Windows service host arguments when present.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="options">Parsed service host options when successful.</param>
    /// <param name="error">Parse error when service-host mode is requested but invalid.</param>
    /// <returns>True when service-host mode is recognized and parsed.</returns>
    private static bool TryParseServiceHostArguments(string[] args, out ServiceHostOptions? options, out string? error)
    {
        options = null;
        error = null;

        if (args.Length == 0 || !string.Equals(args[0], "--service-host", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var index = 1;
        var serviceName = string.Empty;
        var scriptPath = string.Empty;
        var scriptArgs = Array.Empty<string>();
        string? kestrunFolder = null;
        string? kestrunManifestPath = null;
        string? serviceLogPath = null;

        while (index < args.Length)
        {
            var current = args[index];
            if (current is "--name" && index + 1 < args.Length)
            {
                serviceName = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--script" && index + 1 < args.Length)
            {
                scriptPath = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--kestrun-folder" or "-k" && index + 1 < args.Length)
            {
                kestrunFolder = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--kestrun-manifest" or "-m" && index + 1 < args.Length)
            {
                kestrunManifestPath = args[index + 1];
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
                scriptArgs = [.. args.Skip(index + 1)];
                break;
            }

            error = $"Unknown service host option: {current}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            error = "Missing --name for internal service host mode.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            error = "Missing --script for internal service host mode.";
            return false;
        }

        options = new ServiceHostOptions(serviceName, scriptPath, scriptArgs, kestrunFolder, kestrunManifestPath, serviceLogPath);
        return true;
    }

    /// <summary>
    /// Runs ScriptRunner under Windows Service Control Manager.
    /// </summary>
    /// <param name="options">Service host options.</param>
    /// <returns>Process exit code.</returns>
    [SupportedOSPlatform("windows")]
    private static int RunWindowsServiceHost(ServiceHostOptions options)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Internal service host mode is only supported on Windows.");
            return 1;
        }

        ServiceBase.Run(new ScriptRunnerWindowsService(options));
        return 0;
    }

    /// <summary>
    /// Windows service adapter that runs a ScriptRunner script under SCM lifecycle callbacks.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private sealed class ScriptRunnerWindowsService : ServiceBase
    {
        private readonly ServiceHostOptions _options;
        private readonly string _bootstrapLogDirectory;
        private readonly string _bootstrapLogPath;
        private Task? _runTask;

        /// <summary>
        /// Initializes the service host wrapper.
        /// </summary>
        /// <param name="options">Service host options.</param>
        public ScriptRunnerWindowsService(ServiceHostOptions options)
        {
            _options = options;
            ServiceName = options.ServiceName;
            CanStop = true;
            AutoLog = true;

            _bootstrapLogPath = ResolveBootstrapLogPath(options.ServiceLogPath);
            _bootstrapLogDirectory = Path.GetDirectoryName(_bootstrapLogPath) ?? Path.GetTempPath();
        }

        /// <inheritdoc />
        protected override void OnStart(string[] args)
        {
            WriteBootstrapLog($"Service '{ServiceName}' starting. Script='{_options.ScriptPath}'.");
            _runTask = Task.Run(() =>
            {
                try
                {
                    var exitCode = RunScript();
                    if (exitCode == 0)
                    {
                        WriteBootstrapLog($"Service '{ServiceName}' script exited normally.");
                        return;
                    }

                    WriteBootstrapLog($"Service '{ServiceName}' script exited with code {exitCode}.");
                    ExitCode = exitCode;
                    Stop();
                }
                catch (Exception ex)
                {
                    WriteBootstrapLog($"Service '{ServiceName}' failed with exception: {ex}");
                    try
                    {
                        EventLog.WriteEntry(ServiceName, ex.ToString(), EventLogEntryType.Error);
                    }
                    catch
                    {
                        // Ignore EventLog failures to avoid hiding the original error.
                    }

                    ExitCode = 1;
                    Stop();
                }
            });
        }

        /// <inheritdoc />
        protected override void OnStop()
        {
            WriteBootstrapLog($"Service '{ServiceName}' stopping.");
            try
            {
                RequestManagedStopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                WriteBootstrapLog($"Service '{ServiceName}' stop encountered error: {ex}");
            }

            _ = _runTask?.Wait(TimeSpan.FromSeconds(15));
            WriteBootstrapLog($"Service '{ServiceName}' stopped.");
        }

        private int RunScript()
        {
            var scriptPath = Path.GetFullPath(_options.ScriptPath);
            if (!File.Exists(scriptPath))
            {
                WriteBootstrapLog($"Script file not found: {scriptPath}");
                return 2;
            }

            var moduleManifestPath = LocateModuleManifest(_options.KestrunManifestPath, _options.KestrunFolder);
            if (moduleManifestPath is null)
            {
                WriteBootstrapLog("Unable to locate Kestrun.psd1 for service host run.");
                return 3;
            }

            moduleManifestPath = PrepareServiceModuleManifest(moduleManifestPath);

            return ExecuteScript(scriptPath, _options.ScriptArguments, moduleManifestPath);
        }

        /// <summary>
        /// Copies the resolved Kestrun module into a service-safe ProgramData cache and returns cached manifest path.
        /// </summary>
        /// <param name="sourceManifestPath">Resolved source manifest path.</param>
        /// <returns>Manifest path from ProgramData cache when copy succeeds; otherwise source path.</returns>
        private string PrepareServiceModuleManifest(string sourceManifestPath)
        {
            try
            {
                var sourceManifestFullPath = Path.GetFullPath(sourceManifestPath);
                var sourceModuleRoot = Path.GetDirectoryName(sourceManifestFullPath);
                if (string.IsNullOrWhiteSpace(sourceModuleRoot) || !Directory.Exists(sourceModuleRoot))
                {
                    WriteBootstrapLog($"Module cache skipped: invalid module root for '{sourceManifestFullPath}'.");
                    return sourceManifestPath;
                }

                var cacheRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Kestrun",
                    "module-cache");
                Directory.CreateDirectory(cacheRoot);

                // Stable cache key from full source module path.
                var normalized = sourceModuleRoot.ToLowerInvariant();
                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
                var cachedModuleRoot = Path.Combine(cacheRoot, hash);

                // Keep cache fresh on each service start to avoid stale binaries after local builds.
                if (Directory.Exists(cachedModuleRoot))
                {
                    Directory.Delete(cachedModuleRoot, recursive: true);
                }

                CopyDirectoryRecursive(sourceModuleRoot, cachedModuleRoot);

                var cachedManifestPath = Path.Combine(cachedModuleRoot, ModuleManifestFileName);
                if (!File.Exists(cachedManifestPath))
                {
                    WriteBootstrapLog($"Module cache copy completed but manifest missing at '{cachedManifestPath}'. Falling back to source manifest.");
                    return sourceManifestPath;
                }

                WriteBootstrapLog($"Service module cache prepared at '{cachedModuleRoot}'.");
                return cachedManifestPath;
            }
            catch (Exception ex)
            {
                WriteBootstrapLog($"Module cache preparation failed: {ex.Message}. Falling back to source manifest.");
                return sourceManifestPath;
            }
        }

        /// <summary>
        /// Recursively copies a directory preserving relative paths.
        /// </summary>
        /// <param name="sourceDirectory">Source directory path.</param>
        /// <param name="destinationDirectory">Destination directory path.</param>
        private static void CopyDirectoryRecursive(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                var targetPath = Path.Combine(destinationDirectory, relativePath);
                var targetFolder = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                File.Copy(sourceFile, targetPath, overwrite: true);
            }
        }

        private void WriteBootstrapLog(string message)
        {
            try
            {
                _ = Directory.CreateDirectory(_bootstrapLogDirectory);
                var line = $"{DateTime.UtcNow:O} {message}{Environment.NewLine}";
                File.AppendAllText(_bootstrapLogPath, line);
            }
            catch
            {
                // Best-effort logging only.
            }
        }

        /// <summary>
        /// Resolves the bootstrap log file path from optional configured path.
        /// </summary>
        /// <param name="configuredPath">Optional configured full path to the log file.</param>
        /// <returns>Absolute log file path.</returns>
        private static string ResolveBootstrapLogPath(string? configuredPath)
        {
            var defaultDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Kestrun",
                "logs");
            var defaultPath = Path.Combine(defaultDirectory, "script-runner-service.log");

            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return defaultPath;
            }

            var fullPath = Path.GetFullPath(configuredPath);
            if (Directory.Exists(fullPath)
                || configuredPath.EndsWith("\\", StringComparison.Ordinal)
                || configuredPath.EndsWith("/", StringComparison.Ordinal))
            {
                return Path.Combine(fullPath, "script-runner-service.log");
            }

            return fullPath;
        }
    }

    /// <summary>
    /// Performs non-admin checks before elevating a Windows service install request.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <param name="exitCode">Exit code when preflight fails.</param>
    /// <returns>True when install should proceed with elevation.</returns>
    [SupportedOSPlatform("windows")]
    private static bool TryPreflightWindowsServiceInstall(ParsedCommand command, out int exitCode)
    {
        exitCode = 0;
        if (string.IsNullOrWhiteSpace(command.ServiceName))
        {
            Console.Error.WriteLine("Service name is required. Use --name <value>.");
            exitCode = 2;
            return false;
        }

        var scriptPath = Path.GetFullPath(command.ScriptPath);
        if (!File.Exists(scriptPath))
        {
            Console.Error.WriteLine($"Script file not found: {scriptPath}");
            exitCode = 2;
            return false;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            Console.Error.WriteLine("Unable to resolve ScriptRunner executable path for service installation.");
            exitCode = 1;
            return false;
        }

        var moduleManifestPath = LocateModuleManifest(command.KestrunManifestPath, command.KestrunFolder);
        if (moduleManifestPath is null)
        {
            if (!string.IsNullOrWhiteSpace(command.KestrunManifestPath))
            {
                Console.Error.WriteLine($"Unable to locate manifest file: {Path.GetFullPath(command.KestrunManifestPath)}");
            }
            else if (!string.IsNullOrWhiteSpace(command.KestrunFolder))
            {
                Console.Error.WriteLine($"Unable to locate {ModuleManifestFileName} in folder: {Path.GetFullPath(command.KestrunFolder)}");
            }
            else
            {
                Console.Error.WriteLine($"Unable to locate {ModuleManifestFileName} under the executable folder or PSModulePath.");
            }

            exitCode = 3;
            return false;
        }

        if (WindowsServiceExists(command.ServiceName))
        {
            Console.Error.WriteLine($"Windows service '{command.ServiceName}' already exists.");
            exitCode = 2;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Performs non-admin checks before elevating a Windows service removal request.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <param name="exitCode">Exit code when preflight fails.</param>
    /// <returns>True when remove should proceed with elevation.</returns>
    [SupportedOSPlatform("windows")]
    private static bool TryPreflightWindowsServiceRemove(ParsedCommand command, out int exitCode)
    {
        exitCode = 0;
        if (string.IsNullOrWhiteSpace(command.ServiceName))
        {
            Console.Error.WriteLine("Service name is required. Use --name <value>.");
            exitCode = 2;
            return false;
        }

        if (!WindowsServiceExists(command.ServiceName))
        {
            Console.Error.WriteLine($"Windows service '{command.ServiceName}' was not found.");
            exitCode = 2;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Performs non-admin checks before elevating a Windows service start/stop request.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <param name="exitCode">Exit code when preflight fails.</param>
    /// <returns>True when control should proceed with elevation.</returns>
    [SupportedOSPlatform("windows")]
    private static bool TryPreflightWindowsServiceControl(ParsedCommand command, out int exitCode)
    {
        exitCode = 0;
        if (string.IsNullOrWhiteSpace(command.ServiceName))
        {
            Console.Error.WriteLine("Service name is required. Use --name <value>.");
            exitCode = 2;
            return false;
        }

        if (!WindowsServiceExists(command.ServiceName))
        {
            Console.Error.WriteLine($"Windows service '{command.ServiceName}' was not found.");
            exitCode = 2;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether a Windows service exists.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <returns>True when the service exists.</returns>
    [SupportedOSPlatform("windows")]
    private static bool WindowsServiceExists(string serviceName)
    {
        var result = RunProcess("sc.exe", ["query", serviceName], writeStandardOutput: false);
        if (result.ExitCode == 0)
        {
            return true;
        }

        var combined = $"{result.Output}\n{result.Error}";
        if (combined.Contains("1060", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Relaunches the current executable with UAC elevation on Windows.
    /// </summary>
    /// <param name="args">Original command-line arguments.</param>
    /// <returns>Exit code from the elevated child process or an error code.</returns>
    [SupportedOSPlatform("windows")]
    private static int RelaunchElevatedOnWindows(IReadOnlyList<string> args)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            Console.Error.WriteLine("Unable to resolve ScriptRunner executable path for elevation.");
            return 1;
        }

        Console.Error.WriteLine("Administrator rights are required. Requesting elevation...");

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = string.Join(" ", args.Select(EscapeWindowsCommandLineArgument)),
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Environment.CurrentDirectory,
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                Console.Error.WriteLine("Failed to start elevated process.");
                return 1;
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine("Elevated operation failed. If no UAC prompt was shown, run this command from an elevated terminal.");
            }

            return process.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            Console.Error.WriteLine("Elevation was canceled by the user.");
            Console.Error.WriteLine("Run this command from an elevated terminal if you want to proceed without UAC interaction.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to elevate process: {ex.Message}");
            Console.Error.WriteLine("Run this command from an elevated terminal if automatic elevation is unavailable.");
            return 1;
        }
    }

    /// <summary>
    /// Installs a service/daemon entry that runs the target script through the ScriptRunner executable.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <returns>Process exit code.</returns>
    private static int InstallService(ParsedCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ServiceName))
        {
            Console.Error.WriteLine("Service name is required. Use --name <value>.");
            return 2;
        }

        var scriptPath = Path.GetFullPath(command.ScriptPath);
        if (!File.Exists(scriptPath))
        {
            Console.Error.WriteLine($"Script file not found: {scriptPath}");
            return 2;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            Console.Error.WriteLine("Unable to resolve ScriptRunner executable path for service installation.");
            return 1;
        }

        var moduleManifestPath = LocateModuleManifest(command.KestrunManifestPath, command.KestrunFolder);
        if (moduleManifestPath is null)
        {
            if (!string.IsNullOrWhiteSpace(command.KestrunManifestPath))
            {
                Console.Error.WriteLine($"Unable to locate manifest file: {Path.GetFullPath(command.KestrunManifestPath)}");
            }
            else if (!string.IsNullOrWhiteSpace(command.KestrunFolder))
            {
                Console.Error.WriteLine($"Unable to locate {ModuleManifestFileName} in folder: {Path.GetFullPath(command.KestrunFolder)}");
            }
            else
            {
                Console.Error.WriteLine($"Unable to locate {ModuleManifestFileName} under the executable folder or PSModulePath.");
            }

            return 3;
        }

        var runnerArgs = BuildRunnerArgumentsForService(scriptPath, command.ScriptArguments, command.KestrunFolder, command.KestrunManifestPath);
        var workingDirectory = Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory;

        if (OperatingSystem.IsWindows())
        {
            return InstallWindowsService(command, exePath, scriptPath);
        }

        if (OperatingSystem.IsLinux())
        {
            return InstallLinuxUserDaemon(command.ServiceName, exePath, runnerArgs, workingDirectory);
        }

        if (OperatingSystem.IsMacOS())
        {
            return InstallMacLaunchAgent(command.ServiceName, exePath, runnerArgs, workingDirectory);
        }

        Console.Error.WriteLine("Service installation is not supported on this OS.");
        return 1;
    }

    /// <summary>
    /// Removes a previously installed service/daemon entry.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <returns>Process exit code.</returns>
    private static int RemoveService(ParsedCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ServiceName))
        {
            Console.Error.WriteLine("Service name is required. Use --name <value>.");
            return 2;
        }

        if (OperatingSystem.IsWindows())
        {
            return RemoveWindowsService(command);
        }

        if (OperatingSystem.IsLinux())
        {
            return RemoveLinuxUserDaemon(command.ServiceName);
        }

        if (OperatingSystem.IsMacOS())
        {
            return RemoveMacLaunchAgent(command.ServiceName);
        }

        Console.Error.WriteLine("Service removal is not supported on this OS.");
        return 1;
    }

    /// <summary>
    /// Starts a previously installed service/daemon entry.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <returns>Process exit code.</returns>
    private static int StartService(ParsedCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ServiceName))
        {
            Console.Error.WriteLine("Service name is required. Use --name <value>.");
            return 2;
        }

        if (OperatingSystem.IsWindows())
        {
            return StartWindowsService(command.ServiceName);
        }

        if (OperatingSystem.IsLinux())
        {
            return StartLinuxUserDaemon(command.ServiceName);
        }

        if (OperatingSystem.IsMacOS())
        {
            return StartMacLaunchAgent(command.ServiceName);
        }

        Console.Error.WriteLine("Service start is not supported on this OS.");
        return 1;
    }

    /// <summary>
    /// Stops a previously installed service/daemon entry.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <returns>Process exit code.</returns>
    private static int StopService(ParsedCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ServiceName))
        {
            Console.Error.WriteLine("Service name is required. Use --name <value>.");
            return 2;
        }

        if (OperatingSystem.IsWindows())
        {
            return StopWindowsService(command.ServiceName);
        }

        if (OperatingSystem.IsLinux())
        {
            return StopLinuxUserDaemon(command.ServiceName);
        }

        if (OperatingSystem.IsMacOS())
        {
            return StopMacLaunchAgent(command.ServiceName);
        }

        Console.Error.WriteLine("Service stop is not supported on this OS.");
        return 1;
    }

    /// <summary>
    /// Queries a previously installed service/daemon entry.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <returns>Process exit code.</returns>
    private static int QueryService(ParsedCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ServiceName))
        {
            Console.Error.WriteLine("Service name is required. Use --name <value>.");
            return 2;
        }

        if (OperatingSystem.IsWindows())
        {
            return QueryWindowsService(command.ServiceName);
        }

        if (OperatingSystem.IsLinux())
        {
            return QueryLinuxUserDaemon(command.ServiceName);
        }

        if (OperatingSystem.IsMacOS())
        {
            return QueryMacLaunchAgent(command.ServiceName);
        }

        Console.Error.WriteLine("Service query is not supported on this OS.");
        return 1;
    }

    /// <summary>
    /// Builds ScriptRunner command-line arguments used by installed services/daemons.
    /// </summary>
    /// <param name="scriptPath">Absolute script path to execute.</param>
    /// <param name="scriptArguments">Script arguments for the run command.</param>
    /// <param name="kestrunFolder">Optional folder containing Kestrun module manifest.</param>
    /// <returns>Ordered runner argument tokens.</returns>
    private static IReadOnlyList<string> BuildRunnerArgumentsForService(string scriptPath, IReadOnlyList<string> scriptArguments, string? kestrunFolder, string? kestrunManifestPath)
    {
        var arguments = new List<string>(8 + scriptArguments.Count);
        if (!string.IsNullOrWhiteSpace(kestrunManifestPath))
        {
            arguments.Add("--kestrun-manifest");
            arguments.Add(Path.GetFullPath(kestrunManifestPath));
        }

        if (!string.IsNullOrWhiteSpace(kestrunFolder))
        {
            arguments.Add("--kestrun-folder");
            arguments.Add(Path.GetFullPath(kestrunFolder));
        }

        arguments.Add("run");
        arguments.Add(scriptPath);

        if (scriptArguments.Count > 0)
        {
            arguments.Add("--arguments");
            arguments.AddRange(scriptArguments);
        }

        return arguments;
    }

    /// <summary>
    /// Installs a Windows service using sc.exe.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="exePath">Executable path.</param>
    /// <param name="runnerArgs">Runner arguments.</param>
    /// <returns>Process exit code.</returns>
    private static int InstallWindowsService(ParsedCommand command, string exePath, string scriptPath)
    {
        var hostArgs = BuildWindowsServiceHostArguments(command, scriptPath);
        var imagePath = BuildWindowsCommandLine(exePath, hostArgs);
        var createResult = RunProcess(
            "sc.exe",
            ["create", command.ServiceName!, "start=", "auto", "binPath=", imagePath, "DisplayName=", command.ServiceName!]);

        if (createResult.ExitCode != 0)
        {
            Console.Error.WriteLine(createResult.Error);
            return createResult.ExitCode;
        }

        WriteServiceOperationLog($"Service '{command.ServiceName}' install operation completed.", command.ServiceLogPath, command.ServiceName);

        Console.WriteLine($"Installed Windows service '{command.ServiceName}' (not started).");
        return 0;
    }

    /// <summary>
    /// Builds internal service-host arguments used for Windows SCM registration.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <param name="scriptPath">Absolute script path.</param>
    /// <returns>Ordered argument tokens.</returns>
    private static IReadOnlyList<string> BuildWindowsServiceHostArguments(ParsedCommand command, string scriptPath)
    {
        var arguments = new List<string>(12 + command.ScriptArguments.Length)
        {
            "--service-host",
            "--name",
            command.ServiceName!,
            "--script",
            scriptPath,
        };

        if (!string.IsNullOrWhiteSpace(command.KestrunFolder))
        {
            arguments.Add("--kestrun-folder");
            arguments.Add(Path.GetFullPath(command.KestrunFolder));
        }

        if (!string.IsNullOrWhiteSpace(command.KestrunManifestPath))
        {
            arguments.Add("--kestrun-manifest");
            arguments.Add(Path.GetFullPath(command.KestrunManifestPath));
        }

        if (!string.IsNullOrWhiteSpace(command.ServiceLogPath))
        {
            arguments.Add("--service-log-path");
            arguments.Add(Path.GetFullPath(command.ServiceLogPath));
        }

        if (command.ScriptArguments.Length > 0)
        {
            arguments.Add("--arguments");
            arguments.AddRange(command.ScriptArguments);
        }

        return arguments;
    }

    /// <summary>
    /// Removes a Windows service using sc.exe.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <returns>Process exit code.</returns>
    private static int RemoveWindowsService(ParsedCommand command)
    {
        var operationLogPath = ResolveServiceOperationLogPath(command.ServiceLogPath, command.ServiceName);

        _ = RunProcess("sc.exe", ["stop", command.ServiceName!]);
        var deleteResult = RunProcess("sc.exe", ["delete", command.ServiceName!]);
        if (deleteResult.ExitCode != 0)
        {
            Console.Error.WriteLine(deleteResult.Error);
            return deleteResult.ExitCode;
        }

        WriteServiceOperationLog($"Service '{command.ServiceName}' remove operation completed.", operationLogPath, command.ServiceName);

        Console.WriteLine($"Removed Windows service '{command.ServiceName}'.");
        return 0;
    }

    /// <summary>
    /// Writes an install/remove operation log line for Windows service lifecycle operations.
    /// </summary>
    /// <param name="message">Operation message to append.</param>
    /// <param name="configuredPath">Optional configured log path.</param>
    /// <param name="serviceName">Optional service name used to discover configured log path.</param>
    private static void WriteServiceOperationLog(string message, string? configuredPath, string? serviceName)
    {
        var logPath = ResolveServiceOperationLogPath(configuredPath, serviceName);
        try
        {
            var directory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            var line = $"{DateTime.UtcNow:O} {message}{Environment.NewLine}";
            File.AppendAllText(logPath, line, Encoding.UTF8);
        }
        catch
        {
            // Best-effort operation logging only.
        }
    }

    /// <summary>
    /// Resolves the service operation log path from user input, service config, or defaults.
    /// </summary>
    /// <param name="configuredPath">Optional configured log path.</param>
    /// <param name="serviceName">Optional service name for discovery from service config.</param>
    /// <returns>Absolute log file path.</returns>
    private static string ResolveServiceOperationLogPath(string? configuredPath, string? serviceName)
    {
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Kestrun",
            "logs",
            "script-runner-service.log");

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return NormalizeServiceLogPath(configuredPath, defaultFileName: "script-runner-service.log");
        }

        if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(serviceName)
            && TryGetWindowsServiceLogPath(serviceName, out var discoveredPath)
            && !string.IsNullOrWhiteSpace(discoveredPath))
        {
            return discoveredPath;
        }

        return defaultPath;
    }

    /// <summary>
    /// Tries to read the configured --service-log-path from a Windows service ImagePath.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="logPath">Discovered log path.</param>
    /// <returns>True when a log path could be discovered.</returns>
    private static bool TryGetWindowsServiceLogPath(string serviceName, out string? logPath)
    {
        logPath = null;
        var queryResult = RunProcess("sc.exe", ["qc", serviceName], writeStandardOutput: false);
        if (queryResult.ExitCode != 0)
        {
            return false;
        }

        var text = string.Concat(queryResult.Output, Environment.NewLine, queryResult.Error);
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            "--service-log-path\\s+(\\\"(?<quoted>[^\\\"]+)\\\"|(?<plain>\\S+))",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return false;
        }

        var rawPath = match.Groups["quoted"].Success
            ? match.Groups["quoted"].Value
            : match.Groups["plain"].Value;

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return false;
        }

        logPath = NormalizeServiceLogPath(rawPath, defaultFileName: "script-runner-service.log");
        return true;
    }

    /// <summary>
    /// Normalizes service log path input; directory input gets the default file name.
    /// </summary>
    /// <param name="inputPath">Configured path input.</param>
    /// <param name="defaultFileName">Default file name for directory-only inputs.</param>
    /// <returns>Absolute log file path.</returns>
    private static string NormalizeServiceLogPath(string inputPath, string defaultFileName)
    {
        var fullPath = Path.GetFullPath(inputPath);
        if (Directory.Exists(fullPath)
            || inputPath.EndsWith("\\", StringComparison.Ordinal)
            || inputPath.EndsWith("/", StringComparison.Ordinal))
        {
            return Path.Combine(fullPath, defaultFileName);
        }

        return fullPath;
    }

    /// <summary>
    /// Starts a Windows service using sc.exe.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <returns>Process exit code.</returns>
    private static int StartWindowsService(string serviceName)
    {
        var result = RunProcess("sc.exe", ["start", serviceName]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            return result.ExitCode;
        }

        Console.WriteLine($"Started Windows service '{serviceName}'.");
        return 0;
    }

    /// <summary>
    /// Stops a Windows service using sc.exe.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <returns>Process exit code.</returns>
    private static int StopWindowsService(string serviceName)
    {
        var result = RunProcess("sc.exe", ["stop", serviceName]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            return result.ExitCode;
        }

        Console.WriteLine($"Stopped Windows service '{serviceName}'.");
        return 0;
    }

    /// <summary>
    /// Queries a Windows service using sc.exe.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <returns>Process exit code.</returns>
    private static int QueryWindowsService(string serviceName)
    {
        var result = RunProcess("sc.exe", ["query", serviceName]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            return result.ExitCode;
        }

        return 0;
    }

    /// <summary>
    /// Installs a user-level systemd unit.
    /// </summary>
    /// <param name="serviceName">Unit base name.</param>
    /// <param name="exePath">Executable path.</param>
    /// <param name="runnerArgs">Runner arguments.</param>
    /// <param name="workingDirectory">Working directory for the unit.</param>
    /// <returns>Process exit code.</returns>
    private static int InstallLinuxUserDaemon(string serviceName, string exePath, IReadOnlyList<string> runnerArgs, string workingDirectory)
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var unitDirectory = Path.Combine(userHome, ".config", "systemd", "user");
        _ = Directory.CreateDirectory(unitDirectory);

        var unitName = GetLinuxUnitName(serviceName);
        var unitPath = Path.Combine(unitDirectory, unitName);
        var execStart = string.Join(" ", new[] { EscapeSystemdToken(exePath) }.Concat(runnerArgs.Select(EscapeSystemdToken)));
        var unitContent = string.Join('\n',
            "[Unit]",
            $"Description={serviceName}",
            "After=network.target",
            "",
            "[Service]",
            "Type=simple",
            $"WorkingDirectory={workingDirectory}",
            $"ExecStart={execStart}",
            "Restart=always",
            "RestartSec=2",
            "",
            "[Install]",
            "WantedBy=default.target",
            "");

        File.WriteAllText(unitPath, unitContent);

        var reloadResult = RunProcess("systemctl", ["--user", "daemon-reload"]);
        if (reloadResult.ExitCode != 0)
        {
            Console.Error.WriteLine(reloadResult.Error);
            return reloadResult.ExitCode;
        }

        var enableResult = RunProcess("systemctl", ["--user", "enable", unitName]);
        if (enableResult.ExitCode != 0)
        {
            Console.Error.WriteLine(enableResult.Error);
            return enableResult.ExitCode;
        }

        Console.WriteLine($"Installed Linux user daemon '{unitName}' (not started).");
        return 0;
    }

    /// <summary>
    /// Removes a user-level systemd unit.
    /// </summary>
    /// <param name="serviceName">Unit base name.</param>
    /// <returns>Process exit code.</returns>
    private static int RemoveLinuxUserDaemon(string serviceName)
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var unitDirectory = Path.Combine(userHome, ".config", "systemd", "user");
        var unitName = GetLinuxUnitName(serviceName);
        var unitPath = Path.Combine(unitDirectory, unitName);

        _ = RunProcess("systemctl", ["--user", "disable", "--now", unitName]);
        if (File.Exists(unitPath))
        {
            File.Delete(unitPath);
        }

        var reloadResult = RunProcess("systemctl", ["--user", "daemon-reload"]);
        if (reloadResult.ExitCode != 0)
        {
            Console.Error.WriteLine(reloadResult.Error);
            return reloadResult.ExitCode;
        }

        Console.WriteLine($"Removed Linux user daemon '{unitName}'.");
        return 0;
    }

    /// <summary>
    /// Starts a Linux user-level systemd unit.
    /// </summary>
    /// <param name="serviceName">Unit base name.</param>
    /// <returns>Process exit code.</returns>
    private static int StartLinuxUserDaemon(string serviceName)
    {
        var unitName = GetLinuxUnitName(serviceName);
        var result = RunProcess("systemctl", ["--user", "start", unitName]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            return result.ExitCode;
        }

        Console.WriteLine($"Started Linux user daemon '{unitName}'.");
        return 0;
    }

    /// <summary>
    /// Stops a Linux user-level systemd unit.
    /// </summary>
    /// <param name="serviceName">Unit base name.</param>
    /// <returns>Process exit code.</returns>
    private static int StopLinuxUserDaemon(string serviceName)
    {
        var unitName = GetLinuxUnitName(serviceName);
        var result = RunProcess("systemctl", ["--user", "stop", unitName]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            return result.ExitCode;
        }

        Console.WriteLine($"Stopped Linux user daemon '{unitName}'.");
        return 0;
    }

    /// <summary>
    /// Queries a Linux user-level systemd unit.
    /// </summary>
    /// <param name="serviceName">Unit base name.</param>
    /// <returns>Process exit code.</returns>
    private static int QueryLinuxUserDaemon(string serviceName)
    {
        var unitName = GetLinuxUnitName(serviceName);
        var result = RunProcess("systemctl", ["--user", "status", unitName]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            return result.ExitCode;
        }

        return 0;
    }

    /// <summary>
    /// Installs a macOS launch agent plist.
    /// </summary>
    /// <param name="serviceName">Agent label.</param>
    /// <param name="exePath">Executable path.</param>
    /// <param name="runnerArgs">Runner arguments.</param>
    /// <param name="workingDirectory">Working directory for launchd.</param>
    /// <returns>Process exit code.</returns>
    private static int InstallMacLaunchAgent(string serviceName, string exePath, IReadOnlyList<string> runnerArgs, string workingDirectory)
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var agentDirectory = Path.Combine(userHome, "Library", "LaunchAgents");
        _ = Directory.CreateDirectory(agentDirectory);

        var plistName = $"{serviceName}.plist";
        var plistPath = Path.Combine(agentDirectory, plistName);
        var programArgs = new[] { exePath }.Concat(runnerArgs).ToArray();
        var plistContent = BuildLaunchdPlist(serviceName, workingDirectory, programArgs);
        File.WriteAllText(plistPath, plistContent);

        Console.WriteLine($"Installed macOS launch agent '{serviceName}' (not started).");
        return 0;
    }

    /// <summary>
    /// Removes a macOS launch agent plist.
    /// </summary>
    /// <param name="serviceName">Agent label.</param>
    /// <returns>Process exit code.</returns>
    private static int RemoveMacLaunchAgent(string serviceName)
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var agentDirectory = Path.Combine(userHome, "Library", "LaunchAgents");
        var plistPath = Path.Combine(agentDirectory, $"{serviceName}.plist");

        _ = RunProcess("launchctl", ["unload", plistPath]);
        if (File.Exists(plistPath))
        {
            File.Delete(plistPath);
        }

        Console.WriteLine($"Removed macOS launch agent '{serviceName}'.");
        return 0;
    }

    /// <summary>
    /// Starts a macOS launch agent by loading its plist.
    /// </summary>
    /// <param name="serviceName">Agent label.</param>
    /// <returns>Process exit code.</returns>
    private static int StartMacLaunchAgent(string serviceName)
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var agentDirectory = Path.Combine(userHome, "Library", "LaunchAgents");
        var plistPath = Path.Combine(agentDirectory, $"{serviceName}.plist");
        if (!File.Exists(plistPath))
        {
            Console.Error.WriteLine($"Launch agent plist not found: {plistPath}");
            return 2;
        }

        var result = RunProcess("launchctl", ["load", "-w", plistPath]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            return result.ExitCode;
        }

        Console.WriteLine($"Started macOS launch agent '{serviceName}'.");
        return 0;
    }

    /// <summary>
    /// Stops a macOS launch agent by unloading its plist.
    /// </summary>
    /// <param name="serviceName">Agent label.</param>
    /// <returns>Process exit code.</returns>
    private static int StopMacLaunchAgent(string serviceName)
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var agentDirectory = Path.Combine(userHome, "Library", "LaunchAgents");
        var plistPath = Path.Combine(agentDirectory, $"{serviceName}.plist");
        if (!File.Exists(plistPath))
        {
            Console.Error.WriteLine($"Launch agent plist not found: {plistPath}");
            return 2;
        }

        var result = RunProcess("launchctl", ["unload", plistPath]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            return result.ExitCode;
        }

        Console.WriteLine($"Stopped macOS launch agent '{serviceName}'.");
        return 0;
    }

    /// <summary>
    /// Queries a macOS launch agent by label.
    /// </summary>
    /// <param name="serviceName">Agent label.</param>
    /// <returns>Process exit code.</returns>
    private static int QueryMacLaunchAgent(string serviceName)
    {
        var result = RunProcess("launchctl", ["list", serviceName]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            return result.ExitCode;
        }

        return 0;
    }

    /// <summary>
    /// Builds a launchd plist document for a persistent user agent.
    /// </summary>
    /// <param name="label">Launchd label.</param>
    /// <param name="workingDirectory">Working directory.</param>
    /// <param name="programArguments">Program argument list.</param>
    /// <returns>XML plist content.</returns>
    private static string BuildLaunchdPlist(string label, string workingDirectory, IReadOnlyList<string> programArguments)
    {
        var argsXml = string.Join(string.Empty, programArguments.Select(arg => $"\n    <string>{EscapeXml(arg)}</string>"));
        return $"""
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key>
  <string>{EscapeXml(label)}</string>
  <key>ProgramArguments</key>
  <array>{argsXml}
  </array>
  <key>WorkingDirectory</key>
  <string>{EscapeXml(workingDirectory)}</string>
  <key>RunAtLoad</key>
  <true/>
  <key>KeepAlive</key>
  <true/>
</dict>
</plist>
""";
    }

    /// <summary>
    /// Escapes XML-sensitive characters.
    /// </summary>
    /// <param name="input">Raw input string.</param>
    /// <returns>Escaped XML value.</returns>
    private static string EscapeXml(string input)
    {
        return input
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    /// <summary>
    /// Escapes one token for systemd ExecStart parsing.
    /// </summary>
    /// <param name="input">Raw token.</param>
    /// <returns>Escaped token.</returns>
    private static string EscapeSystemdToken(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "\"\"";
        }

        var escaped = input
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(" ", "\\ ", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal);

        return escaped;
    }

    /// <summary>
    /// Builds a Windows command-line string with proper escaping for each token.
    /// </summary>
    /// <param name="exePath">Executable path.</param>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Full command line string.</returns>
    private static string BuildWindowsCommandLine(string exePath, IReadOnlyList<string> args)
    {
        var all = new List<string>(1 + args.Count) { exePath };
        all.AddRange(args);
        return string.Join(" ", all.Select(EscapeWindowsCommandLineArgument));
    }

    /// <summary>
    /// Escapes one command-line argument using Windows CreateProcess rules.
    /// </summary>
    /// <param name="arg">Input argument.</param>
    /// <returns>Escaped argument string.</returns>
    private static string EscapeWindowsCommandLineArgument(string arg)
    {
        if (arg.Length == 0)
        {
            return "\"\"";
        }

        var requiresQuotes = arg.Any(c => char.IsWhiteSpace(c) || c == '"');
        if (!requiresQuotes)
        {
            return arg;
        }

        var result = new System.Text.StringBuilder(arg.Length + 2);
        _ = result.Append('"');
        var backslashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\')
            {
                backslashes += 1;
                continue;
            }

            if (c == '"')
            {
                _ = result.Append('\\', backslashes * 2 + 1);
                _ = result.Append('"');
                backslashes = 0;
                continue;
            }

            if (backslashes > 0)
            {
                _ = result.Append('\\', backslashes);
                backslashes = 0;
            }

            _ = result.Append(c);
        }

        if (backslashes > 0)
        {
            _ = result.Append('\\', backslashes * 2);
        }

        _ = result.Append('"');
        return result.ToString();
    }

    /// <summary>
    /// Builds a normalized systemd unit name.
    /// </summary>
    /// <param name="serviceName">Input service name.</param>
    /// <returns>Sanitized unit file name.</returns>
    private static string GetLinuxUnitName(string serviceName)
    {
        var safeName = new string(serviceName
            .Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-')
            .ToArray());

        return safeName.EndsWith(".service", StringComparison.OrdinalIgnoreCase)
            ? safeName
            : $"{safeName}.service";
    }

    /// <summary>
    /// Runs a process and captures output for diagnostics.
    /// </summary>
    /// <param name="fileName">Executable to run.</param>
    /// <param name="arguments">Argument tokens.</param>
    /// <returns>Process result data.</returns>
    private static ProcessResult RunProcess(string fileName, IReadOnlyList<string> arguments, bool writeStandardOutput = true)
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

        if (writeStandardOutput && !string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine(output.TrimEnd());
        }

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
    /// Executes the target script in a runspace that has Kestrun imported by manifest path.
    /// </summary>
    /// <param name="scriptPath">Absolute path to the script to execute.</param>
    /// <param name="scriptArguments">Command-line arguments passed to the target script.</param>
    /// <param name="moduleManifestPath">Absolute path to Kestrun.psd1.</param>
    /// <returns>Process exit code.</returns>
    private static int ExecuteScript(string scriptPath, IReadOnlyList<string> scriptArguments, string moduleManifestPath)
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

        var stopRequested = false;
        void cancelHandler(object? _, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            if (stopRequested)
            {
                return;
            }

            stopRequested = true;
            Console.Error.WriteLine("Ctrl+C detected. Stopping Kestrun server...");
            _ = Task.Run(RequestManagedStopAsync);
        }

        Console.CancelKeyPress += cancelHandler;
        IEnumerable<PSObject> output;
        try
        {
            var asyncResult = powershell.BeginInvoke();
            while (!asyncResult.IsCompleted)
            {
                _ = asyncResult.AsyncWaitHandle.WaitOne(200);
            }

            output = powershell.EndInvoke(asyncResult);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }

        WriteOutput(output);
        WriteStreams(powershell.Streams);

        return powershell.HadErrors ? 1 : 0;
    }

    /// <summary>
    /// Ensures the runner is executing on .NET 10.
    /// </summary>
    private static void EnsureNet10Runtime()
    {
        var framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        if (!framework.Contains(".NET 10", StringComparison.OrdinalIgnoreCase))
        {
            throw new RuntimeException($"{ProductName} requires .NET 10 runtime. Current runtime: {framework}");
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
            var manifestText = File.ReadAllText(manifestPath);
            return manifestText.Contains("CompatiblePSEditions", StringComparison.Ordinal)
                && manifestText.Contains("Core", StringComparison.OrdinalIgnoreCase);
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
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            candidates.Add(Path.Combine(programFiles, "PowerShell", "7"));
            candidates.Add(Path.Combine(programFiles, "PowerShell", "7-preview"));
        }

        var whereResult = RunProcess("where.exe", ["pwsh"], writeStandardOutput: false);
        if (whereResult.ExitCode == 0)
        {
            var discovered = whereResult.Output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Path.GetDirectoryName)
                .Where(static p => !string.IsNullOrWhiteSpace(p))
                .Select(static p => Path.GetFullPath(p!));
            candidates.AddRange(discovered);
        }

        return candidates
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

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

    /// <summary>
    /// Writes PowerShell pipeline output to stdout.
    /// </summary>
    /// <param name="output">Pipeline output collection.</param>
    private static void WriteOutput(IEnumerable<PSObject> output)
    {
        foreach (var item in output)
        {
            if (item == null)
            {
                continue;
            }

            Console.Out.WriteLine(item.BaseObject?.ToString() ?? item.ToString());
        }
    }

    /// <summary>
    /// Writes non-output streams in a console-friendly format.
    /// </summary>
    /// <param name="streams">PowerShell data streams.</param>
    private static void WriteStreams(PSDataStreams streams)
    {
        foreach (var record in streams.Warning)
        {
            Console.Error.WriteLine($"WARNING: {record}");
        }

        foreach (var record in streams.Verbose)
        {
            Console.Error.WriteLine($"VERBOSE: {record}");
        }

        foreach (var record in streams.Debug)
        {
            Console.Error.WriteLine($"DEBUG: {record}");
        }

        foreach (var record in streams.Information)
        {
            var message = record.MessageData?.ToString();
            if (!string.IsNullOrWhiteSpace(message))
            {
                Console.Out.WriteLine(message);
            }
        }

        foreach (var record in streams.Error)
        {
            Console.Error.WriteLine(record);
        }
    }

    /// <summary>
    /// Tries to parse command-line arguments into a concrete command payload.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="parsedCommand">Parsed command payload.</param>
    /// <param name="error">Error message when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private static bool TryParseArguments(string[] args, out ParsedCommand parsedCommand, out string error)
    {
        parsedCommand = new ParsedCommand(CommandMode.Run, string.Empty, [], null, null, null, null);
        error = string.Empty;

        if (args.Length == 0)
        {
            error = $"No command provided. Use '{ProductName} help' to list commands.";
            return false;
        }

        var commandTokenIndex = 0;
        string? kestrunFolder = null;
        string? kestrunManifestPath = null;
        while (commandTokenIndex < args.Length)
        {
            if (args[commandTokenIndex] is "--kestrun-folder" or "-k")
            {
                if (commandTokenIndex + 1 >= args.Length)
                {
                    error = "Missing value for --kestrun-folder.";
                    return false;
                }

                kestrunFolder = args[commandTokenIndex + 1];
                commandTokenIndex += 2;
                continue;
            }

            if (args[commandTokenIndex] is "--kestrun-manifest" or "-m")
            {
                if (commandTokenIndex + 1 >= args.Length)
                {
                    error = "Missing value for --kestrun-manifest.";
                    return false;
                }

                kestrunManifestPath = args[commandTokenIndex + 1];
                commandTokenIndex += 2;
                continue;
            }

            break;
        }

        if (commandTokenIndex >= args.Length)
        {
            error = $"No command provided. Use '{ProductName} help' to list commands.";
            return false;
        }

        if (string.Equals(args[commandTokenIndex], "run", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseRunArguments(args, commandTokenIndex + 1, kestrunFolder, kestrunManifestPath, out parsedCommand, out error);
        }

        if (string.Equals(args[commandTokenIndex], "service", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseServiceArguments(args, commandTokenIndex + 1, kestrunFolder, kestrunManifestPath, out parsedCommand, out error);
        }

        error = $"Unknown command: {args[commandTokenIndex]}. Use '{ProductName} help' to list commands.";
        return false;
    }

    /// <summary>
    /// Parses arguments for the run command.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="startIndex">Index after command token.</param>
    /// <param name="kestrunFolder">Optional folder containing Kestrun.psd1.</param>
    /// <param name="parsedCommand">Parsed command payload.</param>
    /// <param name="error">Error message when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private static bool TryParseRunArguments(string[] args, int startIndex, string? kestrunFolder, string? kestrunManifestPath, out ParsedCommand parsedCommand, out string error)
    {
        parsedCommand = new ParsedCommand(CommandMode.Run, string.Empty, [], kestrunFolder, kestrunManifestPath, null, null);
        error = string.Empty;

        var scriptPathSet = false;
        var scriptPath = string.Empty;
        var scriptArguments = Array.Empty<string>();
        var index = startIndex;
        while (index < args.Length)
        {
            var current = args[index];
            if (current is "--script" && index + 1 < args.Length)
            {
                if (scriptPathSet)
                {
                    error = "Script path was provided multiple times. Use either positional script path or --script once.";
                    return false;
                }

                scriptPath = args[index + 1];
                scriptPathSet = true;
                index += 2;
                continue;
            }

            if (current is "--kestrun-folder" or "-k")
            {
                if (index + 1 >= args.Length)
                {
                    error = "Missing value for --kestrun-folder.";
                    return false;
                }

                kestrunFolder = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--kestrun-manifest" or "-m")
            {
                if (index + 1 >= args.Length)
                {
                    error = "Missing value for --kestrun-manifest.";
                    return false;
                }

                kestrunManifestPath = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--arguments" or "--")
            {
                scriptArguments = [.. args.Skip(index + 1)];
                break;
            }

            if (current.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unknown option: {current}";
                return false;
            }

            if (scriptPathSet)
            {
                error = "Script arguments must be preceded by --arguments (or --).";
                return false;
            }

            scriptPath = current;
            scriptPathSet = true;
            index += 1;
            continue;
        }

        if (!scriptPathSet)
        {
            // Default to ./server.ps1 when a script path is not explicitly provided.
            scriptPath = DefaultScriptFileName;
        }

        parsedCommand = new ParsedCommand(CommandMode.Run, scriptPath, scriptArguments, kestrunFolder, kestrunManifestPath, null, null);

        return true;
    }

    /// <summary>
    /// Parses arguments for service install/remove commands.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="startIndex">Index after service token.</param>
    /// <param name="kestrunFolder">Optional folder containing Kestrun.psd1.</param>
    /// <param name="parsedCommand">Parsed command payload.</param>
    /// <param name="error">Error message when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private static bool TryParseServiceArguments(string[] args, int startIndex, string? kestrunFolder, string? kestrunManifestPath, out ParsedCommand parsedCommand, out string error)
    {
        parsedCommand = new ParsedCommand(CommandMode.ServiceInstall, string.Empty, [], kestrunFolder, kestrunManifestPath, null, null);
        error = string.Empty;

        if (startIndex >= args.Length)
        {
            error = "Missing service action. Use 'service install', 'service remove', 'service start', 'service stop', or 'service query'.";
            return false;
        }

        var action = args[startIndex];
        var mode = action.ToLowerInvariant() switch
        {
            "install" => CommandMode.ServiceInstall,
            "remove" => CommandMode.ServiceRemove,
            "start" => CommandMode.ServiceStart,
            "stop" => CommandMode.ServiceStop,
            "query" => CommandMode.ServiceQuery,
            _ => (CommandMode?)null,
        };

        if (mode is null)
        {
            error = $"Unknown service action: {action}. Use 'service install', 'service remove', 'service start', 'service stop', or 'service query'.";
            return false;
        }

        var index = startIndex + 1;
        var serviceName = string.Empty;
        var scriptPath = string.Empty;
        var scriptPathSet = false;
        var scriptArguments = Array.Empty<string>();
        string? serviceLogPath = null;

        while (index < args.Length)
        {
            var current = args[index];
            if (current is "--script")
            {
                if (mode is CommandMode.ServiceRemove or CommandMode.ServiceStart or CommandMode.ServiceStop or CommandMode.ServiceQuery)
                {
                    error = "Service remove/start/stop/query does not accept --script.";
                    return false;
                }

                if (index + 1 >= args.Length)
                {
                    error = "Missing value for --script.";
                    return false;
                }

                if (scriptPathSet)
                {
                    error = "Script path was provided multiple times. Use either positional script path or --script once.";
                    return false;
                }

                scriptPath = args[index + 1];
                scriptPathSet = true;
                index += 2;
                continue;
            }

            if (current is "--name" or "-n")
            {
                if (index + 1 >= args.Length)
                {
                    error = "Missing value for --name.";
                    return false;
                }

                serviceName = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--kestrun-folder" or "-k")
            {
                if (index + 1 >= args.Length)
                {
                    error = "Missing value for --kestrun-folder.";
                    return false;
                }

                kestrunFolder = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--kestrun-manifest" or "-m")
            {
                if (index + 1 >= args.Length)
                {
                    error = "Missing value for --kestrun-manifest.";
                    return false;
                }

                kestrunManifestPath = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--service-log-path")
            {
                if (index + 1 >= args.Length)
                {
                    error = "Missing value for --service-log-path.";
                    return false;
                }

                serviceLogPath = args[index + 1];
                index += 2;
                continue;
            }

            if (mode == CommandMode.ServiceInstall && (current is "--arguments" or "--"))
            {
                scriptArguments = [.. args.Skip(index + 1)];
                break;
            }

            if (current.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unknown option: {current}";
                return false;
            }

            if (mode is CommandMode.ServiceRemove or CommandMode.ServiceStart or CommandMode.ServiceStop or CommandMode.ServiceQuery)
            {
                error = "Service remove/start/stop/query does not accept a script path.";
                return false;
            }

            if (scriptPathSet)
            {
                error = "Service install script arguments must be preceded by --arguments (or --).";
                return false;
            }

            scriptPath = current;
            scriptPathSet = true;
            index += 1;
        }

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            error = "Service name is required. Use --name <value>.";
            return false;
        }

        if (mode == CommandMode.ServiceInstall && !scriptPathSet)
        {
            scriptPath = DefaultScriptFileName;
        }

        parsedCommand = new ParsedCommand(mode.Value, scriptPath, scriptArguments, kestrunFolder, kestrunManifestPath, serviceName, serviceLogPath);

        return true;
    }

    /// <summary>
    /// Handles help/info/version command routing before command parsing.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="exitCode">Exit code for the handled command.</param>
    /// <returns>True when a meta command was handled.</returns>
    private static bool TryHandleMetaCommands(string[] args, out int exitCode)
    {
        exitCode = 0;
        var filtered = FilterGlobalOptions(args);
        if (filtered.Count == 0)
        {
            PrintUsage();
            return true;
        }

        if (IsHelpToken(filtered[0]) || string.Equals(filtered[0], "help", StringComparison.OrdinalIgnoreCase))
        {
            if (filtered.Count == 1)
            {
                PrintUsage();
                return true;
            }

            if (filtered.Count == 2 && TryGetHelpTopic(filtered[1], out var topic))
            {
                PrintHelpForTopic(topic);
                return true;
            }

            Console.Error.WriteLine("Unknown help topic. Use 'kestrun help' to list available topics.");
            exitCode = 2;
            return true;
        }

        if (filtered.Count == 2
            && TryGetHelpTopic(filtered[0], out var commandTopic)
            && (IsHelpToken(filtered[1]) || string.Equals(filtered[1], "help", StringComparison.OrdinalIgnoreCase)))
        {
            PrintHelpForTopic(commandTopic);
            return true;
        }

        if (filtered.Count == 1 && string.Equals(filtered[0], "version", StringComparison.OrdinalIgnoreCase))
        {
            PrintVersion();
            return true;
        }

        if (filtered.Count == 1 && string.Equals(filtered[0], "info", StringComparison.OrdinalIgnoreCase))
        {
            PrintInfo();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether an argument token requests usage help.
    /// </summary>
    /// <param name="token">Command-line token to inspect.</param>
    /// <returns>True when the token is a help switch.</returns>
    private static bool IsHelpToken(string token) => token is "-h" or "--help" or "/?";

    /// <summary>
    /// Tries to map a help topic token to a known command topic.
    /// </summary>
    /// <param name="token">Input help topic token.</param>
    /// <param name="topic">Normalized topic when recognized.</param>
    /// <returns>True when the topic is recognized.</returns>
    private static bool TryGetHelpTopic(string token, out string topic)
    {
        topic = token.ToLowerInvariant();
        return topic is "run" or "service" or "info" or "version";
    }

    /// <summary>
    /// Filters known global options injected by launchers from command-line args.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <returns>Arguments without known global options and their values.</returns>
    private static List<string> FilterGlobalOptions(string[] args)
    {
        var filtered = new List<string>(args.Length);
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] is "--kestrun-folder" or "-k" or "--kestrun-manifest" or "-m")
            {
                index += 1;
                continue;
            }

            filtered.Add(args[index]);
        }

        return filtered;
    }

    /// <summary>
    /// Prints command usage and discovery hints.
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  kestrun <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  run       Run a PowerShell script (default script: ./server.ps1)");
        Console.WriteLine("  service   Manage service lifecycle (install/remove/start/stop/query)");
        Console.WriteLine("  info      Show runtime/build diagnostics");
        Console.WriteLine("  version   Show tool version");
        Console.WriteLine();
        Console.WriteLine("Help topics:");
        Console.WriteLine("  kestrun run help");
        Console.WriteLine("  kestrun service help");
        Console.WriteLine("  kestrun info help");
        Console.WriteLine("  kestrun version help");
    }

    /// <summary>
    /// Prints detailed help for a specific topic.
    /// </summary>
    /// <param name="topic">Help topic.</param>
    private static void PrintHelpForTopic(string topic)
    {
        switch (topic)
        {
            case "run":
                Console.WriteLine("Usage:");
                Console.WriteLine("  kestrun [--kestrun-folder <folder>] [--kestrun-manifest <path-to-Kestrun.psd1>] run [--script <main.ps1> | <main.ps1>] [--arguments <script arguments...>]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --script <path>             Optional named script path (alternative to positional <main.ps1>).");
                Console.WriteLine("  --kestrun-manifest <path>   Use an explicit Kestrun.psd1 manifest file.");
                Console.WriteLine("  --arguments <args...>       Pass remaining values to the script as script arguments.");
                Console.WriteLine();
                Console.WriteLine("Notes:");
                Console.WriteLine("  - If no script is provided, ./server.ps1 is used.");
                Console.WriteLine("  - Script arguments must be passed after --arguments (or --).");
                Console.WriteLine("  - Use --kestrun-manifest to pin a specific Kestrun.psd1 file.");
                break;

            case "service":
                Console.WriteLine("Usage:");
                Console.WriteLine("  kestrun [--kestrun-folder <folder>] [--kestrun-manifest <path-to-Kestrun.psd1>] service install --name <service-name> [--service-log-path <path-to-log-file>] [--script <main.ps1> | <main.ps1>] [--arguments <script arguments...>]");
                Console.WriteLine("  kestrun service remove --name <service-name>");
                Console.WriteLine("  kestrun service start --name <service-name>");
                Console.WriteLine("  kestrun service stop --name <service-name>");
                Console.WriteLine("  kestrun service query --name <service-name>");
                Console.WriteLine();
                Console.WriteLine("Options (service install):");
                Console.WriteLine("  --script <path>             Optional named script path (alternative to positional <main.ps1>).");
                Console.WriteLine("  --kestrun-manifest <path>   Use an explicit Kestrun.psd1 manifest for the service runtime.");
                Console.WriteLine("  --service-log-path <path>   Set service bootstrap/operation log file path.");
                Console.WriteLine("  --arguments <args...>       Pass remaining values to the installed script.");
                Console.WriteLine();
                Console.WriteLine("Notes:");
                Console.WriteLine("  - install registers the service/daemon but does not auto-start it.");
                Console.WriteLine("  - remove/start/stop/query require --name and do not accept script paths.");
                break;

            case "info":
                Console.WriteLine("Usage:");
                Console.WriteLine("  kestrun info");
                Console.WriteLine();
                Console.WriteLine("Shows runtime and build diagnostics (framework, OS, architecture, and binary paths).");
                break;

            case "version":
                Console.WriteLine("Usage:");
                Console.WriteLine("  kestrun version");
                Console.WriteLine();
                Console.WriteLine("Shows the kestrun tool version.");
                break;
        }
    }

    /// <summary>
    /// Prints the ScriptRunner version.
    /// </summary>
    private static void PrintVersion()
    {
        var version = GetProductVersion();
        Console.WriteLine($"{ProductName} {version}");
    }

    /// <summary>
    /// Prints diagnostic information about the ScriptRunner build and runtime.
    /// </summary>
    private static void PrintInfo()
    {
        var version = GetProductVersion();
        var assembly = typeof(Program).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        Console.WriteLine($"Product: {ProductName}");
        Console.WriteLine($"Version: {version}");
        Console.WriteLine($"InformationalVersion: {informationalVersion}");
        Console.WriteLine($"Framework: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        Console.WriteLine($"OSArchitecture: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"ProcessArchitecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"ExecutableDirectory: {GetExecutableDirectory()}");
        Console.WriteLine($"BaseDirectory: {Path.GetFullPath(AppContext.BaseDirectory)}");
    }

    /// <summary>
    /// Gets the product version from assembly metadata.
    /// </summary>
    /// <returns>Product version string.</returns>
    private static string GetProductVersion()
    {
        var assembly = typeof(Program).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return !string.IsNullOrWhiteSpace(informationalVersion) ?
        informationalVersion : assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    /// <summary>
    /// Locates Kestrun.psd1 without launching an external pwsh process.
    /// </summary>
    /// <param name="kestrunManifestPath">Optional explicit path to Kestrun.psd1.</param>
    /// <param name="kestrunFolder">Optional folder containing Kestrun.psd1.</param>
    /// <returns>Absolute manifest path when found; otherwise null.</returns>
    private static string? LocateModuleManifest(string? kestrunManifestPath, string? kestrunFolder)
    {
        if (!string.IsNullOrWhiteSpace(kestrunManifestPath))
        {
            var explicitPath = Path.GetFullPath(kestrunManifestPath);
            var explicitManifest = Directory.Exists(explicitPath)
                ? Path.Combine(explicitPath, ModuleManifestFileName)
                : explicitPath;
            return File.Exists(explicitManifest) ? explicitManifest : null;
        }

        if (!string.IsNullOrWhiteSpace(kestrunFolder))
        {
            var explicitFolder = Path.GetFullPath(kestrunFolder);
            var explicitCandidate = Path.Combine(explicitFolder, ModuleManifestFileName);
            return File.Exists(explicitCandidate) ? explicitCandidate : null;
        }

        foreach (var candidate in EnumerateExecutableManifestCandidates())
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        foreach (var candidate in EnumerateModulePathManifestCandidates())
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    /// <summary>
    /// Enumerates candidate locations for Kestrun.psd1 under the executable folder.
    /// </summary>
    /// <returns>Absolute manifest path when found; otherwise null.</returns>
    private static IEnumerable<string> EnumerateExecutableManifestCandidates()
    {
        var executableDirectory = GetExecutableDirectory();

        yield return Path.Combine(executableDirectory, ModuleManifestFileName);
        yield return Path.Combine(executableDirectory, ModuleName, ModuleManifestFileName);

        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        if (!string.Equals(baseDirectory, executableDirectory, StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(baseDirectory, ModuleManifestFileName);
            yield return Path.Combine(baseDirectory, ModuleName, ModuleManifestFileName);
        }
    }

    /// <summary>
    /// Gets the directory where the executable file is located.
    /// </summary>
    /// <returns>Absolute executable directory path.</returns>
    private static string GetExecutableDirectory()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var processDirectory = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrWhiteSpace(processDirectory))
            {
                return Path.GetFullPath(processDirectory);
            }
        }

        return Path.GetFullPath(AppContext.BaseDirectory);
    }

    /// <summary>
    /// Enumerates candidate manifest paths under PSModulePath entries.
    /// </summary>
    /// <returns>Potential manifest file paths from PSModulePath.</returns>
    private static IEnumerable<string> EnumerateModulePathManifestCandidates()
    {
        var modulePathRaw = Environment.GetEnvironmentVariable("PSModulePath");
        if (string.IsNullOrWhiteSpace(modulePathRaw))
        {
            yield break;
        }

        var moduleRoots = modulePathRaw
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var root in moduleRoots)
        {
            var moduleDirectory = Path.Combine(root, ModuleName);
            yield return Path.Combine(moduleDirectory, ModuleManifestFileName);

            if (!Directory.Exists(moduleDirectory))
            {
                continue;
            }

            var versionDirectories = Directory.EnumerateDirectories(moduleDirectory)
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var versionDirectory in versionDirectories)
            {
                yield return Path.Combine(versionDirectory, ModuleManifestFileName);
            }
        }
    }

}
