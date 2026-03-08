using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using Kestrun.Runner;
using Microsoft.PowerShell;
using System.Diagnostics;
using System.ComponentModel;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Runtime.InteropServices;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Kestrun.Tool;

internal static partial class Program
{
    private const string ModuleManifestFileName = "Kestrun.psd1";
    private const string ModuleName = "Kestrun";
    private const string DefaultScriptFileName = "server.ps1";
    private const string ProductName = "kestrun";
    private const string ModuleVersionOption = "--version";
    private const string ModuleScopeOption = "--scope";
    private const string ModuleForceOption = "--force";
    private const string ModuleScopeLocalValue = "local";
    private const string ModuleScopeGlobalValue = "global";
    private const string NoCheckOption = "--nocheck";
    private const string NoCheckAliasOption = "--no-check";
    private const string PowerShellGalleryApiBaseUri = "https://www.powershellgallery.com/api/v2";
    private static readonly Regex ModuleVersionRegex = MyRegex1();
    private static readonly Regex ModulePrereleaseRegex = MyRegex2();
    private static readonly HttpClient GalleryHttpClient = CreateGalleryHttpClient();
    private static readonly string[] ServiceBundleModuleExclusionPatterns =
    [
        "lib/runtimes/*",
        "lib/net8.0/*",
        "lib/Microsoft.CodeAnalysis/4*/*",
    ];
    private enum CommandMode
    {
        Run,
        ModuleInstall,
        ModuleUpdate,
        ModuleRemove,
        ModuleInfo,
        ServiceInstall,
        ServiceRemove,
        ServiceStart,
        ServiceStop,
        ServiceQuery,
    }

    private enum ModuleCommandAction
    {
        Install,
        Update,
        Remove,
    }

    private enum ModuleStorageScope
    {
        Local,
        Global,
    }

    private sealed record ParsedCommand(
        CommandMode Mode,
        string ScriptPath,
        string[] ScriptArguments,
        string? KestrunFolder,
        string? KestrunManifestPath,
        string? ServiceName,
        string? ServiceLogPath,
        string? ModuleVersion,
        ModuleStorageScope ModuleScope,
        bool ModuleForce,
        string? ServiceContentRoot,
        string? ServiceDeploymentRoot);

    private sealed record ServiceHostOptions(
        string ServiceName,
        string ScriptPath,
        string[] ScriptArguments,
        string? KestrunFolder,
        string? KestrunManifestPath,
        string? ServiceLogPath);

    private sealed record ServiceRegisterOptions(
        string ServiceName,
        string ServiceHostExecutablePath,
        string RunnerExecutablePath,
        string ScriptPath,
        string ModuleManifestPath,
        string[] ScriptArguments,
        string? ServiceLogPath);

    private sealed record GlobalOptions(
        string[] CommandArgs,
        bool SkipGalleryCheck);

    private sealed record InstalledModuleRecord(
        string Version,
        string ManifestPath);

    private sealed record ServiceBundleLayout(
        string RootPath,
        string RuntimeExecutablePath,
        string ServiceHostExecutablePath,
        string ScriptPath,
        string ModuleManifestPath);

    private sealed record ResolvedServiceScriptSource(
        string FullScriptPath,
        string? FullContentRoot,
        string RelativeScriptPath);

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

        if (TryParseServiceRegisterArguments(args, out var serviceRegisterOptions, out var serviceRegisterError))
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("Internal service registration mode is only supported on Windows.");
                return 1;
            }

            return RegisterWindowsService(serviceRegisterOptions!);
        }

        if (!string.IsNullOrWhiteSpace(serviceRegisterError))
        {
            Console.Error.WriteLine(serviceRegisterError);
            return 2;
        }

        var globalOptions = ParseGlobalOptions(args);
        var commandArgs = globalOptions.CommandArgs;

        if (TryHandleMetaCommands(commandArgs, out var metaExitCode))
        {
            return metaExitCode;
        }

        if (!TryParseArguments(commandArgs, out var parsedCommand, out var parseError))
        {
            Console.Error.WriteLine(parseError);
            PrintUsage();
            return 2;
        }

        if (parsedCommand.Mode == CommandMode.ServiceInstall)
        {
            return InstallService(parsedCommand, globalOptions.SkipGalleryCheck);
        }

        if (parsedCommand.Mode is CommandMode.ModuleInstall or CommandMode.ModuleUpdate or CommandMode.ModuleRemove or CommandMode.ModuleInfo)
        {
            if (parsedCommand.Mode is CommandMode.ModuleInstall or CommandMode.ModuleUpdate or CommandMode.ModuleRemove
                && parsedCommand.ModuleScope == ModuleStorageScope.Global
                && OperatingSystem.IsWindows()
                && !IsWindowsAdministrator())
            {
                return RelaunchElevatedOnWindows(args);
            }
            // For non-Windows OSes, attempt module management without elevation and rely on error handling for permission issues.
            return ManageModuleCommand(parsedCommand);
        }

        if (parsedCommand.Mode == CommandMode.ServiceRemove)
        {
            if (OperatingSystem.IsWindows() && !IsWindowsAdministrator())
            {
                return !TryPreflightWindowsServiceRemove(parsedCommand, out var preflightExitCode)
                    ? preflightExitCode
                    : RelaunchElevatedOnWindows(args);
            }
            // For non-Windows OSes, attempt removal without elevation and rely on error handling for permission issues or missing services.
            return RemoveService(parsedCommand);
        }

        if (parsedCommand.Mode == CommandMode.ServiceStart)
        {
            if (OperatingSystem.IsWindows() && !IsWindowsAdministrator())
            {
                return !TryPreflightWindowsServiceControl(parsedCommand, out var preflightExitCode)
                    ? preflightExitCode
                    : RelaunchElevatedOnWindows(args);
            }
            // For non-Windows OSes, attempt start without elevation and rely on error handling for permission issues or missing services.
            return StartService(parsedCommand);
        }

        if (parsedCommand.Mode == CommandMode.ServiceStop)
        {
            if (OperatingSystem.IsWindows() && !IsWindowsAdministrator())
            {
                return !TryPreflightWindowsServiceControl(parsedCommand, out var preflightExitCode)
                    ? preflightExitCode
                    : RelaunchElevatedOnWindows(args);
            }
            // For non-Windows OSes, attempt stop without elevation and rely on error handling for permission issues or missing services.
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
            WriteModuleNotFoundMessage(kestrunManifestPath, kestrunFolder, Console.Error.WriteLine);
            return 3;
        }

        if (!globalOptions.SkipGalleryCheck)
        {
            WarnIfNewerGalleryVersionExists(moduleManifestPath);
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
    /// Parses internal Windows service registration arguments when present.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="options">Parsed registration options when successful.</param>
    /// <param name="error">Parse error when registration mode is requested but invalid.</param>
    /// <returns>True when service registration mode is recognized and parsed.</returns>
    private static bool TryParseServiceRegisterArguments(string[] args, out ServiceRegisterOptions? options, out string? error)
    {
        options = null;
        error = null;

        if (args.Length == 0 || !string.Equals(args[0], "--service-register", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var index = 1;
        var serviceName = string.Empty;
        var serviceHostExecutablePath = string.Empty;
        var runnerExecutablePath = string.Empty;
        var scriptPath = string.Empty;
        var moduleManifestPath = string.Empty;
        var scriptArgs = Array.Empty<string>();
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

            if (current is "--service-host-exe" && index + 1 < args.Length)
            {
                serviceHostExecutablePath = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--runner-exe" && index + 1 < args.Length)
            {
                runnerExecutablePath = args[index + 1];
                index += 2;
                continue;
            }

            // Backward compatibility for older elevated registration invocations.
            if (current is "--exe" && index + 1 < args.Length)
            {
                serviceHostExecutablePath = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--script" && index + 1 < args.Length)
            {
                scriptPath = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--kestrun-manifest" or "-m" && index + 1 < args.Length)
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
                scriptArgs = [.. args.Skip(index + 1)];
                break;
            }

            error = $"Unknown service register option: {current}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            error = "Missing --name for internal service registration mode.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(serviceHostExecutablePath))
        {
            error = "Missing --service-host-exe for internal service registration mode.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(runnerExecutablePath))
        {
            runnerExecutablePath = serviceHostExecutablePath;
        }

        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            error = "Missing --script for internal service registration mode.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(moduleManifestPath))
        {
            error = "Missing --kestrun-manifest for internal service registration mode.";
            return false;
        }

        options = new ServiceRegisterOptions(serviceName, serviceHostExecutablePath, runnerExecutablePath, scriptPath, moduleManifestPath, scriptArgs, serviceLogPath);
        return true;
    }

    /// <summary>
    /// Runs KestrunTool under Windows Service Control Manager.
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

        ServiceBase.Run(new KestrunToolWindowsService(options));
        return 0;
    }

    /// <summary>
    /// Windows service adapter that runs a KestrunTool script under SCM lifecycle callbacks.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private sealed class KestrunToolWindowsService : ServiceBase
    {
        private readonly ServiceHostOptions _options;
        private readonly string _bootstrapLogDirectory;
        private readonly string _bootstrapLogPath;
        private Task? _runTask;

        /// <summary>
        /// Initializes the service host wrapper.
        /// </summary>
        /// <param name="options">Service host options.</param>
        public KestrunToolWindowsService(ServiceHostOptions options)
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
                // Run the managed stop on a background thread and wait with a timeout
                var stopTask = Task.Run(async () => await RequestManagedStopAsync().ConfigureAwait(false));
                if (!stopTask.Wait(TimeSpan.FromSeconds(30)))
                {
                    WriteBootstrapLog($"Service '{ServiceName}' managed stop did not complete within the timeout.");
                }
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
                WriteModuleNotFoundMessage(_options.KestrunManifestPath, _options.KestrunFolder, WriteBootstrapLog);
                return 3;
            }

            return ExecuteScript(scriptPath, _options.ScriptArguments, moduleManifestPath);
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
            => RunnerRuntime.ResolveBootstrapLogPath(configuredPath, "kestrun-tool-service.log");
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

        if (!TryResolveServiceScriptSource(command, out var scriptSource, out var scriptError))
        {
            Console.Error.WriteLine(scriptError);
            exitCode = 2;
            return false;
        }

        var moduleManifestPath = LocateModuleManifest(command.KestrunManifestPath, command.KestrunFolder);
        if (moduleManifestPath is null)
        {
            WriteModuleNotFoundMessage(command.KestrunManifestPath, command.KestrunFolder, Console.Error.WriteLine);
            exitCode = 3;
            return false;
        }

        if (!TryResolveServiceRuntimeExecutableFromModule(moduleManifestPath, out _, out var runtimeError))
        {
            Console.Error.WriteLine(runtimeError);
            exitCode = 1;
            return false;
        }

        // Do not hard-fail preflight on bundled modules payload resolution.
        // Elevated relaunch can run under a different working-directory/layout context,
        // so definitive payload validation is performed during actual bundle preparation.

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

        // A non-zero exit without a clear not-found signal is likely permission-related.
        return true;
    }

    /// <summary>
    /// Relaunches the current executable with UAC elevation on Windows.
    /// </summary>
    /// <param name="args">Original command-line arguments.</param>
    /// <param name="exePath">Optional executable path to launch. When null, the current process executable will be used.</param>
    /// <returns>Exit code from the elevated child process or an error code.</returns>
    [SupportedOSPlatform("windows")]
    private static int RelaunchElevatedOnWindows(IReadOnlyList<string> args, string? exePath = null)
    {
        exePath ??= Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            Console.Error.WriteLine("Unable to resolve KestrunTool executable path for elevation.");
            return 1;
        }

        Console.Error.WriteLine("Administrator rights are required. Requesting elevation...");

        var relaunchArgs = BuildElevatedRelaunchArguments(exePath, args);

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = string.Join(" ", relaunchArgs.Select(EscapeWindowsCommandLineArgument)),
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
    /// Builds argument tokens for elevated relaunch scenarios.
    /// </summary>
    /// <param name="executablePath">Current process executable path.</param>
    /// <param name="args">Original command-line arguments.</param>
    /// <returns>Argument token list for elevated invocation.</returns>
    private static IReadOnlyList<string> BuildElevatedRelaunchArguments(string executablePath, IReadOnlyList<string> args)
    {
        if (!IsDotnetHostExecutable(executablePath))
        {
            return [.. args];
        }

        var assemblyPath = typeof(Program).Assembly.Location;
        if (!string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(assemblyPath))
        {
            var elevatedArgs = new List<string>(args.Count + 1)
            {
                Path.GetFullPath(assemblyPath),
            };

            elevatedArgs.AddRange(args);
            return elevatedArgs;
        }

        // Fallback path when assembly location is unavailable.
        var fallbackArgs = new List<string>(args.Count + 1)
        {
            ProductName,
        };

        fallbackArgs.AddRange(args);
        return fallbackArgs;
    }

    /// <summary>
    /// Determines whether the given executable path is the dotnet host executable.
    /// </summary>
    /// <param name="executablePath">Executable path to inspect.</param>
    /// <returns>True when the executable is dotnet host.</returns>
    private static bool IsDotnetHostExecutable(string executablePath)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(executablePath);
        return string.Equals(fileNameWithoutExtension, "dotnet", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Installs a service/daemon entry that runs the target script through the KestrunTool executable.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <returns>Process exit code.</returns>
    private static int InstallService(ParsedCommand command, bool skipGalleryCheck)
    {
        if (string.IsNullOrWhiteSpace(command.ServiceName))
        {
            Console.Error.WriteLine("Service name is required. Use --name <value>.");
            return 2;
        }

        var serviceName = command.ServiceName;

        if (!TryResolveServiceScriptSource(command, out var scriptSource, out var scriptError))
        {
            Console.Error.WriteLine(scriptError);
            return 2;
        }

        var scriptPath = scriptSource.FullScriptPath;

        var moduleManifestPath = LocateModuleManifest(command.KestrunManifestPath, command.KestrunFolder);
        if (moduleManifestPath is null)
        {
            WriteModuleNotFoundMessage(command.KestrunManifestPath, command.KestrunFolder, Console.Error.WriteLine);
            return 3;
        }

        if (OperatingSystem.IsWindows() && !TryPreflightWindowsServiceInstall(command, out var preflightExitCode))
        {
            return preflightExitCode;
        }

        if (!skipGalleryCheck)
        {
            WarnIfNewerGalleryVersionExists(moduleManifestPath, command.ServiceLogPath);
        }

        if (!TryPrepareServiceBundle(serviceName, scriptPath, moduleManifestPath, scriptSource.FullContentRoot, scriptSource.RelativeScriptPath, out var serviceBundle, out var bundleError, command.ServiceDeploymentRoot))
        {
            Console.Error.WriteLine(bundleError);
            return 1;
        }

        if (serviceBundle is null)
        {
            Console.Error.WriteLine("Service bundle preparation failed.");
            return 1;
        }

        var daemonArgs = BuildDaemonHostArgumentsForService(
            serviceName,
            serviceBundle.ServiceHostExecutablePath,
            serviceBundle.RuntimeExecutablePath,
            serviceBundle.ScriptPath,
            serviceBundle.ModuleManifestPath,
            command.ScriptArguments,
            command.ServiceLogPath);
        var workingDirectory = Path.GetDirectoryName(serviceBundle.ScriptPath) ?? Environment.CurrentDirectory;

        if (OperatingSystem.IsWindows())
        {
            return InstallWindowsService(
                command,
                serviceBundle.ServiceHostExecutablePath,
                serviceBundle.RuntimeExecutablePath,
                serviceBundle.ScriptPath,
                serviceBundle.ModuleManifestPath);
        }

        if (OperatingSystem.IsLinux())
        {
            return InstallLinuxUserDaemon(serviceName, serviceBundle.ServiceHostExecutablePath, daemonArgs, workingDirectory);
        }

        if (OperatingSystem.IsMacOS())
        {
            return InstallMacLaunchAgent(serviceName, serviceBundle.ServiceHostExecutablePath, daemonArgs, workingDirectory);
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

        var serviceName = command.ServiceName;

        int result;
        if (OperatingSystem.IsWindows())
        {
            result = RemoveWindowsService(command);
            if (result == 0)
            {
                TryRemoveServiceBundle(serviceName, command.ServiceDeploymentRoot);
            }

            return result;
        }

        if (OperatingSystem.IsLinux())
        {
            result = RemoveLinuxUserDaemon(serviceName);
            if (result == 0)
            {
                TryRemoveServiceBundle(serviceName, command.ServiceDeploymentRoot);
            }

            return result;
        }

        if (OperatingSystem.IsMacOS())
        {
            result = RemoveMacLaunchAgent(serviceName);
            if (result == 0)
            {
                TryRemoveServiceBundle(serviceName, command.ServiceDeploymentRoot);
            }

            return result;
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

        var serviceName = command.ServiceName;

        if (OperatingSystem.IsWindows())
        {
            return StartWindowsService(serviceName, command.ServiceLogPath);
        }

        if (OperatingSystem.IsLinux())
        {
            return StartLinuxUserDaemon(serviceName);
        }

        if (OperatingSystem.IsMacOS())
        {
            return StartMacLaunchAgent(serviceName);
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

        var serviceName = command.ServiceName;

        if (OperatingSystem.IsWindows())
        {
            return StopWindowsService(serviceName, command.ServiceLogPath);
        }

        if (OperatingSystem.IsLinux())
        {
            return StopLinuxUserDaemon(serviceName);
        }

        if (OperatingSystem.IsMacOS())
        {
            return StopMacLaunchAgent(serviceName);
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

        var serviceName = command.ServiceName;

        if (OperatingSystem.IsWindows())
        {
            return QueryWindowsService(serviceName, command.ServiceLogPath);
        }

        if (OperatingSystem.IsLinux())
        {
            return QueryLinuxUserDaemon(serviceName);
        }

        if (OperatingSystem.IsMacOS())
        {
            return QueryMacLaunchAgent(serviceName);
        }

        Console.Error.WriteLine("Service query is not supported on this OS.");
        return 1;
    }

    /// <summary>
    /// Builds KestrunTool command-line arguments used by installed services/daemons.
    /// </summary>
    /// <param name="scriptPath">Absolute script path to execute.</param>
    /// <param name="scriptArguments">Script arguments for the run command.</param>
    /// <param name="kestrunFolder">Optional folder containing Kestrun module manifest.</param>
    /// <param name="kestrunManifestPath">Optional explicit Kestrun module manifest path.</param>
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
    /// Builds command-line arguments for daemon host registration.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="serviceHostExecutablePath">Service-host executable path.</param>
    /// <param name="runnerExecutablePath">Runner executable path.</param>
    /// <param name="scriptPath">Absolute script path.</param>
    /// <param name="moduleManifestPath">Absolute module manifest path.</param>
    /// <param name="scriptArguments">Script arguments for run mode.</param>
    /// <param name="serviceLogPath">Optional service log path.</param>
    /// <returns>Ordered daemon-host argument tokens.</returns>
    private static IReadOnlyList<string> BuildDaemonHostArgumentsForService(
        string serviceName,
        string serviceHostExecutablePath,
        string runnerExecutablePath,
        string scriptPath,
        string moduleManifestPath,
        IReadOnlyList<string> scriptArguments,
        string? serviceLogPath)
    {
        if (UsesDedicatedServiceHostExecutable(serviceHostExecutablePath))
        {
            return BuildDedicatedServiceHostArguments(serviceName, runnerExecutablePath, scriptPath, moduleManifestPath, scriptArguments, serviceLogPath);
        }
        // Fallback to generic runner invocation when the service host is not the dedicated one.
        return BuildRunnerArgumentsForService(scriptPath, scriptArguments, null, moduleManifestPath);
    }

    /// <summary>
    /// Installs a Windows service using sc.exe.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <param name="serviceHostExecutablePath">Service host executable path.</param>
    /// <param name="runnerExecutablePath">Runner executable path.</param>
    /// <param name="scriptPath">Bundled script path.</param>
    /// <param name="moduleManifestPath">Bundled module manifest path.</param>
    /// <returns>Process exit code.</returns>
    [SupportedOSPlatform("windows")]
    private static int InstallWindowsService(
        ParsedCommand command,
        string serviceHostExecutablePath,
        string runnerExecutablePath,
        string scriptPath,
        string moduleManifestPath)
    {
        var serviceName = command.ServiceName!;
        if (!IsWindowsAdministrator())
        {
            var relaunchArgs = BuildWindowsServiceRegisterArguments(command, serviceHostExecutablePath, runnerExecutablePath, scriptPath, moduleManifestPath);
            return RelaunchElevatedOnWindows(relaunchArgs);
        }

        var createResult = CreateWindowsServiceRegistration(
            serviceName,
            Path.GetFullPath(serviceHostExecutablePath),
            Path.GetFullPath(runnerExecutablePath),
            Path.GetFullPath(scriptPath),
            Path.GetFullPath(moduleManifestPath),
            command.ScriptArguments,
            command.ServiceLogPath);

        if (createResult.ExitCode != 0)
        {
            Console.Error.WriteLine(createResult.Error);
            return createResult.ExitCode;
        }

        WriteServiceOperationLog($"Service '{serviceName}' install operation completed.", command.ServiceLogPath, serviceName);

        Console.WriteLine($"Installed Windows service '{serviceName}' (not started).");
        return 0;
    }

    /// <summary>
    /// Registers a Windows service using pre-staged runtime/module/script paths.
    /// </summary>
    /// <param name="options">Parsed service registration options.</param>
    /// <returns>Process exit code.</returns>
    [SupportedOSPlatform("windows")]
    private static int RegisterWindowsService(ServiceRegisterOptions options)
    {
        var serviceName = options.ServiceName;
        var createResult = CreateWindowsServiceRegistration(
            serviceName,
            Path.GetFullPath(options.ServiceHostExecutablePath),
            Path.GetFullPath(options.RunnerExecutablePath),
            Path.GetFullPath(options.ScriptPath),
            Path.GetFullPath(options.ModuleManifestPath),
            options.ScriptArguments,
            options.ServiceLogPath);

        if (createResult.ExitCode != 0)
        {
            Console.Error.WriteLine(createResult.Error);
            return createResult.ExitCode;
        }

        WriteServiceOperationLog($"Service '{serviceName}' install operation completed.", options.ServiceLogPath, serviceName);

        Console.WriteLine($"Installed Windows service '{serviceName}' (not started).");
        return 0;
    }

    /// <summary>
    /// Creates a Windows service registration using sc.exe.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="serviceHostExecutablePath">Service-host executable path.</param>
    /// <param name="runnerExecutablePath">Runner executable path.</param>
    /// <param name="scriptPath">Absolute script path.</param>
    /// <param name="moduleManifestPath">Absolute module manifest path.</param>
    /// <param name="scriptArguments">Script arguments for service-host mode.</param>
    /// <param name="serviceLogPath">Optional service log path.</param>
    /// <returns>Process result from sc.exe create.</returns>
    private static ProcessResult CreateWindowsServiceRegistration(
        string serviceName,
        string serviceHostExecutablePath,
        string runnerExecutablePath,
        string scriptPath,
        string moduleManifestPath,
        IReadOnlyList<string> scriptArguments,
        string? serviceLogPath)
    {
        var hostArgs = UsesDedicatedServiceHostExecutable(serviceHostExecutablePath)
            ? BuildDedicatedServiceHostArguments(serviceName, runnerExecutablePath, scriptPath, moduleManifestPath, scriptArguments, serviceLogPath)
            : BuildWindowsServiceHostArgumentsCore(serviceName, scriptPath, moduleManifestPath, scriptArguments, serviceLogPath);

        var imagePath = BuildWindowsCommandLine(serviceHostExecutablePath, hostArgs);
        return RunProcess(
            "sc.exe",
            ["create", serviceName, "start=", "auto", "binPath=", imagePath, "DisplayName=", serviceName]);
    }

    /// <summary>
    /// Determines whether a path refers to the dedicated service-host executable.
    /// </summary>
    /// <param name="executablePath">Executable path.</param>
    /// <returns>True when the executable is the dedicated service host.</returns>
    private static bool UsesDedicatedServiceHostExecutable(string executablePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(executablePath);
        return string.Equals(fileName, "kestrun-service-host", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "Kestrun.ServiceHost", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds elevated relaunch arguments for internal Windows service registration.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <param name="executablePath">Executable path.</param>
    /// <param name="scriptPath">Absolute script path.</param>
    /// <param name="moduleManifestPath">Manifest path staged for service runtime.</param>
    /// <returns>Ordered argument tokens.</returns>
    private static IReadOnlyList<string> BuildWindowsServiceRegisterArguments(
        ParsedCommand command,
        string serviceHostExecutablePath,
        string runnerExecutablePath,
        string scriptPath,
        string moduleManifestPath)
    {
        var arguments = new List<string>(16 + command.ScriptArguments.Length)
        {
            "--service-register",
            "--name",
            command.ServiceName!,
            "--service-host-exe",
            Path.GetFullPath(serviceHostExecutablePath),
            "--runner-exe",
            Path.GetFullPath(runnerExecutablePath),
            "--script",
            Path.GetFullPath(scriptPath),
            "--kestrun-manifest",
            Path.GetFullPath(moduleManifestPath),
        };

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
    /// Builds arguments for the dedicated service-host executable.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="runnerExecutablePath">Runner executable path.</param>
    /// <param name="scriptPath">Absolute script path.</param>
    /// <param name="moduleManifestPath">Manifest path staged for service runtime.</param>
    /// <param name="scriptArguments">Script arguments forwarded to run mode.</param>
    /// <param name="serviceLogPath">Optional service log path.</param>
    /// <returns>Ordered argument tokens.</returns>
    private static IReadOnlyList<string> BuildDedicatedServiceHostArguments(
        string serviceName,
        string runnerExecutablePath,
        string scriptPath,
        string moduleManifestPath,
        IReadOnlyList<string> scriptArguments,
        string? serviceLogPath)
    {
        var arguments = new List<string>(14 + scriptArguments.Count)
        {
            "--name",
            serviceName,
            "--runner-exe",
            Path.GetFullPath(runnerExecutablePath),
            "--script",
            scriptPath,
            "--kestrun-manifest",
            Path.GetFullPath(moduleManifestPath),
        };

        if (!string.IsNullOrWhiteSpace(serviceLogPath))
        {
            arguments.Add("--service-log-path");
            arguments.Add(Path.GetFullPath(serviceLogPath));
        }

        if (scriptArguments.Count > 0)
        {
            arguments.Add("--arguments");
            arguments.AddRange(scriptArguments);
        }

        return arguments;
    }

    /// <summary>
    /// Builds internal service-host arguments used for Windows SCM registration.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="scriptPath">Absolute script path.</param>
    /// <param name="moduleManifestPath">Manifest path staged for service runtime.</param>
    /// <param name="scriptArguments">Script arguments forwarded to run mode.</param>
    /// <param name="serviceLogPath">Optional service log path.</param>
    /// <returns>Ordered argument tokens.</returns>
    private static IReadOnlyList<string> BuildWindowsServiceHostArgumentsCore(
        string serviceName,
        string scriptPath,
        string moduleManifestPath,
        IReadOnlyList<string> scriptArguments,
        string? serviceLogPath)
    {
        var arguments = new List<string>(12 + scriptArguments.Count)
        {
            "--service-host",
            "--name",
            serviceName,
            "--script",
            scriptPath,
            "--kestrun-manifest",
            Path.GetFullPath(moduleManifestPath),
        };

        if (!string.IsNullOrWhiteSpace(serviceLogPath))
        {
            arguments.Add("--service-log-path");
            arguments.Add(Path.GetFullPath(serviceLogPath));
        }

        if (scriptArguments.Count > 0)
        {
            arguments.Add("--arguments");
            arguments.AddRange(scriptArguments);
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
        var defaultFileName = "kestrun-tool-service.log";
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Kestrun",
            "logs",
            defaultFileName);

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return NormalizeServiceLogPath(configuredPath, defaultFileName);
        }
        // When no explicit path is configured, attempt to discover a --service-log-path from the service config for better log correlation with the service instance.
        return OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(serviceName)
            && TryGetWindowsServiceLogPath(serviceName, out var discoveredPath)
            && !string.IsNullOrWhiteSpace(discoveredPath)
            ? discoveredPath
            : defaultPath;
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
        var match = MyRegex().Match(text);

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

        logPath = NormalizeServiceLogPath(rawPath, defaultFileName: "kestrun-tool-service.log");
        return true;
    }

    /// <summary>
    /// Resolves the runtime executable path from a Kestrun module manifest directory.
    /// </summary>
    /// <param name="moduleManifestPath">Path to Kestrun.psd1.</param>
    /// <param name="runtimeExecutablePath">Resolved runtime executable path.</param>
    /// <param name="error">Error details when resolution fails.</param>
    /// <returns>True when a runtime executable is available for the current OS/architecture.</returns>
    private static bool TryResolveServiceRuntimeExecutableFromModule(string moduleManifestPath, out string runtimeExecutablePath, out string error)
    {
        runtimeExecutablePath = string.Empty;
        error = string.Empty;

        var fullManifestPath = Path.GetFullPath(moduleManifestPath);
        var moduleRoot = Path.GetDirectoryName(fullManifestPath);
        if (string.IsNullOrWhiteSpace(moduleRoot) || !Directory.Exists(moduleRoot))
        {
            error = $"Unable to resolve module root from manifest path: {fullManifestPath}";
            return false;
        }

        if (!TryGetServiceRuntimeRid(out var runtimeRid, out var ridError))
        {
            error = ridError;
            return false;
        }

        var runtimeBinaryName = OperatingSystem.IsWindows() ? "kestrun.exe" : "kestrun";
        foreach (var candidate in EnumerateServiceRuntimeExecutableCandidates(moduleRoot, runtimeRid, runtimeBinaryName))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            runtimeExecutablePath = Path.GetFullPath(candidate);
            return true;
        }

        // Module distributions may intentionally omit tool runtime binaries.
        // In that case, reuse the dedicated service-host payload as the runner executable.
        if (TryResolveDedicatedServiceHostExecutableFromToolDistribution(out var serviceHostExecutablePath))
        {
            runtimeExecutablePath = serviceHostExecutablePath;
            return true;
        }

        // Final fallback for non-standard layouts: use the current process executable path when available.
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath) && File.Exists(Environment.ProcessPath))
        {
            runtimeExecutablePath = Path.GetFullPath(Environment.ProcessPath);
            return true;
        }

        error = $"Unable to locate service runner executable for '{runtimeRid}'. Checked module path '{moduleRoot}', fallback runtime locations, bundled service-host payload, and current process path. Reinstall or update Kestrun.Tool, or run '{ProductName} module install' to refresh module assets.";
        return false;
    }

    /// <summary>
    /// Enumerates candidate runtime executable paths for service bundle staging.
    /// </summary>
    /// <param name="moduleRoot">Resolved module root path.</param>
    /// <param name="runtimeRid">Runtime identifier segment (for example, win-x64).</param>
    /// <param name="runtimeBinaryName">Runtime executable file name.</param>
    /// <returns>Candidate runtime executable paths in resolution priority order.</returns>
    private static IEnumerable<string> EnumerateServiceRuntimeExecutableCandidates(string moduleRoot, string runtimeRid, string runtimeBinaryName)
    {
        var candidates = new List<string>
        {
            Path.Combine(moduleRoot, "runtimes", runtimeRid, runtimeBinaryName),
            Path.Combine(moduleRoot, "lib", "runtimes", runtimeRid, runtimeBinaryName),
            Path.Combine(GetExecutableDirectory(), "runtimes", runtimeRid, runtimeBinaryName),
            Path.Combine(GetExecutableDirectory(), "lib", "runtimes", runtimeRid, runtimeBinaryName),
        };

        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        var executableDirectory = GetExecutableDirectory();
        if (!string.Equals(baseDirectory, executableDirectory, StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(Path.Combine(baseDirectory, "runtimes", runtimeRid, runtimeBinaryName));
            candidates.Add(Path.Combine(baseDirectory, "lib", "runtimes", runtimeRid, runtimeBinaryName));
        }

        foreach (var parent in EnumerateDirectoryAndParents(Environment.CurrentDirectory))
        {
            candidates.Add(Path.Combine(parent, "src", "PowerShell", "Kestrun", "runtimes", runtimeRid, runtimeBinaryName));
            candidates.Add(Path.Combine(parent, "src", "PowerShell", "Kestrun", "lib", "runtimes", runtimeRid, runtimeBinaryName));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return candidate;
        }
    }

    /// <summary>
    /// Tries to resolve a dedicated service-host executable path from the Kestrun.Tool distribution.
    /// </summary>
    /// <param name="serviceHostExecutablePath">Resolved service-host executable path when available.</param>
    /// <returns>True when a dedicated service-host executable is available.</returns>
    private static bool TryResolveDedicatedServiceHostExecutableFromToolDistribution(out string serviceHostExecutablePath)
    {
        serviceHostExecutablePath = string.Empty;
        if (!TryGetServiceRuntimeRid(out var runtimeRid, out _))
        {
            return false;
        }

        var hostBinaryName = OperatingSystem.IsWindows() ? "kestrun-service-host.exe" : "kestrun-service-host";
        foreach (var candidate in EnumerateDedicatedServiceHostCandidates(runtimeRid, hostBinaryName))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            serviceHostExecutablePath = Path.GetFullPath(candidate);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to resolve bundled PowerShell Modules payload from the Kestrun.Tool distribution.
    /// </summary>
    /// <param name="modulesPayloadPath">Resolved modules payload path when available.</param>
    /// <returns>True when the modules payload is available.</returns>
    private static bool TryResolvePowerShellModulesPayloadFromToolDistribution(out string modulesPayloadPath)
    {
        modulesPayloadPath = string.Empty;
        if (!TryGetServiceRuntimeRid(out var runtimeRid, out _))
        {
            return false;
        }

        foreach (var candidate in EnumeratePowerShellModulesPayloadCandidates(runtimeRid))
        {
            if (!Directory.Exists(candidate))
            {
                continue;
            }

            modulesPayloadPath = Path.GetFullPath(candidate);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Enumerates candidate service-host paths shipped with Kestrun.Tool for the target RID.
    /// </summary>
    /// <param name="runtimeRid">Runtime identifier segment (for example, win-x64).</param>
    /// <param name="hostBinaryName">Service-host executable file name.</param>
    /// <returns>Candidate service-host paths in resolution priority order.</returns>
    private static IEnumerable<string> EnumerateDedicatedServiceHostCandidates(string runtimeRid, string hostBinaryName)
    {
        var executableDirectory = GetExecutableDirectory();
        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        var candidates = new List<string>
        {
            Path.Combine(executableDirectory, "kestrun-service", runtimeRid, hostBinaryName),
            Path.Combine(baseDirectory, "kestrun-service", runtimeRid, hostBinaryName),
            Path.Combine(executableDirectory, runtimeRid, hostBinaryName),
            Path.Combine(baseDirectory, runtimeRid, hostBinaryName),
            Path.Combine(executableDirectory, "runtimes", runtimeRid, hostBinaryName),
            Path.Combine(baseDirectory, "runtimes", runtimeRid, hostBinaryName),
            Path.Combine(executableDirectory, hostBinaryName),
            Path.Combine(baseDirectory, hostBinaryName),
        };

        foreach (var parent in EnumerateDirectoryAndParents(Environment.CurrentDirectory))
        {
            candidates.Add(Path.Combine(parent, "src", "CSharp", "Kestrun.Tool", "kestrun-service", runtimeRid, hostBinaryName));
            candidates.Add(Path.Combine(parent, "artifacts", "Kestrun.Tool", $"{runtimeRid}-service-host", hostBinaryName));
            candidates.Add(Path.Combine(parent, "artifacts", "Kestrun.Tool", $"{runtimeRid}-service-host", OperatingSystem.IsWindows() ? "Kestrun.ServiceHost.exe" : "Kestrun.ServiceHost"));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return candidate;
        }
    }

    /// <summary>
    /// Enumerates candidate PowerShell Modules payload paths shipped with Kestrun.Tool for the target RID.
    /// </summary>
    /// <param name="runtimeRid">Runtime identifier segment (for example, win-x64).</param>
    /// <returns>Candidate modules payload paths in resolution priority order.</returns>
    private static IEnumerable<string> EnumeratePowerShellModulesPayloadCandidates(string runtimeRid)
    {
        var executableDirectory = GetExecutableDirectory();
        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        var candidates = new List<string>
        {
            Path.Combine(executableDirectory, "kestrun-service", runtimeRid, "Modules"),
            Path.Combine(baseDirectory, "kestrun-service", runtimeRid, "Modules"),
            Path.Combine(executableDirectory, runtimeRid, "Modules"),
            Path.Combine(baseDirectory, runtimeRid, "Modules"),
            Path.Combine(executableDirectory, "runtimes", runtimeRid, "Modules"),
            Path.Combine(baseDirectory, "runtimes", runtimeRid, "Modules"),
        };

        foreach (var parent in EnumerateDirectoryAndParents(Environment.CurrentDirectory))
        {
            candidates.Add(Path.Combine(parent, "src", "CSharp", "Kestrun.Tool", "kestrun-service", runtimeRid, "Modules"));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return candidate;
        }
    }

    /// <summary>
    /// Enumerates a directory and all parents up to the filesystem root.
    /// </summary>
    /// <param name="startDirectory">Starting directory path.</param>
    /// <returns>Normalized directory path sequence from leaf to root.</returns>
    private static IEnumerable<string> EnumerateDirectoryAndParents(string startDirectory)
    {
        var current = Path.GetFullPath(startDirectory);
        while (!string.IsNullOrWhiteSpace(current))
        {
            yield return current;

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }
    }

    /// <summary>
    /// Creates a per-service deployment bundle with runtime binary, module files, and the script entrypoint.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="sourceScriptPath">Source script path.</param>
    /// <param name="sourceModuleManifestPath">Source module manifest path.</param>
    /// <param name="serviceBundle">Created service bundle paths.</param>
    /// <param name="error">Error details when bundling fails.</param>
    /// <param name="deploymentRootOverride">Optional deployment root override for tests.</param>
    /// <returns>True when service bundle creation succeeds.</returns>
    private static bool TryPrepareServiceBundle(
        string serviceName,
        string sourceScriptPath,
        string sourceModuleManifestPath,
        string? sourceContentRoot,
        string relativeScriptPath,
        out ServiceBundleLayout? serviceBundle,
        out string error,
        string? deploymentRootOverride = null)
    {
        serviceBundle = null;
        error = string.Empty;

        var fullScriptPath = Path.GetFullPath(sourceScriptPath);
        if (!File.Exists(fullScriptPath))
        {
            error = $"Script file not found: {fullScriptPath}";
            return false;
        }

        var fullManifestPath = Path.GetFullPath(sourceModuleManifestPath);
        if (!File.Exists(fullManifestPath))
        {
            error = $"Kestrun manifest file not found: {fullManifestPath}";
            return false;
        }

        if (!TryResolveServiceRuntimeExecutableFromModule(fullManifestPath, out var runtimeExecutablePath, out var runtimeError))
        {
            error = runtimeError;
            return false;
        }

        var moduleRoot = Path.GetDirectoryName(fullManifestPath)!;
        var serviceDirectoryName = GetServiceDeploymentDirectoryName(serviceName);

        if (!TryResolveServiceDeploymentRoot(deploymentRootOverride, out var deploymentRoot, out var deploymentError))
        {
            error = deploymentError;
            return false;
        }

        var serviceRoot = Path.Combine(deploymentRoot, serviceDirectoryName);
        var runtimeDirectory = Path.Combine(serviceRoot, "runtime");
        var modulesDirectory = Path.Combine(serviceRoot, "Modules");
        var moduleDirectory = Path.Combine(modulesDirectory, "Kestrun");
        var scriptDirectory = Path.Combine(serviceRoot, "script");
        var showProgress = !Console.IsOutputRedirected;
        using var bundleProgress = showProgress
            ? new ConsoleProgressBar("Preparing service bundle", 5, FormatServiceBundleStepProgressDetail)
            : null;
        var completedBundleSteps = 0;
        bundleProgress?.Report(0);

        try
        {
            if (Directory.Exists(serviceRoot))
            {
                Directory.Delete(serviceRoot, recursive: true);
            }

            _ = Directory.CreateDirectory(runtimeDirectory);
            _ = Directory.CreateDirectory(modulesDirectory);
            _ = Directory.CreateDirectory(moduleDirectory);
            _ = Directory.CreateDirectory(scriptDirectory);
            completedBundleSteps++;
            bundleProgress?.Report(completedBundleSteps);

            var bundledRuntimePath = Path.Combine(runtimeDirectory, Path.GetFileName(runtimeExecutablePath));
            File.Copy(runtimeExecutablePath, bundledRuntimePath, overwrite: true);
            completedBundleSteps++;
            bundleProgress?.Report(completedBundleSteps);

            if (!TryResolveDedicatedServiceHostExecutableFromToolDistribution(out var serviceHostExecutablePath))
            {
                error = $"Unable to locate dedicated service host for current RID in Kestrun.Tool distribution. Expected '{(OperatingSystem.IsWindows() ? "kestrun-service-host.exe" : "kestrun-service-host")}' under 'kestrun-service/<rid>/'. Reinstall or update Kestrun.Tool.";
                return false;
            }

            var bundledServiceHostPath = Path.Combine(runtimeDirectory, Path.GetFileName(serviceHostExecutablePath));
            File.Copy(serviceHostExecutablePath, bundledServiceHostPath, overwrite: true);

            if (!TryResolvePowerShellModulesPayloadFromToolDistribution(out var toolModulesPayloadPath))
            {
                error = "Unable to locate bundled PowerShell Modules payload for current RID in Kestrun.Tool distribution. Expected payload under 'kestrun-service/<rid>/Modules/'. Reinstall or update Kestrun.Tool.";
                return false;
            }

            CopyDirectoryContents(
                toolModulesPayloadPath,
                modulesDirectory,
                showProgress,
                "Bundling service runtime modules",
                exclusionPatterns: null);
            completedBundleSteps++;
            bundleProgress?.Report(completedBundleSteps);

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                TryEnsureServiceRuntimeExecutablePermissions(bundledRuntimePath);
                if (!string.Equals(bundledRuntimePath, bundledServiceHostPath, StringComparison.OrdinalIgnoreCase))
                {
                    TryEnsureServiceRuntimeExecutablePermissions(bundledServiceHostPath);
                }
            }

            CopyDirectoryContents(
                moduleRoot,
                moduleDirectory,
                showProgress,
                "Bundling module files",
                ServiceBundleModuleExclusionPatterns);
            completedBundleSteps++;
            bundleProgress?.Report(completedBundleSteps);

            var bundledManifestPath = Path.Combine(moduleDirectory, Path.GetFileName(fullManifestPath));
            if (!File.Exists(bundledManifestPath))
            {
                error = $"Service bundle copy did not include module manifest: {bundledManifestPath}";
                return false;
            }

            var bundledScriptPath = Path.Combine(scriptDirectory, relativeScriptPath.Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(sourceContentRoot))
            {
                var bundledScriptDirectory = Path.GetDirectoryName(bundledScriptPath);
                if (!string.IsNullOrWhiteSpace(bundledScriptDirectory))
                {
                    _ = Directory.CreateDirectory(bundledScriptDirectory);
                }

                File.Copy(fullScriptPath, bundledScriptPath, overwrite: true);
            }
            else
            {
                CopyDirectoryContents(
                    sourceContentRoot,
                    scriptDirectory,
                    showProgress,
                    "Bundling service script folder",
                    exclusionPatterns: null);
            }

            if (!File.Exists(bundledScriptPath))
            {
                error = $"Service bundle copy did not include script: {bundledScriptPath}";
                return false;
            }

            completedBundleSteps++;
            bundleProgress?.Report(completedBundleSteps);
            bundleProgress?.Complete(completedBundleSteps);

            serviceBundle = new ServiceBundleLayout(
                Path.GetFullPath(serviceRoot),
                Path.GetFullPath(bundledRuntimePath),
                Path.GetFullPath(bundledServiceHostPath),
                Path.GetFullPath(bundledScriptPath),
                Path.GetFullPath(bundledManifestPath));
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to prepare service bundle at '{serviceRoot}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Resolves service-install script source, including optional content-root semantics.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <param name="scriptSource">Resolved script source details.</param>
    /// <param name="error">Error details when validation fails.</param>
    /// <returns>True when script source is valid and exists.</returns>
    private static bool TryResolveServiceScriptSource(ParsedCommand command, out ResolvedServiceScriptSource scriptSource, out string error)
    {
        scriptSource = new ResolvedServiceScriptSource(string.Empty, null, string.Empty);
        error = string.Empty;

        var requestedScriptPath = string.IsNullOrWhiteSpace(command.ScriptPath)
            ? DefaultScriptFileName
            : command.ScriptPath;

        if (string.IsNullOrWhiteSpace(command.ServiceContentRoot))
        {
            var fullScriptPath = Path.GetFullPath(requestedScriptPath);
            if (!File.Exists(fullScriptPath))
            {
                error = $"Script file not found: {fullScriptPath}";
                return false;
            }

            scriptSource = new ResolvedServiceScriptSource(fullScriptPath, null, Path.GetFileName(fullScriptPath));
            return true;
        }

        var fullContentRoot = Path.GetFullPath(command.ServiceContentRoot);
        if (!Directory.Exists(fullContentRoot))
        {
            error = $"Service content root directory not found: {fullContentRoot}";
            return false;
        }

        if (Path.IsPathRooted(requestedScriptPath))
        {
            error = "When --content-root is specified, --script must be a relative path within that folder.";
            return false;
        }

        var fullScriptPathFromRoot = Path.GetFullPath(Path.Combine(fullContentRoot, requestedScriptPath));
        if (!IsPathWithinDirectory(fullScriptPathFromRoot, fullContentRoot))
        {
            error = $"Script path '{requestedScriptPath}' escapes the service content root '{fullContentRoot}'.";
            return false;
        }

        if (!File.Exists(fullScriptPathFromRoot))
        {
            error = $"Script file '{requestedScriptPath}' was not found under service content root '{fullContentRoot}'.";
            return false;
        }

        var relativeScriptPath = Path.GetRelativePath(fullContentRoot, fullScriptPathFromRoot);
        scriptSource = new ResolvedServiceScriptSource(fullScriptPathFromRoot, fullContentRoot, relativeScriptPath);
        return true;
    }

    /// <summary>
    /// Checks whether a path is inside (or equal to) a given directory.
    /// </summary>
    /// <param name="candidatePath">Candidate absolute path.</param>
    /// <param name="directoryPath">Directory absolute path.</param>
    /// <returns>True when candidate is within the directory tree.</returns>
    private static bool IsPathWithinDirectory(string candidatePath, string directoryPath)
    {
        var fullCandidate = Path.GetFullPath(candidatePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullDirectory = Path.GetFullPath(directoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return fullCandidate.Equals(fullDirectory, StringComparison.OrdinalIgnoreCase)
            || fullCandidate.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || fullCandidate.StartsWith(fullDirectory + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves the deployment root path used for service bundles.
    /// </summary>
    /// <param name="deploymentRootOverride">Optional explicit root path.</param>
    /// <param name="deploymentRoot">Resolved writable deployment root.</param>
    /// <param name="error">Error details when no writable root is available.</param>
    /// <returns>True when a writable deployment root is resolved.</returns>
    private static bool TryResolveServiceDeploymentRoot(string? deploymentRootOverride, out string deploymentRoot, out string error)
    {
        deploymentRoot = string.Empty;
        error = string.Empty;

        if (!string.IsNullOrWhiteSpace(deploymentRootOverride))
        {
            try
            {
                deploymentRoot = Path.GetFullPath(deploymentRootOverride);
                _ = Directory.CreateDirectory(deploymentRoot);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Unable to create deployment root '{deploymentRootOverride}': {ex.Message}";
                return false;
            }
        }

        var failures = new List<string>();
        foreach (var candidate in GetServiceDeploymentRootCandidates())
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            try
            {
                var fullCandidate = Path.GetFullPath(candidate);
                _ = Directory.CreateDirectory(fullCandidate);
                deploymentRoot = fullCandidate;
                return true;
            }
            catch (Exception ex)
            {
                failures.Add($"{candidate} ({ex.Message})");
            }
        }

        error = failures.Count == 0
            ? "Unable to resolve a writable service deployment root."
            : $"Unable to resolve a writable service deployment root. Attempted: {string.Join("; ", failures)}";
        return false;
    }

    /// <summary>
    /// Returns candidate deployment roots for service bundle storage.
    /// </summary>
    /// <returns>Candidate absolute or rooted paths in priority order.</returns>
    private static IEnumerable<string> GetServiceDeploymentRootCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Kestrun", "services");
            yield break;
        }

        if (OperatingSystem.IsLinux())
        {
            yield return "/var/kestrun/services";
            yield return "/usr/local/kestrun/services";

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                yield return Path.Combine(userProfile, ".local", "share", "kestrun", "services");
            }

            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return "/usr/local/kestrun/services";

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                yield return Path.Combine(userProfile, "Library", "Application Support", "Kestrun", "services");
            }

            yield break;
        }

        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kestrun", "services");
    }

    /// <summary>
    /// Removes service bundle directories for a given service name from known deployment roots.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    private static void TryRemoveServiceBundle(string serviceName, string? deploymentRootOverride = null)
    {
        var serviceDirectoryName = GetServiceDeploymentDirectoryName(serviceName);

        var candidateRoots = new List<string>();
        if (!string.IsNullOrWhiteSpace(deploymentRootOverride))
        {
            candidateRoots.Add(deploymentRootOverride);
        }

        candidateRoots.AddRange(GetServiceDeploymentRootCandidates());

        foreach (var candidateRoot in candidateRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(candidateRoot))
            {
                continue;
            }

            var serviceRoot = Path.Combine(candidateRoot, serviceDirectoryName);
            try
            {
                if (Directory.Exists(serviceRoot))
                {
                    Directory.Delete(serviceRoot, recursive: true);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to remove service bundle '{serviceRoot}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Returns a filesystem-safe directory name for service deployment folders.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <returns>Sanitized directory name.</returns>
    private static string GetServiceDeploymentDirectoryName(string serviceName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(serviceName.Length);
        foreach (var ch in serviceName)
        {
            if (char.IsControl(ch) || ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar || invalid.Contains(ch))
            {
                _ = builder.Append('-');
                continue;
            }

            _ = builder.Append(ch);
        }

        var sanitized = builder.ToString().Trim().Trim('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "service" : sanitized;
    }

    /// <summary>
    /// Resolves runtime RID segment for service runtime payloads.
    /// </summary>
    /// <param name="runtimeRid">RID segment (for example win-x64).</param>
    /// <param name="error">Error details when runtime architecture is unsupported.</param>
    /// <returns>True when runtime RID can be resolved.</returns>
    private static bool TryGetServiceRuntimeRid(out string runtimeRid, out string error)
    {
        runtimeRid = string.Empty;
        error = string.Empty;

        var osPrefix = OperatingSystem.IsWindows()
            ? "win"
            : OperatingSystem.IsLinux()
                ? "linux"
                : OperatingSystem.IsMacOS()
                    ? "osx"
                    : string.Empty;

        if (string.IsNullOrWhiteSpace(osPrefix))
        {
            error = "Service runtime bundling is not supported on this operating system.";
            return false;
        }

        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => string.Empty,
        };

        if (string.IsNullOrWhiteSpace(architecture))
        {
            error = $"Service runtime bundling does not support process architecture '{RuntimeInformation.ProcessArchitecture}'.";
            return false;
        }

        runtimeRid = $"{osPrefix}-{architecture}";
        return true;
    }

    /// <summary>
    /// Ensures execute permissions are present for service runtime files on Unix platforms.
    /// </summary>
    /// <param name="runtimePath">Runtime executable file path.</param>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    private static void TryEnsureServiceRuntimeExecutablePermissions(string runtimePath)
    {
        try
        {
            var mode = File.GetUnixFileMode(runtimePath);
            mode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(runtimePath, mode);
        }
        catch
        {
            // Ignore permission update failures and let service startup report execution errors if needed.
        }
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
        return Directory.Exists(fullPath)
            || inputPath.EndsWith('\\')
            || inputPath.EndsWith('/')
            ? Path.Combine(fullPath, defaultFileName)
            : fullPath;
    }

    /// <summary>
    /// Starts a Windows service using sc.exe.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <returns>Process exit code.</returns>
    private static int StartWindowsService(string serviceName, string? configuredLogPath)
    {
        var result = RunProcess("sc.exe", ["start", serviceName]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            WriteServiceOperationLog(
                $"operation='start' service='{serviceName}' platform='windows' result='failed' exitCode={result.ExitCode} error='{result.Error.Trim()}'",
                configuredLogPath,
                serviceName);
            return result.ExitCode;
        }

        WriteServiceOperationLog(
            $"operation='start' service='{serviceName}' platform='windows' result='success' exitCode=0",
            configuredLogPath,
            serviceName);
        Console.WriteLine($"Started Windows service '{serviceName}'.");
        return 0;
    }

    /// <summary>
    /// Stops a Windows service using sc.exe.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <returns>Process exit code.</returns>
    private static int StopWindowsService(string serviceName, string? configuredLogPath)
    {
        var result = RunProcess("sc.exe", ["stop", serviceName]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            WriteServiceOperationLog(
                $"operation='stop' service='{serviceName}' platform='windows' result='failed' exitCode={result.ExitCode} error='{result.Error.Trim()}'",
                configuredLogPath,
                serviceName);
            return result.ExitCode;
        }

        WriteServiceOperationLog(
            $"operation='stop' service='{serviceName}' platform='windows' result='success' exitCode=0",
            configuredLogPath,
            serviceName);
        Console.WriteLine($"Stopped Windows service '{serviceName}'.");
        return 0;
    }

    /// <summary>
    /// Queries a Windows service using sc.exe.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <returns>Process exit code.</returns>
    private static int QueryWindowsService(string serviceName, string? configuredLogPath)
    {
        var result = RunProcess("sc.exe", ["query", serviceName]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            WriteServiceOperationLog(
                $"operation='query' service='{serviceName}' platform='windows' result='failed' exitCode={result.ExitCode} error='{result.Error.Trim()}'",
                configuredLogPath,
                serviceName);
            return result.ExitCode;
        }

        var stateLine = result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(static line => line.Contains("STATE", StringComparison.OrdinalIgnoreCase)) ?? "STATE: unknown";

        WriteServiceOperationLog(
            $"operation='query' service='{serviceName}' platform='windows' result='success' exitCode=0 state='{stateLine}'",
            configuredLogPath,
            serviceName);

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

        var result = new StringBuilder(arg.Length + 2);
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
                _ = result.Append('\\', (backslashes * 2) + 1);
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
        var safeName = new string([.. serviceName.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-')]);

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
        => RunnerRuntime.EnsureNet10Runtime(ProductName);

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
        => RunnerRuntime.EnsurePowerShellRuntimeHome(createFallbackDirectories: true);

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
    /// Writes PowerShell pipeline output to stdout.
    /// </summary>
    /// <param name="output">Pipeline output collection.</param>
    private static void WriteOutput(IEnumerable<PSObject> output)
        => RunnerRuntime.DispatchPowerShellOutput(
            output,
            static value => Console.Out.WriteLine(value),
            skipWhitespace: false);

    /// <summary>
    /// Writes non-output streams in a console-friendly format.
    /// </summary>
    /// <param name="streams">PowerShell data streams.</param>
    private static void WriteStreams(PSDataStreams streams)
    {
        RunnerRuntime.DispatchPowerShellStreams(
            streams,
            onWarning: static message => Console.Error.WriteLine($"WARNING: {message}"),
            onVerbose: static message => Console.Error.WriteLine($"VERBOSE: {message}"),
            onDebug: static message => Console.Error.WriteLine($"DEBUG: {message}"),
            onInformation: static message => Console.Out.WriteLine(message),
            onError: static message => Console.Error.WriteLine(message),
            skipWhitespace: true);
    }

    /// <summary>
    /// Parses runner-specific global options and returns arguments to pass into command parsing.
    /// </summary>
    /// <param name="args">Raw process arguments.</param>
    /// <returns>Normalized argument set and global option flags.</returns>
    private static GlobalOptions ParseGlobalOptions(string[] args)
    {
        var commandArgs = new List<string>(args.Length);
        var skipGalleryCheck = false;
        var passthroughArguments = false;

        foreach (var arg in args)
        {
            if (!passthroughArguments && arg is "--arguments" or "--")
            {
                passthroughArguments = true;
                commandArgs.Add(arg);
                continue;
            }

            if (!passthroughArguments && IsNoCheckOption(arg))
            {
                skipGalleryCheck = true;
                continue;
            }

            commandArgs.Add(arg);
        }

        return new GlobalOptions([.. commandArgs], skipGalleryCheck);
    }

    /// <summary>
    /// Determines whether the token disables gallery version checks.
    /// </summary>
    /// <param name="token">Argument token.</param>
    /// <returns>True when the token disables gallery checks.</returns>
    private static bool IsNoCheckOption(string token)
        => string.Equals(token, NoCheckOption, StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, NoCheckAliasOption, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Executes a module management command.
    /// </summary>
    /// <param name="command">Parsed module command.</param>
    /// <returns>Process exit code.</returns>
    private static int ManageModuleCommand(ParsedCommand command)
    {
        return command.Mode switch
        {
            CommandMode.ModuleInstall => ManageModuleFromGallery(ModuleCommandAction.Install, command.ModuleVersion, command.ModuleScope, command.ModuleForce),
            CommandMode.ModuleUpdate => ManageModuleFromGallery(ModuleCommandAction.Update, command.ModuleVersion, command.ModuleScope, command.ModuleForce),
            CommandMode.ModuleRemove => ManageModuleFromGallery(ModuleCommandAction.Remove, null, command.ModuleScope, force: false),
            CommandMode.ModuleInfo => PrintModuleInfo(command.ModuleScope),
            _ => throw new InvalidOperationException($"Unsupported module mode: {command.Mode}"),
        };
    }

    /// <summary>
    /// Prints module installation details for local and gallery versions.
    /// </summary>
    /// <returns>Process exit code.</returns>
    private static int PrintModuleInfo(ModuleStorageScope scope)
    {
        var modulePath = GetPowerShellModulePath(scope);
        var moduleRoot = Path.Combine(modulePath, ModuleName);
        var records = GetInstalledModuleRecords(moduleRoot);
        var latestInstalledVersionText = records.Count > 0 ? records[0].Version : null;

        Console.WriteLine($"Module name: {ModuleName}");
        Console.WriteLine($"Selected module scope: {GetScopeToken(scope)}");
        Console.WriteLine($"Module path root: {modulePath}");

        if (records.Count == 0)
        {
            Console.WriteLine("Installed versions: none");
        }
        else
        {
            Console.WriteLine("Installed versions:");
            foreach (var record in records)
            {
                Console.WriteLine($"  - {record.Version} ({Path.GetDirectoryName(record.ManifestPath)})");
            }
        }

        if (TryGetLatestGalleryVersionString(out var galleryVersion, out _))
        {
            Console.WriteLine($"Latest PowerShell Gallery version: {galleryVersion}");

            if (!string.IsNullOrWhiteSpace(latestInstalledVersionText)
                && CompareModuleVersionValues(galleryVersion, latestInstalledVersionText) > 0)
            {
                Console.WriteLine($"Update available: run '{ProductName} module update'.");
            }
        }
        else
        {
            Console.WriteLine("Latest PowerShell Gallery version: unavailable");
        }

        return 0;
    }

    /// <summary>
    /// Installs, updates, or removes the Kestrun module in the current-user module path.
    /// </summary>
    /// <param name="action">Module action to perform.</param>
    /// <param name="version">Optional specific module version.</param>
    /// <param name="scope">Module installation scope.</param>
    /// <param name="force">When true, update overwrites an existing target version folder.</param>
    /// <returns>Process exit code.</returns>
    private static int ManageModuleFromGallery(ModuleCommandAction action, string? version, ModuleStorageScope scope, bool force)
    {
        var modulePath = GetPowerShellModulePath(scope);
        var moduleRoot = Path.Combine(modulePath, ModuleName);

        if (action == ModuleCommandAction.Remove)
        {
            if (!TryRemoveInstalledModule(moduleRoot, !Console.IsOutputRedirected, out var removeErrorText))
            {
                Console.Error.WriteLine($"Failed to remove '{ModuleName}' module.");
                if (!string.IsNullOrWhiteSpace(removeErrorText))
                {
                    Console.Error.WriteLine(removeErrorText);
                }

                return 1;
            }

            Console.WriteLine($"{ModuleName} module removed from {GetScopeToken(scope)} module path.");
            Console.WriteLine($"Module root: {moduleRoot}");
            return 0;
        }

        if (action == ModuleCommandAction.Install
            && !TryValidateInstallAction(moduleRoot, GetScopeToken(scope), out var installValidationError))
        {
            Console.Error.WriteLine(installValidationError);
            return 1;
        }

        if (!TryInstallOrUpdateModuleFromGallery(action, version, moduleRoot, !Console.IsOutputRedirected, force, out var installedVersion, out var installedManifestPath, out var errorText))
        {
            Console.Error.WriteLine($"Failed to {action.ToString().ToLowerInvariant()} '{ModuleName}' module.");
            if (!string.IsNullOrWhiteSpace(errorText))
            {
                Console.Error.WriteLine(errorText);
            }

            return 1;
        }

        var installedPath = Path.GetDirectoryName(installedManifestPath) ?? Path.Combine(moduleRoot, installedVersion);
        var versionSuffix = string.IsNullOrWhiteSpace(installedVersion)
            ? string.Empty
            : $" (version {installedVersion})";

        if (action == ModuleCommandAction.Install)
        {
            Console.WriteLine($"{ModuleName} module installed{versionSuffix} to {GetScopeToken(scope)} scope.");
        }
        else
        {
            Console.WriteLine($"{ModuleName} module updated{versionSuffix} in {GetScopeToken(scope)} scope.");
        }

        Console.WriteLine($"Module path: {installedPath}");
        return 0;
    }

    /// <summary>
    /// Downloads a module package from PowerShell Gallery and installs it into the user module path.
    /// </summary>
    /// <param name="action">Module action being executed.</param>
    /// <param name="requestedVersion">Optional requested package version.</param>
    /// <param name="moduleRoot">Root folder for module versions.</param>
    /// <param name="installedVersion">Installed module version.</param>
    /// <param name="installedManifestPath">Installed manifest path.</param>
    /// <param name="errorText">Error details when installation fails.</param>
    /// <returns>True when install/update succeeds.</returns>
    private static bool TryInstallOrUpdateModuleFromGallery(
        ModuleCommandAction action,
        string? requestedVersion,
        string moduleRoot,
        bool showProgress,
        bool force,
        out string installedVersion,
        out string installedManifestPath,
        out string errorText)
    {
        installedVersion = string.Empty;
        installedManifestPath = string.Empty;
        if (!TryDownloadModulePackage(requestedVersion, showProgress, out var packageBytes, out var packageVersion, out errorText))
        {
            return false;
        }

        if (action == ModuleCommandAction.Update
            && !TryValidateUpdateAction(moduleRoot, packageVersion, force, out errorText))
        {
            return false;
        }

        if (!TryExtractModulePackage(packageBytes, packageVersion, moduleRoot, showProgress, force, out installedManifestPath, out errorText))
        {
            return false;
        }

        installedVersion = packageVersion;
        return true;
    }

    /// <summary>
    /// Downloads the Kestrun nupkg package from PowerShell Gallery.
    /// </summary>
    /// <param name="requestedVersion">Optional requested package version.</param>
    /// <param name="packageBytes">Downloaded package payload.</param>
    /// <param name="packageVersion">Resolved package version from nuspec metadata.</param>
    /// <param name="errorText">Error details when download fails.</param>
    /// <returns>True when the package download succeeds.</returns>
    private static bool TryDownloadModulePackage(
        string? requestedVersion,
        bool showProgress,
        out byte[] packageBytes,
        out string packageVersion,
        out string errorText)
    {
        packageBytes = [];
        packageVersion = string.Empty;
        errorText = string.Empty;

        try
        {
            var normalizedVersion = string.IsNullOrWhiteSpace(requestedVersion)
                ? null
                : requestedVersion.Trim();

            var packageUrl = string.IsNullOrWhiteSpace(normalizedVersion)
                ? $"{PowerShellGalleryApiBaseUri}/package/{Uri.EscapeDataString(ModuleName)}"
                : $"{PowerShellGalleryApiBaseUri}/package/{Uri.EscapeDataString(ModuleName)}/{Uri.EscapeDataString(normalizedVersion)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, packageUrl);
            using var response = GalleryHttpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    if (string.IsNullOrWhiteSpace(normalizedVersion)
                        && TryGetLatestGalleryVersionString(out var latestVersion, out _)
                        && !string.IsNullOrWhiteSpace(latestVersion))
                    {
                        return TryDownloadModulePackage(latestVersion, showProgress, out packageBytes, out packageVersion, out errorText);
                    }

                    errorText = string.IsNullOrWhiteSpace(normalizedVersion)
                        ? $"Module '{ModuleName}' was not found on PowerShell Gallery."
                        : $"Module '{ModuleName}' version '{normalizedVersion}' was not found on PowerShell Gallery.";
                    return false;
                }

                var reason = string.IsNullOrWhiteSpace(response.ReasonPhrase)
                    ? "Unknown error"
                    : response.ReasonPhrase;
                errorText = $"PowerShell Gallery request failed with HTTP {(int)response.StatusCode} ({reason}).";
                return false;
            }

            var contentLength = response.Content.Headers.ContentLength;
            using var responseStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            using var packageStream = contentLength.HasValue && contentLength.Value > 0 && contentLength.Value <= int.MaxValue
                ? new MemoryStream((int)contentLength.Value)
                : new MemoryStream();

            using var downloadProgress = showProgress
                ? new ConsoleProgressBar("Downloading package", contentLength, FormatByteProgressDetail)
                : null;

            CopyStreamWithProgress(responseStream, packageStream, downloadProgress);
            packageBytes = packageStream.ToArray();
            if (packageBytes.Length == 0)
            {
                errorText = "Downloaded package was empty.";
                return false;
            }

            if (!TryReadPackageVersion(packageBytes, out packageVersion))
            {
                if (!string.IsNullOrWhiteSpace(normalizedVersion))
                {
                    if (TryNormalizeModuleVersion(normalizedVersion, out packageVersion))
                    {
                        return true;
                    }

                    errorText = $"Unable to normalize package version '{normalizedVersion}' for module folder naming.";
                    return false;
                }

                errorText = "Unable to determine package version from downloaded metadata.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorText = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Validates install preconditions for module install operations.
    /// </summary>
    /// <param name="moduleRoot">Root folder for module versions.</param>
    /// <param name="scopeToken">Module scope token for messaging.</param>
    /// <param name="errorText">Validation error details when install should not proceed.</param>
    /// <returns>True when install can proceed.</returns>
    private static bool TryValidateInstallAction(string moduleRoot, string scopeToken, out string errorText)
    {
        errorText = string.Empty;
        if (GetInstalledModuleRecords(moduleRoot).Count == 0)
        {
            return true;
        }

        errorText = $"{ModuleName} module is already installed in {scopeToken} scope. Use '{ProductName} module update' to update the existing installation.";
        return false;
    }

    /// <summary>
    /// Validates update preconditions for module update operations.
    /// </summary>
    /// <param name="moduleRoot">Root folder for module versions.</param>
    /// <param name="packageVersion">Resolved target package version.</param>
    /// <param name="force">When true, overwrite is allowed.</param>
    /// <param name="errorText">Validation error details when update should not proceed.</param>
    /// <returns>True when update can proceed.</returns>
    private static bool TryValidateUpdateAction(string moduleRoot, string packageVersion, bool force, out string errorText)
    {
        errorText = string.Empty;
        if (force)
        {
            return true;
        }

        var destinationModuleDirectory = Path.Combine(moduleRoot, packageVersion);
        if (!Directory.Exists(destinationModuleDirectory))
        {
            return true;
        }

        errorText = $"Module version '{packageVersion}' is already installed at '{destinationModuleDirectory}'. Use '{ProductName} module update {ModuleForceOption}' to overwrite this version.";
        return false;
    }

    /// <summary>
    /// Reads the package version from a nupkg file payload.
    /// </summary>
    /// <param name="packageBytes">Package bytes.</param>
    /// <param name="packageVersion">Parsed package version.</param>
    /// <returns>True when a version was discovered.</returns>
    private static bool TryReadPackageVersion(byte[] packageBytes, out string packageVersion)
    {
        packageVersion = string.Empty;

        using var stream = new MemoryStream(packageBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var nuspecEntry = archive.Entries.FirstOrDefault(static entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        if (nuspecEntry is null)
        {
            return false;
        }

        using var reader = new StreamReader(nuspecEntry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var nuspecText = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(nuspecText))
        {
            return false;
        }

        var document = XDocument.Parse(nuspecText);
        var versionElement = document.Descendants()
            .FirstOrDefault(static element => string.Equals(element.Name.LocalName, "version", StringComparison.OrdinalIgnoreCase));
        if (versionElement is null)
        {
            return false;
        }

        packageVersion = versionElement.Value.Trim();
        return TryNormalizeModuleVersion(packageVersion, out packageVersion);
    }

    /// <summary>
    /// Extracts a module package payload and installs it under the versioned module directory.
    /// </summary>
    /// <param name="packageBytes">Downloaded package bytes.</param>
    /// <param name="packageVersion">Package version used for destination folder naming.</param>
    /// <param name="moduleRoot">Root directory for module versions.</param>
    /// <param name="installedManifestPath">Installed module manifest path.</param>
    /// <param name="errorText">Error details when extraction fails.</param>
    /// <returns>True when package extraction and install succeed.</returns>
    private static bool TryExtractModulePackage(
        byte[] packageBytes,
        string packageVersion,
        string moduleRoot,
        bool showProgress,
        bool allowOverwrite,
        out string installedManifestPath,
        out string errorText)
    {
        installedManifestPath = string.Empty;
        errorText = string.Empty;

        if (packageVersion.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            errorText = $"Invalid package version '{packageVersion}' for filesystem install path.";
            return false;
        }

        var stagingPath = Path.Combine(Path.GetTempPath(), $"{ProductName}-module-{Guid.NewGuid():N}");
        var comparisonType = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        try
        {
            _ = Directory.CreateDirectory(stagingPath);

            using var packageStream = new MemoryStream(packageBytes, writable: false);
            using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: false);

            var payloadEntries = new List<(ZipArchiveEntry Entry, string RelativePath)>();
            foreach (var entry in archive.Entries)
            {
                if (TryGetPackagePayloadPath(entry.FullName, out var relativePath))
                {
                    payloadEntries.Add((entry, relativePath));
                }
            }

            if (payloadEntries.Count == 0)
            {
                errorText = "Package did not contain any module payload files.";
                return false;
            }

            var shouldStripModulePrefix = payloadEntries.All(static payloadEntry =>
            {
                var separatorIndex = payloadEntry.RelativePath.IndexOf('/');
                return separatorIndex > 0
                    && string.Equals(payloadEntry.RelativePath[..separatorIndex], ModuleName, StringComparison.OrdinalIgnoreCase);
            });

            using var extractProgress = showProgress
                ? new ConsoleProgressBar("Extracting package", payloadEntries.Count, FormatFileProgressDetail)
                : null;
            var extractedEntryCount = 0;
            extractProgress?.Report(0);

            var fullStagingPath = Path.GetFullPath(stagingPath);
            var fullStagingPathWithSeparator = Path.EndsInDirectorySeparator(fullStagingPath)
                ? fullStagingPath
                : fullStagingPath + Path.DirectorySeparatorChar;

            foreach (var (Entry, RelativePath) in payloadEntries)
            {
                var relativePath = RelativePath;
                if (shouldStripModulePrefix)
                {
                    var separatorIndex = relativePath.IndexOf('/');
                    relativePath = separatorIndex >= 0
                        ? relativePath[(separatorIndex + 1)..]
                        : relativePath;
                }

                if (string.IsNullOrWhiteSpace(relativePath))
                {
                    continue;
                }

                var destinationPath = Path.GetFullPath(Path.Combine(stagingPath, relativePath));
                if (!destinationPath.StartsWith(fullStagingPathWithSeparator, comparisonType))
                {
                    errorText = $"Package entry '{Entry.FullName}' resolves outside staging directory.";
                    return false;
                }

                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    _ = Directory.CreateDirectory(destinationDirectory);
                }

                Entry.ExtractToFile(destinationPath, overwrite: true);
                extractedEntryCount++;
                extractProgress?.Report(extractedEntryCount);
            }

            extractProgress?.Complete(extractedEntryCount);

            var manifestPath = Directory.EnumerateFiles(stagingPath, ModuleManifestFileName, SearchOption.AllDirectories)
                .FirstOrDefault(static path => path is not null);

            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                errorText = $"Package payload did not contain '{ModuleManifestFileName}'.";
                return false;
            }

            var sourceModuleDirectory = Path.GetDirectoryName(manifestPath)!;
            var destinationModuleDirectory = Path.Combine(moduleRoot, packageVersion);

            if (Directory.Exists(destinationModuleDirectory))
            {
                if (!allowOverwrite)
                {
                    errorText = $"Target module version folder already exists: {destinationModuleDirectory}";
                    return false;
                }

                Directory.Delete(destinationModuleDirectory, recursive: true);
            }

            CopyDirectoryContents(sourceModuleDirectory, destinationModuleDirectory, showProgress);

            installedManifestPath = Path.Combine(destinationModuleDirectory, ModuleManifestFileName);
            return File.Exists(installedManifestPath);
        }
        catch (Exception ex)
        {
            errorText = ex.Message;
            return false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingPath))
                {
                    Directory.Delete(stagingPath, recursive: true);
                }
            }
            catch
            {
                // Cleanup failures are non-fatal for module install flow.
            }
        }
    }

    /// <summary>
    /// Maps package entry paths to relative module payload paths.
    /// </summary>
    /// <param name="entryPath">Original package entry path.</param>
    /// <param name="relativePath">Mapped relative payload path.</param>
    /// <returns>True when the entry belongs to module payload content.</returns>
    private static bool TryGetPackagePayloadPath(string entryPath, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(entryPath))
        {
            return false;
        }

        var normalizedPath = entryPath.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalizedPath) || normalizedPath.EndsWith('/'))
        {
            return false;
        }

        if (normalizedPath.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith("_rels/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith("package/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalizedPath.StartsWith("tools/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath = normalizedPath["tools/".Length..];
        }
        else if (normalizedPath.StartsWith("content/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath = normalizedPath["content/".Length..];
        }
        else if (normalizedPath.StartsWith("contentFiles/any/any/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath = normalizedPath["contentFiles/any/any/".Length..];
        }

        relativePath = normalizedPath.TrimStart('/');
        return !string.IsNullOrWhiteSpace(relativePath);
    }

    /// <summary>
    /// Copies all files recursively from one directory to another.
    /// </summary>
    /// <param name="sourceDirectory">Source directory.</param>
    /// <param name="destinationDirectory">Destination directory.</param>
    /// <param name="showProgress">When true, writes interactive progress bars.</param>
    private static void CopyDirectoryContents(string sourceDirectory, string destinationDirectory, bool showProgress)
        => CopyDirectoryContents(sourceDirectory, destinationDirectory, showProgress, "Installing files", exclusionPatterns: null);

    /// <summary>
    /// Copies all files recursively from one directory to another.
    /// </summary>
    /// <param name="sourceDirectory">Source directory.</param>
    /// <param name="destinationDirectory">Destination directory.</param>
    /// <param name="showProgress">When true, writes interactive progress bars.</param>
    /// <param name="progressLabel">Progress bar label for file copy operations.</param>
    /// <param name="exclusionPatterns">Optional wildcard patterns (relative to <paramref name="sourceDirectory"/>) for files to skip.</param>
    private static void CopyDirectoryContents(
        string sourceDirectory,
        string destinationDirectory,
        bool showProgress,
        string progressLabel,
        IReadOnlyList<string>? exclusionPatterns)
    {
        _ = Directory.CreateDirectory(destinationDirectory);

        var exclusionRegexes = BuildCopyExclusionRegexes(exclusionPatterns);
        var sourceFilePaths = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            .Where(sourceFilePath => !ShouldExcludeCopyFile(sourceDirectory, sourceFilePath, exclusionRegexes))
            .ToList();
        using var copyProgress = showProgress
            ? new ConsoleProgressBar(progressLabel, sourceFilePaths.Count, FormatFileProgressDetail)
            : null;
        var copiedFiles = 0;
        copyProgress?.Report(0);

        foreach (var sourceFilePath in sourceFilePaths)
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourceFilePath);
            var destinationFilePath = Path.Combine(destinationDirectory, relativePath);
            var destinationFileDirectory = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrWhiteSpace(destinationFileDirectory))
            {
                _ = Directory.CreateDirectory(destinationFileDirectory);
            }

            File.Copy(sourceFilePath, destinationFilePath, overwrite: true);
            copiedFiles++;
            copyProgress?.Report(copiedFiles);
        }

        copyProgress?.Complete(copiedFiles);
    }

    /// <summary>
    /// Determines whether a source file should be excluded from a directory copy operation.
    /// </summary>
    /// <param name="sourceDirectory">Source directory root.</param>
    /// <param name="sourceFilePath">Absolute source file path.</param>
    /// <param name="exclusionRegexes">Compiled exclusion regexes.</param>
    /// <returns>True when the file should be excluded.</returns>
    private static bool ShouldExcludeCopyFile(string sourceDirectory, string sourceFilePath, IReadOnlyList<Regex> exclusionRegexes)
    {
        if (exclusionRegexes.Count == 0)
        {
            return false;
        }

        var relativePath = NormalizeCopyPath(Path.GetRelativePath(sourceDirectory, sourceFilePath));
        return exclusionRegexes.Any(regex => regex.IsMatch(relativePath));
    }

    /// <summary>
    /// Compiles wildcard exclusion patterns used by directory copy operations.
    /// </summary>
    /// <param name="exclusionPatterns">Wildcard exclusion patterns.</param>
    /// <returns>Compiled regex list for path matching.</returns>
    private static List<Regex> BuildCopyExclusionRegexes(IReadOnlyList<string>? exclusionPatterns)
    {
        if (exclusionPatterns is null || exclusionPatterns.Count == 0)
        {
            return [];
        }

        var regexOptions = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        if (OperatingSystem.IsWindows())
        {
            regexOptions |= RegexOptions.IgnoreCase;
        }

        var regexes = new List<Regex>(exclusionPatterns.Count);
        foreach (var exclusionPattern in exclusionPatterns)
        {
            var normalizedPattern = NormalizeCopyPath(exclusionPattern);
            if (string.IsNullOrWhiteSpace(normalizedPattern))
            {
                continue;
            }

            var regexPattern = $"^{Regex.Escape(normalizedPattern).Replace(@"\*", ".*").Replace(@"\?", ".")}$";
            regexes.Add(new Regex(regexPattern, regexOptions, TimeSpan.FromMilliseconds(250)));
        }

        return regexes;
    }

    /// <summary>
    /// Normalizes a relative path for wildcard matching.
    /// </summary>
    /// <param name="relativePath">Relative path or wildcard pattern.</param>
    /// <returns>Normalized slash-separated path without leading dot prefixes.</returns>
    private static string NormalizeCopyPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        var normalizedPath = relativePath.Trim().Replace('\\', '/');
        while (normalizedPath.StartsWith("./", StringComparison.Ordinal))
        {
            normalizedPath = normalizedPath[2..];
        }

        return normalizedPath.TrimStart('/');
    }

    /// <summary>
    /// Removes all installed module files and folders for the selected scope.
    /// </summary>
    /// <param name="moduleRoot">Module root directory to remove.</param>
    /// <param name="showProgress">When true, writes interactive progress bars.</param>
    /// <param name="errorText">Error details when removal fails.</param>
    /// <returns>True when removal succeeds.</returns>
    private static bool TryRemoveInstalledModule(string moduleRoot, bool showProgress, out string errorText)
    {
        errorText = string.Empty;

        if (!Directory.Exists(moduleRoot))
        {
            return true;
        }

        try
        {
            var filePaths = Directory.EnumerateFiles(moduleRoot, "*", SearchOption.AllDirectories).ToList();
            using var fileProgress = showProgress
                ? new ConsoleProgressBar("Removing files", filePaths.Count, FormatFileProgressDetail)
                : null;
            var removedFiles = 0;
            fileProgress?.Report(0);

            foreach (var filePath in filePaths)
            {
                try
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                }
                catch
                {
                    // Best-effort normalization; delete may still succeed without changing attributes.
                }

                File.Delete(filePath);
                removedFiles++;
                fileProgress?.Report(removedFiles);
            }

            fileProgress?.Complete(removedFiles);

            var directoryPaths = Directory.EnumerateDirectories(moduleRoot, "*", SearchOption.AllDirectories)
                .OrderByDescending(path => path.Length)
                .ToList();

            using var directoryProgress = showProgress
                ? new ConsoleProgressBar("Removing folders", directoryPaths.Count + 1, FormatFileProgressDetail)
                : null;
            var removedDirectories = 0;
            directoryProgress?.Report(0);

            foreach (var directoryPath in directoryPaths)
            {
                Directory.Delete(directoryPath, recursive: false);
                removedDirectories++;
                directoryProgress?.Report(removedDirectories);
            }

            Directory.Delete(moduleRoot, recursive: false);
            removedDirectories++;
            directoryProgress?.Report(removedDirectories);
            directoryProgress?.Complete(removedDirectories);

            return true;
        }
        catch (Exception ex)
        {
            errorText = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Copies one stream into another while reporting transfer progress.
    /// </summary>
    /// <param name="source">Source stream.</param>
    /// <param name="destination">Destination stream.</param>
    /// <param name="progress">Optional progress reporter.</param>
    private static void CopyStreamWithProgress(Stream source, Stream destination, ConsoleProgressBar? progress)
    {
        var buffer = new byte[81920];
        var totalCopied = 0L;
        progress?.Report(0);

        while (true)
        {
            var bytesRead = source.Read(buffer, 0, buffer.Length);
            if (bytesRead <= 0)
            {
                break;
            }

            destination.Write(buffer, 0, bytesRead);
            totalCopied += bytesRead;
            progress?.Report(totalCopied);
        }

        progress?.Complete(totalCopied);
    }

    /// <summary>
    /// Formats byte transfer progress details.
    /// </summary>
    /// <param name="current">Current transferred bytes.</param>
    /// <param name="total">Total bytes when known.</param>
    /// <returns>Formatted progress text.</returns>
    private static string FormatByteProgressDetail(long current, long? total)
        => total.HasValue
            ? $"{FormatByteSize(current)} / {FormatByteSize(total.Value)}"
            : FormatByteSize(current);

    /// <summary>
    /// Formats file count progress details.
    /// </summary>
    /// <param name="current">Current processed file count.</param>
    /// <param name="total">Total file count when known.</param>
    /// <returns>Formatted progress text.</returns>
    private static string FormatFileProgressDetail(long current, long? total)
        => total.HasValue
            ? $"{current}/{total.Value} files"
            : $"{current} files";

    /// <summary>
    /// Formats progress details for service bundle preparation steps.
    /// </summary>
    /// <param name="current">Current completed step number.</param>
    /// <param name="total">Total step count.</param>
    /// <returns>Formatted step progress detail.</returns>
    private static string FormatServiceBundleStepProgressDetail(long current, long? total)
    {
        var stepLabel = current switch
        {
            <= 0 => "initializing",
            1 => "creating folders",
            2 => "copying runtime",
            3 => "copying module",
            _ => "copying script",
        };

        return total.HasValue
            ? $"step {Math.Min(current, total.Value)}/{total.Value} ({stepLabel})"
            : $"step {current} ({stepLabel})";
    }

    /// <summary>
    /// Formats a byte value to a readable unit string.
    /// </summary>
    /// <param name="bytes">Byte count.</param>
    /// <returns>Human-readable byte text.</returns>
    private static string FormatByteSize(long bytes)
    {
        var unitIndex = 0;
        var value = (double)Math.Max(0, bytes);
        var units = new[] { "B", "KB", "MB", "GB", "TB" };

        while (value >= 1024d && unitIndex < units.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} {units[unitIndex]}"
            : $"{value:0.##} {units[unitIndex]}";
    }

    /// <summary>
    /// Prints an update warning when a newer PowerShell Gallery version exists.
    /// </summary>
    /// <param name="moduleManifestPath">Resolved local module manifest path.</param>
    /// <param name="logPath">Optional log path for warning output; when omitted, warning is written to stderr.</param>
    private static void WarnIfNewerGalleryVersionExists(string moduleManifestPath, string? logPath = null)
    {
        if (!TryGetLatestInstalledModuleVersionText(ModuleStorageScope.Local, out var installedVersion)
            && !TryGetLatestInstalledModuleVersionText(ModuleStorageScope.Global, out installedVersion)
            && !TryGetInstalledModuleVersionText(moduleManifestPath, out installedVersion))
        {
            return;
        }

        if (!TryGetLatestGalleryVersionString(out var galleryVersion, out _))
        {
            return;
        }

        if (CompareModuleVersionValues(galleryVersion, installedVersion) <= 0)
        {
            return;
        }

        var warningMessage =
            $"WARNING: A newer {ModuleName} module is available on PowerShell Gallery ({galleryVersion}). "
            + $"Current version: {installedVersion}. Use '{ProductName} module update' or {NoCheckOption} to suppress this check.";

        WriteWarningToLogOrConsole(warningMessage, logPath);
    }

    /// <summary>
    /// Writes a warning to a configured log file when available; otherwise stderr.
    /// </summary>
    /// <param name="message">Warning message.</param>
    /// <param name="logPath">Optional log path.</param>
    private static void WriteWarningToLogOrConsole(string message, string? logPath)
    {
        switch (string.IsNullOrWhiteSpace(logPath))
        {
            case true:
                Console.Error.WriteLine(message);
                return;

            case false:
                try
                {
                    var resolvedPath = NormalizeServiceLogPath(logPath, defaultFileName: "kestrun-tool-service.log");
                    var directory = Path.GetDirectoryName(resolvedPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        _ = Directory.CreateDirectory(directory);
                    }

                    File.AppendAllText(resolvedPath, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}", Encoding.UTF8);
                    return;
                }
                catch
                {
                    Console.Error.WriteLine(message);
                    return;
                }
        }
    }

    /// <summary>
    /// Attempts to read the latest installed module version text from a selected scope.
    /// </summary>
    /// <param name="scope">Module storage scope.</param>
    /// <param name="versionText">Installed semantic version text when available.</param>
    /// <returns>True when an installed version was found in the scope.</returns>
    private static bool TryGetLatestInstalledModuleVersionText(ModuleStorageScope scope, out string versionText)
    {
        versionText = string.Empty;

        var modulePath = GetPowerShellModulePath(scope);
        var moduleRoot = Path.Combine(modulePath, ModuleName);
        var records = GetInstalledModuleRecords(moduleRoot);
        if (records.Count == 0)
        {
            return false;
        }

        versionText = records[0].Version;
        return !string.IsNullOrWhiteSpace(versionText);
    }

    /// <summary>
    /// Attempts to read the local Kestrun module semantic version from manifest metadata.
    /// </summary>
    /// <param name="moduleManifestPath">Path to Kestrun.psd1.</param>
    /// <param name="versionText">Installed semantic version text when available.</param>
    /// <returns>True when a version was read.</returns>
    private static bool TryGetInstalledModuleVersionText(string moduleManifestPath, out string versionText)
    {
        versionText = string.Empty;

        if (TryReadModuleSemanticVersionFromManifest(moduleManifestPath, out var manifestVersionText))
        {
            versionText = manifestVersionText;
            return true;
        }

        var versionDirectory = Path.GetFileName(Path.GetDirectoryName(moduleManifestPath));
        if (TryNormalizeModuleVersion(versionDirectory, out var normalizedVersionDirectory))
        {
            versionText = normalizedVersionDirectory;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to read module semantic version (including prerelease) from a PowerShell module manifest file.
    /// </summary>
    /// <param name="manifestPath">Manifest path.</param>
    /// <param name="versionText">Semantic version text when present.</param>
    /// <returns>True when semantic version was discovered.</returns>
    private static bool TryReadModuleSemanticVersionFromManifest(string manifestPath, out string versionText)
    {
        versionText = string.Empty;
        if (!TryReadModuleVersionFromManifest(manifestPath, out var baseVersion))
        {
            return false;
        }

        var semanticVersion = baseVersion;
        try
        {
            var content = File.ReadAllText(manifestPath);
            var prereleaseMatch = ModulePrereleaseRegex.Match(content);
            if (prereleaseMatch.Success)
            {
                var prereleaseValue = prereleaseMatch.Groups["value"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(prereleaseValue)
                    && !baseVersion.Contains('-', StringComparison.Ordinal)
                    && !baseVersion.Contains('+', StringComparison.Ordinal))
                {
                    semanticVersion = $"{baseVersion}-{prereleaseValue}";
                }
            }
        }
        catch
        {
            // Fall back to ModuleVersion when Prerelease inspection fails.
        }

        versionText = semanticVersion;
        return !string.IsNullOrWhiteSpace(versionText);
    }

    /// <summary>
    /// Attempts to query the latest Kestrun module version string from PowerShell Gallery.
    /// </summary>
    /// <param name="version">Latest gallery version string when available.</param>
    /// <param name="errorText">Error details when discovery fails.</param>
    /// <returns>True when latest gallery version was discovered.</returns>
    private static bool TryGetLatestGalleryVersionString(out string version, out string errorText)
    {
        version = string.Empty;
        if (!TryGetGalleryModuleVersions(out var versions, out errorText))
        {
            return false;
        }

        versions.Sort(CompareModuleVersionValues);
        version = versions[^1];
        return true;
    }

    /// <summary>
    /// Queries all available Kestrun module versions from PowerShell Gallery.
    /// </summary>
    /// <param name="versions">Discovered gallery versions.</param>
    /// <param name="errorText">Error details when discovery fails.</param>
    /// <returns>True when at least one version was discovered.</returns>
    private static bool TryGetGalleryModuleVersions(out List<string> versions, out string errorText)
    {
        versions = [];
        errorText = string.Empty;

        try
        {
            var requestUri = $"{PowerShellGalleryApiBaseUri}/FindPackagesById()?id='{Uri.EscapeDataString(ModuleName)}'";
            using var response = GalleryHttpClient.GetAsync(requestUri).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                var reason = string.IsNullOrWhiteSpace(response.ReasonPhrase)
                    ? "Unknown error"
                    : response.ReasonPhrase;
                errorText = $"PowerShell Gallery request failed with HTTP {(int)response.StatusCode} ({reason}).";
                return false;
            }

            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(content))
            {
                errorText = "PowerShell Gallery response was empty.";
                return false;
            }

            var document = XDocument.Parse(content);
            var discoveredVersions = document.Descendants()
                .Where(static element => string.Equals(element.Name.LocalName, "Version", StringComparison.OrdinalIgnoreCase))
                .Select(static element => element.Value.Trim())
                .Where(static versionText => !string.IsNullOrWhiteSpace(versionText))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (discoveredVersions.Count == 0)
            {
                errorText = $"Module '{ModuleName}' was not found on PowerShell Gallery.";
                return false;
            }

            versions = discoveredVersions;
            return true;
        }
        catch (Exception ex)
        {
            errorText = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Creates the shared HTTP client used for PowerShell Gallery requests.
    /// </summary>
    /// <returns>Configured HTTP client instance.</returns>
    private static HttpClient CreateGalleryHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60),
        };

        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ProductName, "1.0"));
        return client;
    }

    /// <summary>
    /// Parses a module version value into a comparable <see cref="Version"/> instance.
    /// </summary>
    /// <param name="rawValue">Raw version string.</param>
    /// <param name="version">Parsed version.</param>
    /// <returns>True when parsing succeeds.</returns>
    private static bool TryParseVersionValue(string? rawValue, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var normalized = rawValue.Trim();
        var suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
        {
            normalized = normalized[..suffixIndex];
        }

        if (!Version.TryParse(normalized, out var parsedVersion) || parsedVersion is null)
        {
            return false;
        }

        version = parsedVersion;
        return true;
    }

    /// <summary>
    /// Normalizes a module version value to the stable numeric folder format used by PowerShell module installs.
    /// </summary>
    /// <param name="rawValue">Raw version token that may include prerelease/build suffixes.</param>
    /// <param name="normalizedVersion">Normalized numeric version text.</param>
    /// <returns>True when normalization succeeds.</returns>
    private static bool TryNormalizeModuleVersion(string? rawValue, out string normalizedVersion)
    {
        normalizedVersion = string.Empty;
        if (!TryParseVersionValue(rawValue, out var parsedVersion))
        {
            return false;
        }

        normalizedVersion = parsedVersion.ToString();
        return true;
    }

    /// <summary>
    /// Tries to parse a module storage scope token.
    /// </summary>
    /// <param name="scopeToken">Scope token.</param>
    /// <param name="scope">Parsed scope value.</param>
    /// <returns>True when parsing succeeds.</returns>
    private static bool TryParseModuleScope(string? scopeToken, out ModuleStorageScope scope)
    {
        scope = ModuleStorageScope.Local;
        if (string.IsNullOrWhiteSpace(scopeToken))
        {
            return false;
        }

        if (string.Equals(scopeToken, ModuleScopeLocalValue, StringComparison.OrdinalIgnoreCase))
        {
            scope = ModuleStorageScope.Local;
            return true;
        }

        if (string.Equals(scopeToken, ModuleScopeGlobalValue, StringComparison.OrdinalIgnoreCase))
        {
            scope = ModuleStorageScope.Global;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets a stable scope token for messages and help text.
    /// </summary>
    /// <param name="scope">Module storage scope.</param>
    /// <returns>Normalized scope token.</returns>
    private static string GetScopeToken(ModuleStorageScope scope)
        => scope == ModuleStorageScope.Global ? ModuleScopeGlobalValue : ModuleScopeLocalValue;

    /// <summary>
    /// Compares two module version strings.
    /// </summary>
    /// <param name="leftVersion">Left version.</param>
    /// <param name="rightVersion">Right version.</param>
    /// <returns>Comparison result compatible with <see cref="IComparer{T}"/>.</returns>
    private static int CompareModuleVersionValues(string? leftVersion, string? rightVersion)
    {
        if (ReferenceEquals(leftVersion, rightVersion))
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(leftVersion))
        {
            return -1;
        }

        if (string.IsNullOrWhiteSpace(rightVersion))
        {
            return 1;
        }

        if (TryParseVersionValue(leftVersion, out var leftParsed)
            && TryParseVersionValue(rightVersion, out var rightParsed))
        {
            var comparison = leftParsed.CompareTo(rightParsed);
            if (comparison != 0)
            {
                return comparison;
            }

            var leftHasPrerelease = HasPrereleaseSuffix(leftVersion);
            var rightHasPrerelease = HasPrereleaseSuffix(rightVersion);
            if (leftHasPrerelease != rightHasPrerelease)
            {
                return leftHasPrerelease ? -1 : 1;
            }
        }

        return string.Compare(leftVersion.Trim(), rightVersion.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether a module version string includes prerelease suffix data.
    /// </summary>
    /// <param name="versionText">Version string to inspect.</param>
    /// <returns>True when prerelease or build suffix exists.</returns>
    private static bool HasPrereleaseSuffix(string versionText)
        => versionText.Contains('-', StringComparison.Ordinal) || versionText.Contains('+', StringComparison.Ordinal);

    /// <summary>
    /// Reads ModuleVersion from a PowerShell module manifest file.
    /// </summary>
    /// <param name="manifestPath">Manifest path.</param>
    /// <param name="versionText">ModuleVersion text when present.</param>
    /// <returns>True when ModuleVersion was discovered.</returns>
    private static bool TryReadModuleVersionFromManifest(string manifestPath, out string versionText)
    {
        versionText = string.Empty;

        try
        {
            var content = File.ReadAllText(manifestPath);
            var match = ModuleVersionRegex.Match(content);
            if (!match.Success)
            {
                return false;
            }

            versionText = match.Groups["value"].Value.Trim();
            return !string.IsNullOrWhiteSpace(versionText);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Enumerates installed module manifest records from the user module root.
    /// </summary>
    /// <param name="moduleRoot">Module root path.</param>
    /// <returns>Installed module records sorted by version descending.</returns>
    private static List<InstalledModuleRecord> GetInstalledModuleRecords(string moduleRoot)
    {
        var records = new List<InstalledModuleRecord>();
        if (!Directory.Exists(moduleRoot))
        {
            return records;
        }

        var seenManifestPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifestPath in Directory.EnumerateFiles(moduleRoot, ModuleManifestFileName, SearchOption.AllDirectories))
        {
            if (!seenManifestPaths.Add(manifestPath))
            {
                continue;
            }

            var versionDirectory = Path.GetFileName(Path.GetDirectoryName(manifestPath));
            string? versionText = null;

            if (TryReadModuleSemanticVersionFromManifest(manifestPath, out var manifestSemanticVersion))
            {
                versionText = manifestSemanticVersion;
            }

            if (string.IsNullOrWhiteSpace(versionText)
                && !string.IsNullOrWhiteSpace(versionDirectory)
                && TryNormalizeModuleVersion(versionDirectory, out var normalizedVersionDirectory))
            {
                versionText = normalizedVersionDirectory;
            }

            if (string.IsNullOrWhiteSpace(versionText))
            {
                versionText = versionDirectory;
            }

            if (string.IsNullOrWhiteSpace(versionText))
            {
                continue;
            }

            records.Add(new InstalledModuleRecord(versionText, manifestPath));
        }

        records.Sort(static (left, right) => CompareModuleVersionValues(right.Version, left.Version));
        return records;
    }

    /// <summary>
    /// Gets the module storage path for a selected scope.
    /// </summary>
    /// <param name="scope">Module storage scope.</param>
    /// <returns>Absolute module storage path.</returns>
    private static string GetPowerShellModulePath(ModuleStorageScope scope)
        => scope == ModuleStorageScope.Global
            ? GetGlobalPowerShellModulePath()
            : GetDefaultPowerShellModulePath();

    /// <summary>
    /// Gets the default all-users PowerShell module path for the active OS.
    /// </summary>
    /// <returns>Absolute all-users module path.</returns>
    private static string GetGlobalPowerShellModulePath()
    {
        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var root = string.IsNullOrWhiteSpace(programFiles) ? @"C:\Program Files" : programFiles;
            return Path.Combine(root, "PowerShell", "Modules");
        }

        return "/usr/local/share/powershell/Modules";
    }

    /// <summary>
    /// Gets the default current-user PowerShell module path for the active OS.
    /// </summary>
    /// <returns>Absolute module path.</returns>
    private static string GetDefaultPowerShellModulePath()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var root = string.IsNullOrWhiteSpace(documents) ? userHome : documents;
            return Path.Combine(root, "PowerShell", "Modules");
        }

        return Path.Combine(userHome, ".local", "share", "powershell", "Modules");
    }

    /// <summary>
    /// Writes a consistent module-not-found message with remediation guidance.
    /// </summary>
    /// <param name="kestrunManifestPath">Optional explicit manifest path argument.</param>
    /// <param name="kestrunFolder">Optional explicit module folder argument.</param>
    /// <param name="writeLine">Output writer callback.</param>
    private static void WriteModuleNotFoundMessage(string? kestrunManifestPath, string? kestrunFolder, Action<string> writeLine)
    {
        if (!string.IsNullOrWhiteSpace(kestrunManifestPath))
        {
            writeLine($"Unable to locate manifest file: {Path.GetFullPath(kestrunManifestPath)}");
        }
        else if (!string.IsNullOrWhiteSpace(kestrunFolder))
        {
            writeLine($"Unable to locate {ModuleManifestFileName} in folder: {Path.GetFullPath(kestrunFolder)}");
        }
        else
        {
            writeLine($"Unable to locate {ModuleManifestFileName} under the executable folder or PSModulePath.");
        }

        writeLine($"No {ModuleName} module was found. Use '{ProductName} module install' to install it from PowerShell Gallery.");
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
        parsedCommand = new ParsedCommand(CommandMode.Run, string.Empty, [], null, null, null, null, null, ModuleStorageScope.Local, false, null, null);
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

        if (string.Equals(args[commandTokenIndex], "module", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseModuleArguments(args, commandTokenIndex + 1, out parsedCommand, out error);
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
        parsedCommand = new ParsedCommand(CommandMode.Run, string.Empty, [], kestrunFolder, kestrunManifestPath, null, null, null, ModuleStorageScope.Local, false, null, null);
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

        parsedCommand = new ParsedCommand(CommandMode.Run, scriptPath, scriptArguments, kestrunFolder, kestrunManifestPath, null, null, null, ModuleStorageScope.Local, false, null, null);

        return true;
    }

    /// <summary>
    /// Parses arguments for module install/update/remove/info commands.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="startIndex">Index after module token.</param>
    /// <param name="parsedCommand">Parsed command payload.</param>
    /// <param name="error">Error message when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private static bool TryParseModuleArguments(string[] args, int startIndex, out ParsedCommand parsedCommand, out string error)
    {
        parsedCommand = new ParsedCommand(CommandMode.ModuleInfo, string.Empty, [], null, null, null, null, null, ModuleStorageScope.Local, false, null, null);
        error = string.Empty;

        if (startIndex >= args.Length)
        {
            error = "Missing module action. Use 'module install', 'module update', 'module remove', or 'module info'.";
            return false;
        }

        var actionToken = args[startIndex];
        var mode = actionToken.ToLowerInvariant() switch
        {
            "install" => CommandMode.ModuleInstall,
            "update" => CommandMode.ModuleUpdate,
            "remove" => CommandMode.ModuleRemove,
            "info" => CommandMode.ModuleInfo,
            _ => (CommandMode?)null,
        };

        if (mode is null)
        {
            error = $"Unknown module action: {actionToken}. Use 'module install', 'module update', 'module remove', or 'module info'.";
            return false;
        }

        var index = startIndex + 1;
        string? moduleVersion = null;
        var moduleScope = ModuleStorageScope.Local;
        var moduleScopeSet = false;
        var moduleForce = false;
        var moduleForceSet = false;
        var acceptsVersion = mode is CommandMode.ModuleInstall or CommandMode.ModuleUpdate;
        while (index < args.Length)
        {
            var current = args[index];

            if (current is ModuleScopeOption or "-s")
            {
                if (index + 1 >= args.Length)
                {
                    error = $"Missing value for {ModuleScopeOption}. Use '{ModuleScopeLocalValue}' or '{ModuleScopeGlobalValue}'.";
                    return false;
                }

                if (moduleScopeSet)
                {
                    error = $"Module scope was provided multiple times. Use {ModuleScopeOption} once.";
                    return false;
                }

                if (!TryParseModuleScope(args[index + 1], out moduleScope))
                {
                    error = $"Unknown module scope: {args[index + 1]}. Use '{ModuleScopeLocalValue}' or '{ModuleScopeGlobalValue}'.";
                    return false;
                }

                moduleScopeSet = true;
                index += 2;
                continue;
            }

            if (current is ModuleVersionOption or "-v")
            {
                if (!acceptsVersion)
                {
                    error = $"Module {actionToken} does not accept {ModuleVersionOption}.";
                    return false;
                }

                if (index + 1 >= args.Length)
                {
                    error = $"Missing value for {ModuleVersionOption}.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(moduleVersion))
                {
                    error = $"Module version was provided multiple times. Use {ModuleVersionOption} once.";
                    return false;
                }

                moduleVersion = args[index + 1];
                index += 2;
                continue;
            }

            if (current is ModuleForceOption or "-f")
            {
                if (mode != CommandMode.ModuleUpdate)
                {
                    error = $"Module {actionToken} does not accept {ModuleForceOption}.";
                    return false;
                }

                if (moduleForceSet)
                {
                    error = $"{ModuleForceOption} was provided multiple times. Use {ModuleForceOption} once.";
                    return false;
                }

                moduleForce = true;
                moduleForceSet = true;
                index += 1;
                continue;
            }

            if (current.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unknown option: {current}";
                return false;
            }

            error = $"Unexpected argument for module {actionToken}: {current}";
            return false;
        }

        parsedCommand = new ParsedCommand(mode.Value, string.Empty, [], null, null, null, null, moduleVersion, moduleScope, moduleForce, null, null);
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
        parsedCommand = new ParsedCommand(CommandMode.ServiceInstall, string.Empty, [], kestrunFolder, kestrunManifestPath, null, null, null, ModuleStorageScope.Local, false, null, null);
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
        string? serviceContentRoot = null;
        string? serviceDeploymentRoot = null;

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

            if (current is "--deployment-root")
            {
                if (mode is CommandMode.ServiceStart or CommandMode.ServiceStop or CommandMode.ServiceQuery)
                {
                    error = "Service start/stop/query does not accept --deployment-root.";
                    return false;
                }

                if (index + 1 >= args.Length)
                {
                    error = "Missing value for --deployment-root.";
                    return false;
                }

                serviceDeploymentRoot = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--content-root")
            {
                if (mode is CommandMode.ServiceRemove or CommandMode.ServiceStart or CommandMode.ServiceStop or CommandMode.ServiceQuery)
                {
                    error = "Service remove/start/stop/query does not accept --content-root.";
                    return false;
                }

                if (index + 1 >= args.Length)
                {
                    error = "Missing value for --content-root.";
                    return false;
                }

                serviceContentRoot = args[index + 1];
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

        parsedCommand = new ParsedCommand(mode.Value, scriptPath, scriptArguments, kestrunFolder, kestrunManifestPath, serviceName, serviceLogPath, null, ModuleStorageScope.Local, false, serviceContentRoot, serviceDeploymentRoot);

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
        return topic is "run" or "service" or "module" or "info" or "version";
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

            if (IsNoCheckOption(args[index]))
            {
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
        Console.WriteLine("Global options:");
        Console.WriteLine($"  {NoCheckOption}          Skip PowerShell Gallery update check warnings.");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  run       Run a PowerShell script (default script: ./server.ps1)");
        Console.WriteLine("  module    Manage Kestrun module (install/update/remove/info)");
        Console.WriteLine("  service   Manage service lifecycle (install/remove/start/stop/query)");
        Console.WriteLine("  info      Show runtime/build diagnostics");
        Console.WriteLine("  version   Show tool version");
        Console.WriteLine();
        Console.WriteLine("Help topics:");
        Console.WriteLine("  kestrun run help");
        Console.WriteLine("  kestrun module help");
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
                Console.WriteLine("  kestrun [--nocheck] [--kestrun-folder <folder>] [--kestrun-manifest <path-to-Kestrun.psd1>] run [--script <main.ps1> | <main.ps1>] [--arguments <script arguments...>]");
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
                Console.WriteLine($"  - If {ModuleName} is missing, run '{ProductName} module install'.");
                break;

            case "module":
                Console.WriteLine("Usage:");
                Console.WriteLine($"  {ProductName} module install [{ModuleVersionOption} <version>] [{ModuleScopeOption} <{ModuleScopeLocalValue}|{ModuleScopeGlobalValue}>]");
                Console.WriteLine($"  {ProductName} module update [{ModuleVersionOption} <version>] [{ModuleScopeOption} <{ModuleScopeLocalValue}|{ModuleScopeGlobalValue}>] [{ModuleForceOption}]");
                Console.WriteLine($"  {ProductName} module remove [{ModuleScopeOption} <{ModuleScopeLocalValue}|{ModuleScopeGlobalValue}>]");
                Console.WriteLine($"  {ProductName} module info [{ModuleScopeOption} <{ModuleScopeLocalValue}|{ModuleScopeGlobalValue}>]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine($"  {ModuleVersionOption} <version>      Optional specific version for install/update.");
                Console.WriteLine($"  {ModuleScopeOption} <scope>         Module storage scope: '{ModuleScopeLocalValue}' (default) or '{ModuleScopeGlobalValue}'.");
                Console.WriteLine($"  {ModuleForceOption}                 Overwrite existing target version folder for update operations.");
                Console.WriteLine();
                Console.WriteLine("Notes:");
                Console.WriteLine($"  - install: fails when Kestrun is already installed; use '{ProductName} module update'.");
                Console.WriteLine($"  - update: updates to latest when no --version is provided and fails if the target version folder exists unless {ModuleForceOption} is supplied.");
                Console.WriteLine("  - remove: removes all installed versions from the selected scope and shows deletion progress in interactive terminals.");
                Console.WriteLine("  - info: shows installed module versions and latest Gallery version for the selected scope.");
                Console.WriteLine("  - Windows global scope for install/update/remove prompts for elevation (UAC) when needed.");
                break;

            case "service":
                Console.WriteLine("Usage:");
                Console.WriteLine("  kestrun [--nocheck] [--kestrun-folder <folder>] [--kestrun-manifest <path-to-Kestrun.psd1>] service install --name <service-name> [--service-log-path <path-to-log-file>] [--deployment-root <folder>] [--content-root <folder>] [--script <main.ps1> | <main.ps1>] [--arguments <script arguments...>]");
                Console.WriteLine("  kestrun service remove --name <service-name>");
                Console.WriteLine("  kestrun service start --name <service-name>");
                Console.WriteLine("  kestrun service stop --name <service-name>");
                Console.WriteLine("  kestrun service query --name <service-name>");
                Console.WriteLine();
                Console.WriteLine("Options (service install):");
                Console.WriteLine("  --script <path>             Optional named script path (alternative to positional <main.ps1>).");
                Console.WriteLine("  --content-root <folder>     Copy the full folder into the service bundle; --script is resolved relative to this folder.");
                Console.WriteLine("  --deployment-root <folder>  Override where per-service bundles are created (default is OS-specific).");
                Console.WriteLine("  --kestrun-manifest <path>   Use an explicit Kestrun.psd1 manifest for the service runtime.");
                Console.WriteLine("  --service-log-path <path>   Set service bootstrap/operation log file path.");
                Console.WriteLine("  --arguments <args...>       Pass remaining values to the installed script.");
                Console.WriteLine();
                Console.WriteLine("Notes:");
                Console.WriteLine("  - install registers the service/daemon but does not auto-start it.");
                Console.WriteLine("  - If no script is provided, ./server.ps1 is used.");
                Console.WriteLine("  - When --content-root is provided, the script path must be relative to that folder and must exist inside it.");
                Console.WriteLine("  - --deployment-root overrides the OS default bundle root used during install and remove cleanup.");
                Console.WriteLine("  - install snapshots runtime/module/script plus dedicated service-host from Kestrun.Tool payload into a per-service bundle before registration.");
                Console.WriteLine("  - install shows progress bars during bundle staging in interactive terminals.");
                Console.WriteLine("  - bundle roots: Windows %ProgramData%\\Kestrun\\services; Linux /var/kestrun/services or /usr/local/kestrun/services (with user fallback); macOS /usr/local/kestrun/services (with user fallback).");
                Console.WriteLine("  - remove/start/stop/query require --name and do not accept script paths.");
                Console.WriteLine($"  - Use '{ProductName} module install' before service install when {ModuleName} is not available.");
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
    /// Prints the KestrunTool version.
    /// </summary>
    private static void PrintVersion()
    {
        var version = GetProductVersion();
        Console.WriteLine($"{ProductName} {version}");
    }

    /// <summary>
    /// Prints diagnostic information about the KestrunTool build and runtime.
    /// </summary>
    private static void PrintInfo()
    {
        var version = GetProductVersion();
        var assembly = typeof(Program).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        Console.WriteLine($"Product: {ProductName}");
        Console.WriteLine($"Version: {version}");
        Console.WriteLine($"InformationalVersion: {informationalVersion}");
        Console.WriteLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
        Console.WriteLine($"OSArchitecture: {RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"ProcessArchitecture: {RuntimeInformation.ProcessArchitecture}");
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
        var moduleRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var modulePathRaw = Environment.GetEnvironmentVariable("PSModulePath");
        if (!string.IsNullOrWhiteSpace(modulePathRaw))
        {
            foreach (var root in modulePathRaw.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(root))
                {
                    _ = moduleRoots.Add(root);
                }
            }
        }

        // Service and elevated contexts can have an incomplete PSModulePath.
        // Always include the conventional user/global module roots as discovery fallbacks.
        _ = moduleRoots.Add(GetDefaultPowerShellModulePath());
        _ = moduleRoots.Add(GetGlobalPowerShellModulePath());

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

    [GeneratedRegex("--service-log-path\\s+(\\\"(?<quoted>[^\\\"]+)\\\"|(?<plain>\\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex();
    [GeneratedRegex("^\\s*ModuleVersion\\s*=\\s*['\\\"](?<value>[^'\\\"]+)['\\\"]", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex1();
    [GeneratedRegex("^\\s*Prerelease\\s*=\\s*['\\\"](?<value>[^'\\\"]+)['\\\"]", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex2();
}
