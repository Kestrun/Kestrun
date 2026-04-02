using System.Reflection;
using Kestrun.Runner;
using System.Diagnostics;
using System.ComponentModel;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.IO.Compression;
using System.Formats.Tar;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Kestrun.Tool;

internal static partial class Program
{
    private static int Main(string[] args)
    {
        if (TryHandleInternalServiceRegisterMode(args, out var serviceRegisterExitCode))
        {
            return serviceRegisterExitCode;
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

        if (TryDispatchParsedCommand(parsedCommand, globalOptions, args, out var commandExitCode))
        {
            return commandExitCode;
        }
        // If the command was not handled by dispatch, it must be a run command. Execute the default run mode path.
        return ExecuteRunMode(parsedCommand, globalOptions.SkipGalleryCheck);
    }

    /// <summary>
    /// Handles internal Windows-only service registration mode prior to normal command parsing.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="exitCode">Exit code when internal service registration mode is handled.</param>
    /// <returns>True when internal service registration mode was handled.</returns>
    private static bool TryHandleInternalServiceRegisterMode(string[] args, out int exitCode)
    {
        exitCode = 0;

        if (TryParseServiceRegisterArguments(args, out var serviceRegisterOptions, out var serviceRegisterError))
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("Internal service registration mode is only supported on Windows.");
                exitCode = 1;
                return true;
            }

            exitCode = RegisterWindowsService(serviceRegisterOptions!);
            return true;
        }

        if (string.IsNullOrWhiteSpace(serviceRegisterError))
        {
            return false;
        }

        Console.Error.WriteLine(serviceRegisterError);
        exitCode = 2;
        return true;
    }

    /// <summary>
    /// Dispatches parsed non-run commands and returns an exit code when handled.
    /// </summary>
    /// <param name="parsedCommand">Parsed command.</param>
    /// <param name="globalOptions">Parsed global options.</param>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="exitCode">Exit code when command is handled.</param>
    /// <returns>True when the command mode is handled by dispatch.</returns>
    private static bool TryDispatchParsedCommand(ParsedCommand parsedCommand, GlobalOptions globalOptions, string[] args, out int exitCode)
    {
        switch (parsedCommand.Mode)
        {
            case CommandMode.ServiceInstall:
                exitCode = InstallService(parsedCommand, globalOptions.SkipGalleryCheck);
                return true;
            case CommandMode.ServiceUpdate:
                exitCode = UpdateService(parsedCommand);
                return true;
            case CommandMode.ModuleInstall:
            case CommandMode.ModuleUpdate:
            case CommandMode.ModuleRemove:
            case CommandMode.ModuleInfo:
                exitCode = HandleModuleCommand(parsedCommand, args);
                return true;
            case CommandMode.ServiceRemove:
                exitCode = HandleServiceRemoveCommand(parsedCommand, args);
                return true;
            case CommandMode.ServiceStart:
                exitCode = HandleServiceStartCommand(parsedCommand, args);
                return true;
            case CommandMode.ServiceStop:
                exitCode = HandleServiceStopCommand(parsedCommand, args);
                return true;
            case CommandMode.ServiceQuery:
                exitCode = QueryService(parsedCommand);
                return true;
            case CommandMode.ServiceInfo:
                exitCode = InfoService(parsedCommand);
                return true;
            default:
                exitCode = 0;
                return false;
        }
    }

    /// <summary>
    /// Handles module command execution, including Windows elevation for global scope changes.
    /// </summary>
    /// <param name="parsedCommand">Parsed module command.</param>
    /// <param name="args">Raw command-line arguments.</param>
    /// <returns>Process exit code.</returns>
    private static int HandleModuleCommand(ParsedCommand parsedCommand, string[] args)
    {
        if (OperatingSystem.IsWindows() && RequiresWindowsElevationForGlobalModuleOperation(parsedCommand))
        {
            return RelaunchElevatedOnWindows(args);
        }

        // For non-Windows OSes, attempt module management without elevation and rely on error handling for permission issues.
        return ManageModuleCommand(parsedCommand);
    }

    /// <summary>
    /// Returns true when a module install/update/remove command requires Windows elevation.
    /// </summary>
    /// <param name="parsedCommand">Parsed command.</param>
    /// <returns>True when the command targets global scope on Windows without elevation.</returns>
    private static bool RequiresWindowsElevationForGlobalModuleOperation(ParsedCommand parsedCommand)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }
        // Global scope module operations require admin rights on Windows.
        return parsedCommand.Mode is CommandMode.ModuleInstall or CommandMode.ModuleUpdate or CommandMode.ModuleRemove
            && parsedCommand.ModuleScope == ModuleStorageScope.Global
            && !IsWindowsAdministrator();
    }

    /// <summary>
    /// Handles service remove command execution and Windows elevation preflight.
    /// </summary>
    /// <param name="parsedCommand">Parsed service command.</param>
    /// <param name="args">Raw command-line arguments.</param>
    /// <returns>Process exit code.</returns>
    private static int HandleServiceRemoveCommand(ParsedCommand parsedCommand, string[] args)
    {
        if (OperatingSystem.IsWindows() && !IsWindowsAdministrator())
        {
            return !TryPreflightWindowsServiceRemove(parsedCommand, out var preflightExitCode)
                ? preflightExitCode
                : RelaunchElevatedOnWindows(args);
        }

        // For non-Windows OSes, attempt removal without elevation and rely on permission/service-state errors.
        return RemoveService(parsedCommand);
    }

    /// <summary>
    /// Handles service start command execution and Windows elevation preflight.
    /// </summary>
    /// <param name="parsedCommand">Parsed service command.</param>
    /// <param name="args">Raw command-line arguments.</param>
    /// <returns>Process exit code.</returns>
    private static int HandleServiceStartCommand(ParsedCommand parsedCommand, string[] args)
    {
        if (OperatingSystem.IsWindows() && !IsWindowsAdministrator())
        {
            if (!TryPreflightWindowsServiceControl(parsedCommand, out var preflightExitCode, out var preflightMessage))
            {
                if (parsedCommand.RawOutput)
                {
                    Console.Error.WriteLine(preflightMessage);
                    return preflightExitCode;
                }

                return WriteServiceControlResult(
                    parsedCommand,
                    new ServiceControlResult(
                        "start",
                        parsedCommand.ServiceName ?? string.Empty,
                        "windows",
                        "unknown",
                        null,
                        preflightExitCode,
                        preflightMessage,
                        string.Empty,
                        string.Empty));
            }

            var relaunchExitCode = RelaunchElevatedOnWindows(args, suppressStatusMessages: true);
            return relaunchExitCode == 1223 && !parsedCommand.RawOutput
                ? WriteServiceControlResult(
                    parsedCommand,
                    new ServiceControlResult(
                        "start",
                        parsedCommand.ServiceName ?? string.Empty,
                        "windows",
                        "unknown",
                        null,
                        1,
                        "Elevation was canceled by the user. Run this command from an elevated terminal if you want to proceed without UAC interaction.",
                        string.Empty,
                        string.Empty))
                : relaunchExitCode;
        }

        // For non-Windows OSes, attempt start without elevation and rely on permission/service-state errors.
        return StartService(parsedCommand);
    }

    /// <summary>
    /// Handles service stop command execution and Windows elevation preflight.
    /// </summary>
    /// <param name="parsedCommand">Parsed service command.</param>
    /// <param name="args">Raw command-line arguments.</param>
    /// <returns>Process exit code.</returns>
    private static int HandleServiceStopCommand(ParsedCommand parsedCommand, string[] args)
    {
        if (OperatingSystem.IsWindows() && !IsWindowsAdministrator())
        {
            if (!TryPreflightWindowsServiceControl(parsedCommand, out var preflightExitCode, out var preflightMessage))
            {
                if (parsedCommand.RawOutput)
                {
                    Console.Error.WriteLine(preflightMessage);
                    return preflightExitCode;
                }

                return WriteServiceControlResult(
                    parsedCommand,
                    new ServiceControlResult(
                        "stop",
                        parsedCommand.ServiceName ?? string.Empty,
                        "windows",
                        "unknown",
                        null,
                        preflightExitCode,
                        preflightMessage,
                        string.Empty,
                        string.Empty));
            }

            var relaunchExitCode = RelaunchElevatedOnWindows(args, suppressStatusMessages: true);
            return relaunchExitCode == 1223 && !parsedCommand.RawOutput
                ? WriteServiceControlResult(
                    parsedCommand,
                    new ServiceControlResult(
                        "stop",
                        parsedCommand.ServiceName ?? string.Empty,
                        "windows",
                        "unknown",
                        null,
                        1,
                        "Elevation was canceled by the user. Run this command from an elevated terminal if you want to proceed without UAC interaction.",
                        string.Empty,
                        string.Empty))
                : relaunchExitCode;
        }

        // For non-Windows OSes, attempt stop without elevation and rely on permission/service-state errors.
        return StopService(parsedCommand);
    }

    /// <summary>
    /// Executes the default run mode path after command parsing succeeds.
    /// </summary>
    /// <param name="parsedCommand">Parsed run command.</param>
    /// <param name="skipGalleryCheck">True to skip gallery version checks.</param>
    /// <returns>Process exit code.</returns>
    private static int ExecuteRunMode(ParsedCommand parsedCommand, bool skipGalleryCheck)
    {
        var fullScriptPath = Path.GetFullPath(parsedCommand.ScriptPath);
        if (!File.Exists(fullScriptPath))
        {
            Console.Error.WriteLine($"Script file not found: {fullScriptPath}");
            return 2;
        }

        var moduleManifestPath = ResolveRunModuleManifestPath(parsedCommand.KestrunManifestPath, parsedCommand.KestrunFolder);
        if (moduleManifestPath is null)
        {
            WriteModuleNotFoundMessage(parsedCommand.KestrunManifestPath, parsedCommand.KestrunFolder, Console.Error.WriteLine);
            return 3;
        }

        if (!skipGalleryCheck)
        {
            WarnIfNewerGalleryVersionExists(moduleManifestPath);
        }

        try
        {
            return ExecuteScriptViaServiceHost(fullScriptPath, parsedCommand.ScriptArguments, moduleManifestPath);
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
    /// Performs non-admin checks before elevating a Windows service install request.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <param name="serviceName">Resolved service name.</param>
    /// <param name="exitCode">Exit code when preflight fails.</param>
    /// <returns>True when install should proceed with elevation.</returns>
    [SupportedOSPlatform("windows")]
    private static bool TryPreflightWindowsServiceInstall(ParsedCommand command, string serviceName, out int exitCode)
    {
        exitCode = 0;
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            Console.Error.WriteLine("Service name is required. Use --name <value>.");
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

        if (WindowsServiceExists(serviceName))
        {
            Console.Error.WriteLine($"Windows service '{serviceName}' already exists.");
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
    /// <param name="errorMessage">Preflight failure message when validation fails.</param>
    /// <returns>True when control should proceed with elevation.</returns>
    [SupportedOSPlatform("windows")]
    private static bool TryPreflightWindowsServiceControl(ParsedCommand command, out int exitCode, out string errorMessage)
    {
        exitCode = 0;
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(command.ServiceName))
        {
            exitCode = 2;
            errorMessage = "Service name is required. Use --name <value>.";
            return false;
        }

        if (!WindowsServiceExists(command.ServiceName))
        {
            exitCode = 2;
            errorMessage = $"Windows service '{command.ServiceName}' was not found.";
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
    private static int RelaunchElevatedOnWindows(IReadOnlyList<string> args, string? exePath = null, bool suppressStatusMessages = false)
    {
        exePath ??= Environment.ProcessPath;
        if (!TryResolveElevationExecutablePath(exePath, out var resolvedExePath))
        {
            return 1;
        }

        if (!suppressStatusMessages)
        {
            Console.Error.WriteLine("Administrator rights are required. Requesting elevation...");
        }

        var relaunchArgs = BuildElevatedRelaunchArguments(resolvedExePath, args);
        var tempDirectory = Path.Combine(Path.GetTempPath(), ProductName);
        _ = Directory.CreateDirectory(tempDirectory);

        var outputPath = Path.Combine(tempDirectory, $"elevated-{Guid.NewGuid():N}.log");
        var wrapperPath = Path.Combine(tempDirectory, $"elevated-{Guid.NewGuid():N}.cmd");

        WriteElevationWrapperScript(wrapperPath, outputPath, resolvedExePath, relaunchArgs);

        try
        {
            return StartElevatedProcess(wrapperPath, outputPath, suppressStatusMessages);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            WriteElevationCanceledMessage(suppressStatusMessages);
            return 1223;
        }
        catch (Exception ex)
        {
            WriteElevationFailureMessage(ex.Message, suppressStatusMessages);
            return 1;
        }
        finally
        {
            TryDeleteFileQuietly(wrapperPath);
            TryDeleteFileQuietly(outputPath);
        }
    }

    /// <summary>
    /// Resolves and validates the executable path used for elevation relaunch.
    /// </summary>
    /// <param name="exePath">Input executable path.</param>
    /// <param name="resolvedExePath">Resolved executable path when validation succeeds.</param>
    /// <returns>True when the executable path is valid.</returns>
    private static bool TryResolveElevationExecutablePath(string? exePath, out string resolvedExePath)
    {
        resolvedExePath = exePath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedExePath) || !File.Exists(resolvedExePath))
        {
            Console.Error.WriteLine("Unable to resolve KestrunTool executable path for elevation.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Writes the temporary wrapper script used to capture elevated process output.
    /// </summary>
    /// <param name="wrapperPath">Wrapper script path.</param>
    /// <param name="outputPath">Output capture file path.</param>
    /// <param name="exePath">Executable path to launch.</param>
    /// <param name="relaunchArgs">Relaunch argument tokens.</param>
    private static void WriteElevationWrapperScript(string wrapperPath, string outputPath, string exePath, IReadOnlyList<string> relaunchArgs)
    {
        var commandLine = BuildWindowsCommandLine(exePath, relaunchArgs);
        var wrapperContents = $"@echo off{Environment.NewLine}{commandLine} > \"{outputPath}\" 2>&1{Environment.NewLine}exit /b %errorlevel%{Environment.NewLine}";
        File.WriteAllText(wrapperPath, wrapperContents, Encoding.ASCII);
    }

    /// <summary>
    /// Starts the elevated wrapper process and relays captured output.
    /// </summary>
    /// <param name="wrapperPath">Wrapper script path.</param>
    /// <param name="outputPath">Output capture file path.</param>
    /// <param name="suppressStatusMessages">True to suppress non-essential status messages.</param>
    /// <returns>Exit code from the elevated child process.</returns>
    private static int StartElevatedProcess(string wrapperPath, string outputPath, bool suppressStatusMessages)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{wrapperPath}\"",
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Environment.CurrentDirectory,
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            Console.Error.WriteLine("Failed to start elevated process.");
            return 1;
        }

        process.WaitForExit();
        RelayElevatedOutput(outputPath);

        if (process.ExitCode != 0 && !suppressStatusMessages)
        {
            Console.Error.WriteLine("Elevated operation failed. If no UAC prompt was shown, run this command from an elevated terminal.");
        }

        return process.ExitCode;
    }

    /// <summary>
    /// Writes captured elevated output to standard output when available.
    /// </summary>
    /// <param name="outputPath">Output capture file path.</param>
    private static void RelayElevatedOutput(string outputPath)
    {
        if (!File.Exists(outputPath))
        {
            return;
        }

        var elevatedOutput = File.ReadAllText(outputPath);
        if (!string.IsNullOrWhiteSpace(elevatedOutput))
        {
            Console.Write(elevatedOutput);
        }
    }

    /// <summary>
    /// Writes the standard elevation canceled message when status output is enabled.
    /// </summary>
    /// <param name="suppressStatusMessages">True to suppress status messages.</param>
    private static void WriteElevationCanceledMessage(bool suppressStatusMessages)
    {
        if (suppressStatusMessages)
        {
            return;
        }

        Console.Error.WriteLine("Elevation was canceled by the user.");
        Console.Error.WriteLine("Run this command from an elevated terminal if you want to proceed without UAC interaction.");
    }

    /// <summary>
    /// Writes the standard elevation failure message when status output is enabled.
    /// </summary>
    /// <param name="errorMessage">Error message from the failed elevation attempt.</param>
    /// <param name="suppressStatusMessages">True to suppress status messages.</param>
    private static void WriteElevationFailureMessage(string errorMessage, bool suppressStatusMessages)
    {
        if (suppressStatusMessages)
        {
            return;
        }

        Console.Error.WriteLine($"Failed to elevate process: {errorMessage}");
        Console.Error.WriteLine("Run this command from an elevated terminal if automatic elevation is unavailable.");
    }

    /// <summary>
    /// Best-effort delete for temporary files used by elevated relaunch flow.
    /// </summary>
    /// <param name="path">File path to remove.</param>
    private static void TryDeleteFileQuietly(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup only.
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
    /// <param name="serviceName">Resolved service name.</param>
    /// <param name="serviceLogPath">Effective service log path.</param>
    /// <param name="serviceHostExecutablePath">Service host executable path.</param>
    /// <param name="runnerExecutablePath">Runner executable path.</param>
    /// <param name="scriptPath">Bundled script path.</param>
    /// <param name="moduleManifestPath">Bundled module manifest path.</param>
    /// <returns>Process exit code.</returns>
    [SupportedOSPlatform("windows")]
    private static int InstallWindowsService(
        ParsedCommand command,
        string serviceName,
        string? serviceLogPath,
        string serviceHostExecutablePath,
        string runnerExecutablePath,
        string scriptPath,
        string moduleManifestPath)
    {
        if (!IsWindowsAdministrator())
        {
            var relaunchArgs = BuildWindowsServiceRegisterArguments(command, serviceName, serviceLogPath, serviceHostExecutablePath, runnerExecutablePath, scriptPath, moduleManifestPath);
            return RelaunchElevatedOnWindows(relaunchArgs);
        }

        var createResult = CreateWindowsServiceRegistration(
            serviceName,
            Path.GetFullPath(serviceHostExecutablePath),
            Path.GetFullPath(runnerExecutablePath),
            Path.GetFullPath(scriptPath),
            Path.GetFullPath(moduleManifestPath),
            command.ScriptArguments,
            serviceLogPath,
            command.ServiceUser,
            command.ServicePassword);

        if (createResult.ExitCode != 0)
        {
            Console.Error.WriteLine(createResult.Error);
            return createResult.ExitCode;
        }

        WriteServiceOperationLog($"Service '{serviceName}' install operation completed.", serviceLogPath, serviceName);

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
            options.ServiceLogPath,
            options.ServiceUser,
            options.ServicePassword);

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
        string? serviceLogPath,
        string? serviceUser,
        string? servicePassword)
    {
        if (!UsesDedicatedServiceHostExecutable(serviceHostExecutablePath))
        {
            return new ProcessResult(
                1,
                string.Empty,
                "Service registration now requires the dedicated kestrun-service-host executable. Reinstall or update Kestrun.Tool.");
        }

        var hostArgs = BuildDedicatedServiceHostArguments(serviceName, runnerExecutablePath, scriptPath, moduleManifestPath, scriptArguments, serviceLogPath);

        var imagePath = BuildWindowsCommandLine(serviceHostExecutablePath, hostArgs);
        var scArgs = new List<string>
        {
            "create",
            serviceName,
            "start=",
            "auto",
            "binPath=",
            imagePath,
            "DisplayName=",
            serviceName,
        };

        if (!string.IsNullOrWhiteSpace(serviceUser))
        {
            var normalizedServiceUser = NormalizeWindowsServiceAccountName(serviceUser);
            scArgs.Add("obj=");
            scArgs.Add(normalizedServiceUser);

            // Windows built-in service accounts do not require a password.
            if (!IsWindowsBuiltinServiceAccount(normalizedServiceUser) && !string.IsNullOrWhiteSpace(servicePassword))
            {
                scArgs.Add("password=");
                scArgs.Add(servicePassword);
            }
        }

        return RunProcess("sc.exe", scArgs);
    }

    /// <summary>
    /// Normalizes friendly Windows built-in service account aliases to SCM-compatible names.
    /// </summary>
    /// <param name="serviceUser">Raw service user argument.</param>
    /// <returns>Normalized account name for sc.exe registration.</returns>
    private static string NormalizeWindowsServiceAccountName(string serviceUser)
    {
        var trimmed = serviceUser.Trim();

        return trimmed.ToLowerInvariant() switch
        {
            "networkservice" or "network service" or @"nt authority\networkservice" => @"NT AUTHORITY\NetworkService",
            "localservice" or "local service" or @"nt authority\localservice" => @"NT AUTHORITY\LocalService",
            "localsystem" or "local system" or "system" or @"nt authority\system" => "LocalSystem",
            _ => trimmed,
        };
    }

    /// <summary>
    /// Determines whether an account string refers to a Windows built-in service account.
    /// </summary>
    /// <param name="accountName">Account name to inspect.</param>
    /// <returns>True when account is LocalSystem, NetworkService, or LocalService.</returns>
    private static bool IsWindowsBuiltinServiceAccount(string accountName)
    {
        return accountName.Equals("LocalSystem", StringComparison.OrdinalIgnoreCase)
            || accountName.Equals(@"NT AUTHORITY\NetworkService", StringComparison.OrdinalIgnoreCase)
            || accountName.Equals(@"NT AUTHORITY\LocalService", StringComparison.OrdinalIgnoreCase);
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
    /// <param name="serviceName">Resolved service name.</param>
    /// <param name="serviceLogPath">Effective service log path.</param>
    /// <param name="executablePath">Executable path.</param>
    /// <param name="scriptPath">Absolute script path.</param>
    /// <param name="moduleManifestPath">Manifest path staged for service runtime.</param>
    /// <returns>Ordered argument tokens.</returns>
    private static IReadOnlyList<string> BuildWindowsServiceRegisterArguments(
        ParsedCommand command,
        string serviceName,
        string? serviceLogPath,
        string serviceHostExecutablePath,
        string runnerExecutablePath,
        string scriptPath,
        string moduleManifestPath)
    {
        var arguments = new List<string>(16 + command.ScriptArguments.Length)
        {
            "--service-register",
            "--name",
            serviceName,
            "--service-host-exe",
            Path.GetFullPath(serviceHostExecutablePath),
            "--runner-exe",
            Path.GetFullPath(runnerExecutablePath),
            "--script",
            Path.GetFullPath(scriptPath),
            "--kestrun-manifest",
            Path.GetFullPath(moduleManifestPath),
        };

        if (!string.IsNullOrWhiteSpace(serviceLogPath))
        {
            arguments.Add("--service-log-path");
            arguments.Add(Path.GetFullPath(serviceLogPath));
        }

        if (!string.IsNullOrWhiteSpace(command.ServiceUser))
        {
            arguments.Add("--service-user");
            arguments.Add(command.ServiceUser);
        }

        if (!string.IsNullOrWhiteSpace(command.ServicePassword))
        {
            arguments.Add("--service-password");
            arguments.Add(command.ServicePassword);
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
    /// Removes a Windows service using sc.exe.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <returns>Process exit code.</returns>
    private static int RemoveWindowsService(ParsedCommand command)
    {
        var operationLogPath = ResolveServiceOperationLogPath(command.ServiceLogPath, command.ServiceName);

        var stopResult = RunProcess("sc.exe", ["stop", command.ServiceName!], writeStandardOutput: false);
        if (stopResult.ExitCode != 0 && !IsWindowsServiceAlreadyStopped(stopResult))
        {
            WriteServiceOperationLog(
                $"Service '{command.ServiceName}' stop-before-delete returned exitCode={stopResult.ExitCode} error='{stopResult.Error.Trim()}'",
                operationLogPath,
                command.ServiceName);
        }
        else if (!WaitForWindowsServiceToStopOrDisappear(command.ServiceName!, timeoutMs: 15000))
        {
            WriteServiceOperationLog(
                $"Service '{command.ServiceName}' did not reach STOPPED/deleted state before delete attempt.",
                operationLogPath,
                command.ServiceName);
        }

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
    /// Waits until a Windows service reaches STOPPED state or is no longer present in SCM.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="timeoutMs">Maximum wait time in milliseconds.</param>
    /// <param name="pollIntervalMs">Polling interval in milliseconds.</param>
    /// <returns>True when the service is stopped or deleted before timeout.</returns>
    private static bool WaitForWindowsServiceToStopOrDisappear(string serviceName, int timeoutMs = 15000, int pollIntervalMs = 300)
    {
        var timeout = TimeSpan.FromMilliseconds(Math.Max(timeoutMs, pollIntervalMs));
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow <= deadline)
        {
            var queryResult = RunProcess("sc.exe", ["query", serviceName], writeStandardOutput: false);
            var diagnostics = $"{queryResult.Output}\n{queryResult.Error}";

            if (queryResult.ExitCode == 0)
            {
                if (diagnostics.Contains("STOPPED", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (diagnostics.Contains("1060", StringComparison.OrdinalIgnoreCase)
                || diagnostics.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Thread.Sleep(pollIntervalMs);
        }

        return false;
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
    /// Writes a standardized service operation result entry.
    /// </summary>
    /// <param name="operation">Operation name (install/start/stop/query/remove).</param>
    /// <param name="platform">Platform label.</param>
    /// <param name="serviceName">Service name.</param>
    /// <param name="exitCode">Process exit code.</param>
    /// <param name="configuredPath">Optional configured log path.</param>
    private static void WriteServiceOperationResult(string operation, string platform, string serviceName, int exitCode, string? configuredPath)
    {
        var result = exitCode == 0 ? "success" : "failed";
        WriteServiceOperationLog(
            $"operation='{operation}' service='{serviceName}' platform='{platform}' result='{result}' exitCode={exitCode}",
            configuredPath,
            serviceName);
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
        var defaultPath = RunnerRuntime.ResolveBootstrapLogPath(null, defaultFileName);

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
        var match = ServiceLogPathRegex().Match(text);

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

        var runtimeBinaryName = OperatingSystem.IsWindows() ? WindowsServiceRuntimeBinaryName : UnixServiceRuntimeBinaryName;
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
        var candidates = new List<string>();

        // 1. Development/source path: ./src/CSharp/Kestrun.Tool/kestrun-service/<rid>/
        // Prefer this during local builds/tests to avoid accidentally selecting a stale globally installed tool payload.
        foreach (var parent in EnumerateDirectoryAndParents(Environment.CurrentDirectory))
        {
            candidates.Add(Path.Combine(parent, "src", "CSharp", "Kestrun.Tool", "kestrun-service", runtimeRid, hostBinaryName));
        }

        // 2. Dotnet tool store path: ~/.dotnet/tools/.store/kestrun.tool/<version>/
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(homeDirectory))
        {
            var toolStorePath = Path.Combine(homeDirectory, ".dotnet", "tools", ".store", "kestrun.tool");
            if (Directory.Exists(toolStorePath))
            {
                foreach (var versionDir in Directory.GetDirectories(toolStorePath))
                {
                    candidates.Add(Path.Combine(versionDir, "kestrun.tool", Path.GetFileName(versionDir), "tools", "net10.0", "any", "kestrun-service", runtimeRid, hostBinaryName));
                }
            }
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
    /// Resolves service-install script source, including optional content-root semantics.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <param name="scriptSource">Resolved script source details.</param>
    /// <param name="error">Error details when validation fails.</param>
    /// <returns>True when script source is valid and exists.</returns>
    private static bool TryResolveServiceScriptSource(ParsedCommand command, out ResolvedServiceScriptSource scriptSource, out string error)
    {
        var optionFlags = GetServiceContentRootOptionFlags(command);
        if (!TryClassifyServiceContentRoot(command.ServiceContentRoot, out _, out var contentRootUri, out var fullContentRoot))
        {
            var fallbackScriptPath = ResolveRequestedServiceScriptPath(command.ScriptPath, useDefaultWhenMissing: true);
            return TryResolveServiceScriptWithoutContentRoot(fallbackScriptPath, optionFlags, out scriptSource, out error);
        }

        if (command.Mode == CommandMode.ServiceInstall && command.ServiceNameProvided)
        {
            scriptSource = CreateEmptyResolvedServiceScriptSource();
            error = "--name is no longer supported for service install. Define Name in Service.psd1 inside the package.";
            return false;
        }

        if (command.ScriptPathProvided)
        {
            scriptSource = CreateEmptyResolvedServiceScriptSource();
            error = "An explicit script path is not supported when --package is used. Define EntryPoint in Service.psd1 (format 1.0).";
            return false;
        }

        var requestedScriptPath = ResolveRequestedServiceScriptPath(command.ScriptPath, useDefaultWhenMissing: false);
        // When a content-root value is supplied, attempt to resolve the script source from the content root, even if the script path argument is not provided,
        // since some content-root scenarios imply a default script name.
        return TryResolveServiceScriptFromContentRoot(
            command,
            requestedScriptPath,
            contentRootUri,
            fullContentRoot,
            optionFlags,
            out scriptSource,
            out error);
    }

    /// <summary>
    /// Classifies service content-root input into normalized local path or HTTP(S) URI forms.
    /// </summary>
    /// <param name="rawContentRoot">Raw content-root token from CLI arguments.</param>
    /// <param name="normalizedContentRoot">Trimmed content-root token.</param>
    /// <param name="contentRootUri">Parsed HTTP(S) URI when the content root is remote; otherwise null.</param>
    /// <param name="fullContentRoot">Normalized absolute local path when the content root is local; otherwise empty.</param>
    /// <returns>True when a content-root value exists; false when no content-root was supplied.</returns>
    private static bool TryClassifyServiceContentRoot(
        string? rawContentRoot,
        out string normalizedContentRoot,
        out Uri? contentRootUri,
        out string fullContentRoot)
    {
        normalizedContentRoot = string.Empty;
        contentRootUri = null;
        fullContentRoot = string.Empty;

        if (string.IsNullOrWhiteSpace(rawContentRoot))
        {
            return false;
        }

        normalizedContentRoot = rawContentRoot.Trim();
        if (TryParseServiceContentRootHttpUri(normalizedContentRoot, out var parsedUri))
        {
            contentRootUri = parsedUri;
            return true;
        }

        fullContentRoot = Path.GetFullPath(normalizedContentRoot);
        return true;
    }

    /// <summary>
    /// Resolves service script source for a classified content-root input.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <param name="requestedScriptPath">Requested script path argument.</param>
    /// <param name="contentRootUri">Parsed HTTP(S) content-root URI when applicable.</param>
    /// <param name="fullContentRoot">Absolute local content-root path when applicable.</param>
    /// <param name="optionFlags">Content-root related option usage flags.</param>
    /// <param name="scriptSource">Resolved script source details.</param>
    /// <param name="error">Error details when resolution fails.</param>
    /// <returns>True when script source resolution succeeds.</returns>
    private static bool TryResolveServiceScriptFromContentRoot(
        ParsedCommand command,
        string requestedScriptPath,
        Uri? contentRootUri,
        string fullContentRoot,
        ServiceContentRootOptionFlags optionFlags,
        out ResolvedServiceScriptSource scriptSource,
        out string error)
    {
        if (contentRootUri is not null)
        {
            return TryResolveServiceScriptFromHttpContentRoot(command, requestedScriptPath, contentRootUri, out scriptSource, out error);
        }

        if (Directory.Exists(fullContentRoot))
        {
            return TryResolveServiceScriptFromDirectoryContentRoot(requestedScriptPath, fullContentRoot, optionFlags, out scriptSource, out error);
        }
        // When content-root is supplied but does not exist as a local directory, attempt to treat it as an archive path for better UX in common scenarios where users point content-root
        // to a file without realizing that only directories are supported for local content roots.
        return TryResolveServiceScriptFromArchiveContentRoot(command, requestedScriptPath, fullContentRoot, optionFlags, out scriptSource, out error);
    }

    /// <summary>
    /// Represents parsed option usage for content-root related arguments.
    /// </summary>
    private readonly record struct ServiceContentRootOptionFlags(
        bool HasArchiveChecksum,
        bool HasBearerToken,
        bool IgnoreCertificate,
        bool HasHeaders);

    /// <summary>
    /// Resolves the script path value for service install commands, applying default script fallback.
    /// </summary>
    /// <param name="scriptPath">Requested script path argument.</param>
    /// <param name="useDefaultWhenMissing">True to apply the default script file name when no script path was provided.</param>
    /// <returns>Resolved script path token.</returns>
    private static string ResolveRequestedServiceScriptPath(string scriptPath, bool useDefaultWhenMissing)
        => string.IsNullOrWhiteSpace(scriptPath)
            ? (useDefaultWhenMissing ? ServiceDefaultScriptFileName : string.Empty)
            : scriptPath;

    /// <summary>
    /// Captures which content-root related options were supplied on the command line.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <returns>Normalized option usage flags.</returns>
    private static ServiceContentRootOptionFlags GetServiceContentRootOptionFlags(ParsedCommand command)
        => new(
            !string.IsNullOrWhiteSpace(command.ServiceContentRootChecksum),
            !string.IsNullOrWhiteSpace(command.ServiceContentRootBearerToken),
            command.ServiceContentRootIgnoreCertificate,
            command.ServiceContentRootHeaders.Length > 0);

    /// <summary>
    /// Resolves script source when no content-root argument is provided.
    /// </summary>
    /// <param name="requestedScriptPath">Requested script path.</param>
    /// <param name="optionFlags">Content-root related option flags.</param>
    /// <param name="scriptSource">Resolved script source details.</param>
    /// <param name="error">Error details when validation fails.</param>
    /// <returns>True when script resolution succeeds.</returns>
    private static bool TryResolveServiceScriptWithoutContentRoot(
        string requestedScriptPath,
        ServiceContentRootOptionFlags optionFlags,
        out ResolvedServiceScriptSource scriptSource,
        out string error)
    {
        scriptSource = CreateEmptyResolvedServiceScriptSource();
        if (!TryValidateOptionsForMissingContentRoot(optionFlags, out error))
        {
            return false;
        }

        var fullScriptPath = Path.GetFullPath(requestedScriptPath);
        if (!File.Exists(fullScriptPath))
        {
            error = $"Script file not found: {fullScriptPath}";
            return false;
        }

        scriptSource = new ResolvedServiceScriptSource(fullScriptPath, null, Path.GetFileName(fullScriptPath), null, null, null, null, null, []);
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Resolves script source when content-root points to an HTTP(S) archive.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <param name="requestedScriptPath">Requested script path.</param>
    /// <param name="contentRootUri">HTTP(S) archive URI.</param>
    /// <param name="optionFlags">Content-root related option flags.</param>
    /// <param name="scriptSource">Resolved script source details.</param>
    /// <param name="error">Error details when validation fails.</param>
    /// <returns>True when script resolution succeeds.</returns>
    private static bool TryResolveServiceScriptFromHttpContentRoot(
        ParsedCommand command,
        string requestedScriptPath,
        Uri contentRootUri,
        out ResolvedServiceScriptSource scriptSource,
        out string error)
    {
        scriptSource = CreateEmptyResolvedServiceScriptSource();
        if (!TryValidateHttpContentRootScriptPath(requestedScriptPath, out error))
        {
            return false;
        }

        var temporaryRoot = CreateServiceContentRootExtractionDirectory(command.ServiceName);
        var downloadedContentRoot = Path.Combine(temporaryRoot, "content");
        _ = Directory.CreateDirectory(downloadedContentRoot);

        try
        {
            if (!TryDownloadAndExtractHttpContentRoot(command, contentRootUri, temporaryRoot, downloadedContentRoot, out error))
            {
                TryDeleteDirectoryWithRetry(temporaryRoot, maxAttempts: 5, initialDelayMs: 50);
                return false;
            }

            if (!TryResolveServiceInstallDescriptor(downloadedContentRoot, out var descriptor, out error))
            {
                TryDeleteDirectoryWithRetry(temporaryRoot, maxAttempts: 5, initialDelayMs: 50);
                return false;
            }

            if (!TryResolveServiceDescriptorScriptPath(requestedScriptPath, descriptor, out var resolvedScriptPath, out error))
            {
                TryDeleteDirectoryWithRetry(temporaryRoot, maxAttempts: 5, initialDelayMs: 50);
                return false;
            }

            if (!TryResolveScriptFromResolvedContentRoot(
                    resolvedScriptPath,
                    downloadedContentRoot,
                    $"Script path '{resolvedScriptPath}' escapes the extracted archive content root.",
                    $"Script file '{resolvedScriptPath}' was not found inside extracted archive downloaded from '{contentRootUri}'.",
                    temporaryRoot,
                    out scriptSource,
                    out error))
            {
                TryDeleteDirectoryWithRetry(temporaryRoot, maxAttempts: 5, initialDelayMs: 50);
                return false;
            }

            scriptSource = ApplyDescriptorMetadata(scriptSource, descriptor);

            return true;
        }
        catch
        {
            TryDeleteDirectoryWithRetry(temporaryRoot, maxAttempts: 5, initialDelayMs: 50);
            throw;
        }
    }

    /// <summary>
    /// Resolves script source when content-root points to a local directory.
    /// </summary>
    /// <param name="requestedScriptPath">Requested script path.</param>
    /// <param name="fullContentRoot">Absolute content-root directory path.</param>
    /// <param name="optionFlags">Content-root related option flags.</param>
    /// <param name="scriptSource">Resolved script source details.</param>
    /// <param name="error">Error details when validation fails.</param>
    /// <returns>True when script resolution succeeds.</returns>
    private static bool TryResolveServiceScriptFromDirectoryContentRoot(
        string requestedScriptPath,
        string fullContentRoot,
        ServiceContentRootOptionFlags optionFlags,
        out ResolvedServiceScriptSource scriptSource,
        out string error)
    {
        scriptSource = CreateEmptyResolvedServiceScriptSource();
        if (!TryValidateDirectoryContentRootOptions(optionFlags, out error))
        {
            return false;
        }

        if (!TryResolveServiceInstallDescriptor(fullContentRoot, out var descriptor, out error))
        {
            return false;
        }

        if (!TryResolveServiceDescriptorScriptPath(requestedScriptPath, descriptor, out var resolvedScriptPath, out error))
        {
            return false;
        }

        if (!TryResolveScriptFromResolvedContentRoot(
            resolvedScriptPath,
            fullContentRoot,
            $"Script path '{resolvedScriptPath}' escapes the service content root '{fullContentRoot}'.",
            $"Script file '{resolvedScriptPath}' was not found under service content root '{fullContentRoot}'.",
            null,
            out scriptSource,
            out error))
        {
            return false;
        }

        scriptSource = ApplyDescriptorMetadata(scriptSource, descriptor);
        return true;
    }

    /// <summary>
    /// Resolves script source when content-root points to a local archive file.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <param name="requestedScriptPath">Requested script path.</param>
    /// <param name="fullContentRoot">Absolute archive path.</param>
    /// <param name="optionFlags">Content-root related option flags.</param>
    /// <param name="scriptSource">Resolved script source details.</param>
    /// <param name="error">Error details when validation fails.</param>
    /// <returns>True when script resolution succeeds.</returns>
    private static bool TryResolveServiceScriptFromArchiveContentRoot(
        ParsedCommand command,
        string requestedScriptPath,
        string fullContentRoot,
        ServiceContentRootOptionFlags optionFlags,
        out ResolvedServiceScriptSource scriptSource,
        out string error)
    {
        scriptSource = CreateEmptyResolvedServiceScriptSource();
        if (!File.Exists(fullContentRoot))
        {
            error = $"Service content root path was not found: {fullContentRoot}";
            return false;
        }

        if (!TryValidateLocalArchiveContentRootOptions(optionFlags, out error))
        {
            return false;
        }

        if (!IsSupportedServiceContentRootArchive(fullContentRoot))
        {
            error = $"Unsupported package format. Supported extensions: {ServicePackageExtension}, .zip, .tar, .tgz, .tar.gz.";
            return false;
        }

        if (!TryValidateServiceContentRootArchiveChecksum(command, fullContentRoot, out error))
        {
            return false;
        }

        var extractedContentRoot = CreateServiceContentRootExtractionDirectory(command.ServiceName);
        try
        {
            if (TryResolveServiceScriptFromExtractedArchiveContentRoot(
                    requestedScriptPath,
                    fullContentRoot,
                    extractedContentRoot,
                    out var extractedScriptSource,
                    out error))
            {
                scriptSource = extractedScriptSource;
                return true;
            }

            TryDeleteDirectoryWithRetry(extractedContentRoot, maxAttempts: 5, initialDelayMs: 50);
            return false;
        }
        catch
        {
            TryDeleteDirectoryWithRetry(extractedContentRoot, maxAttempts: 5, initialDelayMs: 50);
            throw;
        }
    }

    /// <summary>
    /// Resolves service script source from an already-created extraction directory for a local archive content root.
    /// </summary>
    /// <param name="requestedScriptPath">Requested script path.</param>
    /// <param name="fullContentRoot">Absolute archive path.</param>
    /// <param name="extractedContentRoot">Archive extraction directory path.</param>
    /// <param name="scriptSource">Resolved script source details.</param>
    /// <param name="error">Error details when validation fails.</param>
    /// <returns>True when script source resolution succeeds.</returns>
    private static bool TryResolveServiceScriptFromExtractedArchiveContentRoot(
        string requestedScriptPath,
        string fullContentRoot,
        string extractedContentRoot,
        out ResolvedServiceScriptSource scriptSource,
        out string error)
    {
        scriptSource = CreateEmptyResolvedServiceScriptSource();

        if (!TryExtractServiceContentRootArchive(fullContentRoot, extractedContentRoot, out error))
        {
            return false;
        }

        if (!TryResolveServiceInstallDescriptor(extractedContentRoot, out var descriptor, out error))
        {
            return false;
        }

        if (!TryResolveServiceDescriptorScriptPath(requestedScriptPath, descriptor, out var resolvedScriptPath, out error))
        {
            return false;
        }

        if (!TryResolveScriptFromResolvedContentRoot(
                resolvedScriptPath,
                extractedContentRoot,
                $"Script path '{resolvedScriptPath}' escapes the extracted archive content root.",
                $"Script file '{resolvedScriptPath}' was not found inside extracted archive '{fullContentRoot}'.",
                extractedContentRoot,
                out scriptSource,
                out error))
        {
            return false;
        }

        scriptSource = ApplyDescriptorMetadata(scriptSource, descriptor);
        return true;
    }

    /// <summary>
    /// Validates content-root options when no content-root argument was supplied.
    /// </summary>
    /// <param name="optionFlags">Content-root related option flags.</param>
    /// <param name="error">Validation error message when invalid combinations are detected.</param>
    /// <returns>True when the option combination is valid.</returns>
    private static bool TryValidateOptionsForMissingContentRoot(ServiceContentRootOptionFlags optionFlags, out string error)
    {
        if (optionFlags.HasArchiveChecksum)
        {
            error = "--content-root-checksum requires --content-root.";
            return false;
        }

        if (optionFlags.HasBearerToken)
        {
            error = "--content-root-bearer-token requires --content-root.";
            return false;
        }

        if (optionFlags.IgnoreCertificate)
        {
            error = "--content-root-ignore-certificate requires --content-root.";
            return false;
        }

        if (optionFlags.HasHeaders)
        {
            error = "--content-root-header requires --content-root.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Validates URL-only options for non-URL content roots.
    /// </summary>
    /// <param name="optionFlags">Content-root related option flags.</param>
    /// <param name="error">Validation error message when invalid combinations are detected.</param>
    /// <returns>True when URL-only options were not supplied.</returns>
    private static bool TryValidateUrlOnlyContentRootOptions(ServiceContentRootOptionFlags optionFlags, out string error)
    {
        if (optionFlags.HasBearerToken)
        {
            error = "--content-root-bearer-token is only supported when --content-root points to an HTTP(S) archive URL.";
            return false;
        }

        if (optionFlags.IgnoreCertificate)
        {
            error = "--content-root-ignore-certificate is only supported when --content-root points to an HTTPS archive URL.";
            return false;
        }

        if (optionFlags.HasHeaders)
        {
            error = "--content-root-header is only supported when --content-root points to an HTTP(S) archive URL.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Validates option combinations for directory-based content roots.
    /// </summary>
    /// <param name="optionFlags">Content-root related option flags.</param>
    /// <param name="error">Validation error message when invalid combinations are detected.</param>
    /// <returns>True when the option combination is valid.</returns>
    private static bool TryValidateDirectoryContentRootOptions(ServiceContentRootOptionFlags optionFlags, out string error)
    {
        if (optionFlags.HasArchiveChecksum)
        {
            error = "--content-root-checksum is only supported when --content-root points to an archive file.";
            return false;
        }

        return TryValidateUrlOnlyContentRootOptions(optionFlags, out error);
    }

    /// <summary>
    /// Validates option combinations for local archive content roots.
    /// </summary>
    /// <param name="optionFlags">Content-root related option flags.</param>
    /// <param name="error">Validation error message when invalid combinations are detected.</param>
    /// <returns>True when the option combination is valid.</returns>
    private static bool TryValidateLocalArchiveContentRootOptions(ServiceContentRootOptionFlags optionFlags, out string error)
        => TryValidateUrlOnlyContentRootOptions(optionFlags, out error);

    /// <summary>
    /// Validates script path shape for HTTP archive content roots.
    /// </summary>
    /// <param name="requestedScriptPath">Requested script path.</param>
    /// <param name="error">Validation error text.</param>
    /// <returns>True when the script path is valid for URL archive usage.</returns>
    private static bool TryValidateHttpContentRootScriptPath(string requestedScriptPath, out string error)
    {
        if (!string.IsNullOrWhiteSpace(requestedScriptPath) && Path.IsPathRooted(requestedScriptPath))
        {
            error = "When --content-root is a URL archive, --script must be a relative path inside the archive.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Reads and validates Service.psd1 from a resolved service content-root folder.
    /// </summary>
    /// <param name="fullContentRoot">Absolute content-root directory path.</param>
    /// <param name="descriptor">Resolved and validated descriptor metadata.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when descriptor exists and mandatory metadata is valid.</returns>
    private static bool TryResolveServiceInstallDescriptor(string fullContentRoot, out ServiceInstallDescriptor descriptor, out string error)
    {
        descriptor = new ServiceInstallDescriptor(string.Empty, string.Empty, string.Empty, string.Empty, null, null, []);
        if (!TryReadNormalizedServiceDescriptorText(fullContentRoot, out var descriptorText, out error))
        {
            return false;
        }

        if (!TryResolveServiceDescriptorCoreFields(descriptorText, out var name, out var description, out var formatVersion, out error))
        {
            return false;
        }

        if (!TryResolveServiceDescriptorEntryPointAndVersion(
                descriptorText,
                formatVersion,
                out var normalizedFormatVersion,
                out var entryPoint,
                out var version,
                out error))
        {
            return false;
        }

        _ = TryGetServiceDescriptorStringValue(descriptorText, "ServiceLogPath", required: false, out var serviceLogPath, out _);

        if (!TryGetServiceDescriptorStringArrayValue(descriptorText, "PreservePaths", out var preservePaths, out error))
        {
            return false;
        }

        descriptor = new ServiceInstallDescriptor(
            normalizedFormatVersion,
            name,
            entryPoint,
            description,
            version,
            string.IsNullOrWhiteSpace(serviceLogPath) ? null : serviceLogPath,
            preservePaths);
        return true;
    }

    /// <summary>
    /// Reads service descriptor text from disk and normalizes escaped newlines for regex parsing.
    /// </summary>
    /// <param name="fullContentRoot">Absolute content-root directory path.</param>
    /// <param name="descriptorText">Normalized service descriptor text.</param>
    /// <param name="error">Error details when file resolution or read fails.</param>
    /// <returns>True when descriptor text is available and normalized.</returns>
    private static bool TryReadNormalizedServiceDescriptorText(string fullContentRoot, out string descriptorText, out string error)
    {
        descriptorText = string.Empty;
        var descriptorPath = Path.Combine(fullContentRoot, ServiceDescriptorFileName);
        if (!File.Exists(descriptorPath))
        {
            error = $"Service descriptor file '{ServiceDescriptorFileName}' was not found at content-root '{fullContentRoot}'.";
            return false;
        }

        try
        {
            descriptorText = File.ReadAllText(descriptorPath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            error = $"Failed to read service descriptor '{descriptorPath}': {ex.Message}";
            return false;
        }

        descriptorText = NormalizeServiceDescriptorText(descriptorText);
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Resolves required descriptor core fields and optional format version.
    /// </summary>
    /// <param name="descriptorText">Normalized service descriptor text.</param>
    /// <param name="name">Resolved service name.</param>
    /// <param name="description">Resolved service description.</param>
    /// <param name="formatVersion">Optional format version token.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when required core fields are valid.</returns>
    private static bool TryResolveServiceDescriptorCoreFields(
        string descriptorText,
        out string name,
        out string description,
        out string formatVersion,
        out string error)
    {
        if (!TryGetServiceDescriptorStringValue(descriptorText, "Name", required: true, out name, out error))
        {
            description = string.Empty;
            formatVersion = string.Empty;
            return false;
        }

        if (!TryGetServiceDescriptorStringValue(descriptorText, "Description", required: true, out description, out error))
        {
            formatVersion = string.Empty;
            return false;
        }

        _ = TryGetServiceDescriptorStringValue(descriptorText, "FormatVersion", required: false, out formatVersion, out _);
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Resolves descriptor entrypoint/version fields for legacy and format-1.0 descriptors.
    /// </summary>
    /// <param name="descriptorText">Normalized service descriptor text.</param>
    /// <param name="formatVersion">Optional format version token.</param>
    /// <param name="normalizedFormatVersion">Normalized format marker used by runtime metadata.</param>
    /// <param name="entryPoint">Resolved script entrypoint path.</param>
    /// <param name="version">Optional parsed version string.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when entrypoint/version resolution succeeds.</returns>
    private static bool TryResolveServiceDescriptorEntryPointAndVersion(
        string descriptorText,
        string formatVersion,
        out string normalizedFormatVersion,
        out string entryPoint,
        out string? version,
        out string error)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(formatVersion))
        {
            normalizedFormatVersion = "legacy";
            if (!TryGetServiceDescriptorStringValue(descriptorText, "Script", required: false, out entryPoint, out error))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(entryPoint))
            {
                entryPoint = ServiceDefaultScriptFileName;
            }

            return TryResolveOptionalServiceDescriptorVersion(descriptorText, out version, out error);
        }

        var trimmedFormatVersion = formatVersion.Trim();
        normalizedFormatVersion = trimmedFormatVersion;
        if (!string.Equals(trimmedFormatVersion, "1.0", StringComparison.Ordinal))
        {
            entryPoint = string.Empty;
            error = "Service descriptor FormatVersion must be '1.0'.";
            return false;
        }

        return TryGetServiceDescriptorStringValue(descriptorText, "EntryPoint", required: true, out entryPoint, out error)
            && TryResolveOptionalServiceDescriptorVersion(descriptorText, out version, out error);
    }

    /// <summary>
    /// Resolves and validates optional descriptor version metadata.
    /// </summary>
    /// <param name="descriptorText">Normalized service descriptor text.</param>
    /// <param name="version">Resolved version string when present.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when version is absent or parseable by <see cref="Version"/>.</returns>
    private static bool TryResolveOptionalServiceDescriptorVersion(string descriptorText, out string? version, out string error)
    {
        version = null;
        _ = TryGetServiceDescriptorStringValue(descriptorText, "Version", required: false, out var rawVersion, out _);
        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            error = string.Empty;
            return true;
        }

        var trimmedVersion = rawVersion.Trim();
        if (!Version.TryParse(trimmedVersion, out _))
        {
            error = $"Service descriptor '{ServiceDescriptorFileName}' key 'Version' must be compatible with System.Version.";
            return false;
        }

        version = trimmedVersion;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Normalizes service descriptor text for regex parsing.
    /// </summary>
    /// <param name="descriptorText">Raw descriptor text.</param>
    /// <returns>Descriptor text with PowerShell escaped newline sequences expanded.</returns>
    private static string NormalizeServiceDescriptorText(string descriptorText)
        => descriptorText
            .Replace("`r`n", "\n", StringComparison.Ordinal)
            .Replace("`n", "\n", StringComparison.Ordinal)
            .Replace("`r", "\n", StringComparison.Ordinal);

    /// <summary>
    /// Reads a string-valued key from Service.psd1.
    /// </summary>
    /// <param name="descriptorText">Raw descriptor content.</param>
    /// <param name="key">Descriptor key name.</param>
    /// <param name="required">True when a missing key should fail validation.</param>
    /// <param name="value">Resolved string value.</param>
    /// <param name="error">Validation error details when required values are missing.</param>
    /// <returns>True when key resolution succeeded for the required/optional mode.</returns>
    private static bool TryGetServiceDescriptorStringValue(string descriptorText, string key, bool required, out string value, out string error)
    {
        var match = Regex.Match(
            descriptorText,
            $@"(?mi)(?:^|[;{{\r\n])\s*{Regex.Escape(key)}\s*=\s*(?:'(?<single>[^']*)'|""(?<double>[^""]*)"")",
            RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            value = string.Empty;
            error = required
                ? $"Service descriptor '{ServiceDescriptorFileName}' is missing required key '{key}'."
                : string.Empty;
            return !required;
        }

        value = (match.Groups["single"].Success ? match.Groups["single"].Value : match.Groups["double"].Value).Trim();
        if (required && string.IsNullOrWhiteSpace(value))
        {
            error = $"Service descriptor '{ServiceDescriptorFileName}' key '{key}' must not be empty.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Reads a string-array key from Service.psd1 using PowerShell array syntax: Key = @( 'a', 'b' ).
    /// </summary>
    /// <param name="descriptorText">Raw descriptor content.</param>
    /// <param name="key">Descriptor key name.</param>
    /// <param name="values">Resolved array values.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when array resolution succeeds.</returns>
    private static bool TryGetServiceDescriptorStringArrayValue(string descriptorText, string key, out string[] values, out string error)
    {
        values = [];
        error = string.Empty;

        var arrayMatch = Regex.Match(
            descriptorText,
            $@"(?mis)(?:^|[;{{\r\n])\s*{Regex.Escape(key)}\s*=\s*@\((?<items>.*?)\)",
            RegexOptions.CultureInvariant);

        if (!arrayMatch.Success)
        {
            return true;
        }

        var itemsText = arrayMatch.Groups["items"].Value;
        var itemMatches = Regex.Matches(
            itemsText,
            "'(?<single>(?:''|[^'])*)'|\"(?<double>(?:\"\"|[^\"])*)\"",
            RegexOptions.CultureInvariant);

        if (itemMatches.Count == 0 && !string.IsNullOrWhiteSpace(itemsText))
        {
            error = $"Service descriptor '{ServiceDescriptorFileName}' key '{key}' must be a string array, for example: @('path1','path2').";
            return false;
        }

        values = [.. itemMatches
            .Select(static match =>
            {
                var raw = match.Groups["single"].Success
                    ? match.Groups["single"].Value.Replace("''", "'", StringComparison.Ordinal)
                    : match.Groups["double"].Value.Replace("\"\"", "\"", StringComparison.Ordinal);
                return raw.Trim();
            })
            .Where(static path => !string.IsNullOrWhiteSpace(path))];
        return true;
    }

    /// <summary>
    /// Resolves the script path for descriptor-driven service installs.
    /// </summary>
    /// <param name="requestedScriptPath">Script path requested on the command line.</param>
    /// <param name="descriptor">Resolved service descriptor.</param>
    /// <param name="resolvedScriptPath">Final script path relative to content root.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when the resolved script path is valid.</returns>
    private static bool TryResolveServiceDescriptorScriptPath(string requestedScriptPath, ServiceInstallDescriptor descriptor, out string resolvedScriptPath, out string error)
    {
        if (!string.IsNullOrWhiteSpace(requestedScriptPath))
        {
            resolvedScriptPath = string.Empty;
            error = "An explicit script path is not supported when --package is used. Define EntryPoint in Service.psd1 (format 1.0).";
            return false;
        }

        resolvedScriptPath = descriptor.EntryPoint;

        if (Path.IsPathRooted(resolvedScriptPath))
        {
            error = $"Service descriptor '{ServiceDescriptorFileName}' EntryPoint/Script must be a relative path within the package root.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Applies descriptor metadata to a resolved service script source.
    /// </summary>
    /// <param name="scriptSource">Resolved script source.</param>
    /// <param name="descriptor">Descriptor metadata.</param>
    /// <returns>Script source enriched with descriptor metadata.</returns>
    private static ResolvedServiceScriptSource ApplyDescriptorMetadata(ResolvedServiceScriptSource scriptSource, ServiceInstallDescriptor descriptor)
        => new(
            scriptSource.FullScriptPath,
            scriptSource.FullContentRoot,
            scriptSource.RelativeScriptPath,
            scriptSource.TemporaryContentRootPath,
            descriptor.Name,
            descriptor.Description,
            descriptor.Version,
            descriptor.ServiceLogPath,
            descriptor.PreservePaths);

    /// <summary>
    /// Downloads and extracts an HTTP content-root archive into the supplied directory.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <param name="contentRootUri">HTTP(S) archive URI.</param>
    /// <param name="temporaryRoot">Temporary root folder used for download output.</param>
    /// <param name="downloadedContentRoot">Folder where the archive should be extracted.</param>
    /// <param name="error">Error details when any stage fails.</param>
    /// <returns>True when download, checksum, and extraction all succeed.</returns>
    private static bool TryDownloadAndExtractHttpContentRoot(
        ParsedCommand command,
        Uri contentRootUri,
        string temporaryRoot,
        string downloadedContentRoot,
        out string error)
    {
        if (!TryDownloadServiceContentRootArchive(
                contentRootUri,
                temporaryRoot,
                command.ServiceContentRootBearerToken,
                command.ServiceContentRootHeaders,
                command.ServiceContentRootIgnoreCertificate,
                out var downloadedArchivePath,
                out error))
        {
            return false;
        }

        try
        {
            return
                TryValidateServiceContentRootArchiveChecksum(command, downloadedArchivePath, out error) &&
                TryExtractServiceContentRootArchive(downloadedArchivePath, downloadedContentRoot, out error);
        }
        finally
        {
            // Best-effort cleanup to avoid retaining large downloaded archives after extraction attempts.
            TryDeleteFileQuietly(downloadedArchivePath);
        }
    }

    /// <summary>
    /// Resolves a relative script path from an already materialized content-root directory.
    /// </summary>
    /// <param name="requestedScriptPath">Requested relative script path.</param>
    /// <param name="fullContentRoot">Absolute content-root path.</param>
    /// <param name="escapedPathError">Error message used when the script path escapes the content root.</param>
    /// <param name="missingScriptError">Error message used when the script file does not exist.</param>
    /// <param name="temporaryContentRootPath">Optional temporary content-root path for cleanup ownership.</param>
    /// <param name="scriptSource">Resolved script source details.</param>
    /// <param name="error">Error details when validation fails.</param>
    /// <returns>True when the script path resolves and exists inside the root.</returns>
    private static bool TryResolveScriptFromResolvedContentRoot(
        string requestedScriptPath,
        string fullContentRoot,
        string escapedPathError,
        string missingScriptError,
        string? temporaryContentRootPath,
        out ResolvedServiceScriptSource scriptSource,
        out string error)
    {
        scriptSource = CreateEmptyResolvedServiceScriptSource();
        var fullScriptPathFromRoot = Path.GetFullPath(Path.Combine(fullContentRoot, requestedScriptPath));
        if (!IsPathWithinDirectory(fullScriptPathFromRoot, fullContentRoot))
        {
            error = escapedPathError;
            return false;
        }

        if (!File.Exists(fullScriptPathFromRoot))
        {
            error = missingScriptError;
            return false;
        }

        var relativeScriptPath = Path.GetRelativePath(fullContentRoot, fullScriptPathFromRoot);
        scriptSource = new ResolvedServiceScriptSource(fullScriptPathFromRoot, fullContentRoot, relativeScriptPath, temporaryContentRootPath, null, null, null, null, []);
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Creates an empty service-script-source placeholder.
    /// </summary>
    /// <returns>Empty resolved service script source value.</returns>
    private static ResolvedServiceScriptSource CreateEmptyResolvedServiceScriptSource()
        => new(string.Empty, null, string.Empty, null, null, null, null, null, []);

    /// <summary>
    /// Creates a temporary extraction directory for archive-based service content roots.
    /// </summary>
    /// <param name="serviceName">Optional service name for easier diagnostics.</param>
    /// <returns>Newly created extraction directory path.</returns>
    private static string CreateServiceContentRootExtractionDirectory(string? serviceName)
    {
        var safeServiceName = string.IsNullOrWhiteSpace(serviceName)
            ? "service"
            : string.Concat(serviceName.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));

        if (string.IsNullOrWhiteSpace(safeServiceName))
        {
            safeServiceName = "service";
        }

        var extractionRoot = Path.Combine(Path.GetTempPath(), "kestrun-content-root", safeServiceName, Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(extractionRoot);
        return extractionRoot;
    }

    /// <summary>
    /// Parses an HTTP(S) URI for service content-root input.
    /// </summary>
    /// <param name="contentRootInput">Raw content-root input token.</param>
    /// <param name="uri">Parsed HTTP(S) URI.</param>
    /// <returns>True when input is an absolute HTTP(S) URI.</returns>
    private static bool TryParseServiceContentRootHttpUri(string contentRootInput, out Uri uri)
    {
        if (Uri.TryCreate(contentRootInput, UriKind.Absolute, out var parsed)
            && parsed is not null
            && (parsed.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }

    /// <summary>
    /// Downloads a service content-root archive from HTTP(S) into a temporary directory.
    /// </summary>
    /// <param name="uri">HTTP(S) source URI.</param>
    /// <param name="temporaryRoot">Temporary root directory for download output.</param>
    /// <param name="archivePath">Downloaded archive path.</param>
    /// <param name="error">Error details when download fails.</param>
    /// <returns>True when archive is downloaded and has a supported extension.</returns>
    private static bool TryDownloadServiceContentRootArchive(
        Uri uri,
        string temporaryRoot,
        string? bearerToken,
        string[] customHeaders,
        bool ignoreCertificate,
        out string archivePath,
        out string error)
    {
        archivePath = string.Empty;
        error = string.Empty;

        if (ignoreCertificate && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "--content-root-ignore-certificate is only valid for HTTPS URLs.";
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            if (!TryApplyServiceContentRootCustomHeaders(request, customHeaders, out error))
            {
                return false;
            }

            if (!ignoreCertificate)
            {
                using var response = ServiceContentRootHttpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    error = $"Failed to download service content root from '{uri}'. HTTP {(int)response.StatusCode} {response.ReasonPhrase}.";
                    return false;
                }

                return TryWriteDownloadedContentRootArchive(temporaryRoot, uri, response, out archivePath, out error);
            }

            using var insecureHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            };
            using var insecureClient = new HttpClient(insecureHandler)
            {
                Timeout = TimeSpan.FromMinutes(5),
            };
            insecureClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ProductName, "1.0"));
            using var insecureResponse = insecureClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
            if (!insecureResponse.IsSuccessStatusCode)
            {
                error = $"Failed to download service content root from '{uri}'. HTTP {(int)insecureResponse.StatusCode} {insecureResponse.ReasonPhrase}.";
                return false;
            }

            return TryWriteDownloadedContentRootArchive(temporaryRoot, uri, insecureResponse, out archivePath, out error);
        }
        catch (Exception ex)
        {
            error = $"Failed to download service content root from '{uri}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Applies custom request headers used for service content-root URL downloads.
    /// </summary>
    /// <param name="request">HTTP request message to update.</param>
    /// <param name="customHeaders">Custom header tokens in <c>name:value</c> format.</param>
    /// <param name="error">Validation error when a header token cannot be applied.</param>
    /// <returns>True when all headers are valid and applied to the request.</returns>
    private static bool TryApplyServiceContentRootCustomHeaders(HttpRequestMessage request, IReadOnlyList<string> customHeaders, out string error)
    {
        error = string.Empty;
        foreach (var headerToken in customHeaders)
        {
            if (string.IsNullOrWhiteSpace(headerToken))
            {
                error = "--content-root-header value cannot be empty. Use <name:value>.";
                return false;
            }

            var separatorIndex = headerToken.IndexOf(':');
            if (separatorIndex <= 0)
            {
                error = $"Invalid --content-root-header value '{headerToken}'. Use <name:value>.";
                return false;
            }

            var headerName = headerToken[..separatorIndex].Trim();
            var headerValue = headerToken[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(headerName))
            {
                error = $"Invalid --content-root-header value '{headerToken}'. Header name cannot be empty.";
                return false;
            }

            if (headerName.Contains('\r') || headerName.Contains('\n'))
            {
                error = $"Invalid --content-root-header value '{headerToken}'. Header name cannot contain CR or LF characters.";
                return false;
            }

            if (headerValue.Contains('\r') || headerValue.Contains('\n'))
            {
                error = $"Invalid --content-root-header value '{headerToken}'. Header value cannot contain CR or LF characters.";
                return false;
            }

            if (!request.Headers.TryAddWithoutValidation(headerName, headerValue))
            {
                error = $"Invalid --content-root-header value '{headerToken}'.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Writes a downloaded service content-root archive response to disk.
    /// </summary>
    /// <param name="temporaryRoot">Temporary root directory for archive output.</param>
    /// <param name="uri">Source URI.</param>
    /// <param name="response">HTTP response with archive payload.</param>
    /// <param name="archivePath">Written archive path.</param>
    /// <param name="error">Error details when write or validation fails.</param>
    /// <returns>True when the archive is written and has a supported extension.</returns>
    private static bool TryWriteDownloadedContentRootArchive(
        string temporaryRoot,
        Uri uri,
        HttpResponseMessage response,
        out string archivePath,
        out string error)
    {
        archivePath = string.Empty;
        error = string.Empty;

        var resolvedFileName = TryResolveServiceContentRootArchiveFileName(uri, response)
            ?? "content-root";
        resolvedFileName = GetSafeServiceContentRootArchiveFileName(resolvedFileName, "content-root");

        var provisionalArchivePath = Path.Combine(temporaryRoot, resolvedFileName);
        using (var sourceStream = response.Content.ReadAsStream())
        using (var destinationStream = File.Create(provisionalArchivePath))
        {
            sourceStream.CopyTo(destinationStream);
        }

        if (!TryResolveDownloadedServiceContentRootArchiveFileName(
                uri,
                resolvedFileName,
                provisionalArchivePath,
                response,
                out var finalizedFileName,
                out error))
        {
            try
            {
                if (File.Exists(provisionalArchivePath))
                {
                    File.Delete(provisionalArchivePath);
                }
            }
            catch
            {
                // Ignore cleanup errors because the original archive-validation error is more actionable.
            }
            return false;
        }

        archivePath = provisionalArchivePath;
        if (!string.Equals(finalizedFileName, resolvedFileName, StringComparison.OrdinalIgnoreCase))
        {
            var finalizedArchivePath = Path.Combine(temporaryRoot, finalizedFileName);
            File.Move(provisionalArchivePath, finalizedArchivePath, overwrite: true);
            archivePath = finalizedArchivePath;
        }

        return true;
    }

    /// <summary>
    /// Converts an archive file name candidate to a filesystem-safe file name.
    /// </summary>
    /// <param name="candidate">Raw file name candidate from headers or URI metadata.</param>
    /// <param name="fallbackFileName">Fallback file name when the candidate is empty or invalid.</param>
    /// <returns>Filesystem-safe file name.</returns>
    private static string GetSafeServiceContentRootArchiveFileName(string? candidate, string fallbackFileName)
    {
        var fileName = Path.GetFileName(candidate ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return fallbackFileName;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(fileName.Length);
        foreach (var ch in fileName)
        {
            if (char.IsControl(ch) || ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar || invalidChars.Contains(ch))
            {
                _ = builder.Append('-');
                continue;
            }

            _ = builder.Append(ch);
        }

        var sanitized = builder.ToString().Trim().Trim('.');
        return string.IsNullOrWhiteSpace(sanitized) ? fallbackFileName : sanitized;
    }

    /// <summary>
    /// Resolves a supported archive file name for a downloaded content-root payload.
    /// </summary>
    /// <param name="uri">Source URI.</param>
    /// <param name="resolvedFileName">Initially resolved file name candidate.</param>
    /// <param name="downloadedArchivePath">Downloaded archive payload path.</param>
    /// <param name="response">HTTP response metadata.</param>
    /// <param name="finalizedFileName">Finalized archive file name with supported extension.</param>
    /// <param name="error">Validation error details when archive type cannot be resolved.</param>
    /// <returns>True when a supported archive file name is resolved.</returns>
    private static bool TryResolveDownloadedServiceContentRootArchiveFileName(
        Uri uri,
        string resolvedFileName,
        string downloadedArchivePath,
        HttpResponseMessage response,
        out string finalizedFileName,
        out string error)
    {
        finalizedFileName = resolvedFileName;
        error = string.Empty;

        if (IsSupportedServiceContentRootArchive(finalizedFileName))
        {
            return true;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (TryGetServiceContentRootArchiveExtensionFromMediaType(mediaType, out var archiveExtension)
            || TryDetectServiceContentRootArchiveExtensionFromSignature(downloadedArchivePath, out archiveExtension))
        {
            finalizedFileName = BuildServiceContentRootArchiveFileName(resolvedFileName, archiveExtension);
            return true;
        }

        error = $"Downloaded package from '{uri}' is not a supported archive. Supported extensions: {ServicePackageExtension}, .zip, .tar, .tgz, .tar.gz.";
        return false;
    }

    /// <summary>
    /// Maps content-type metadata to a preferred service content-root archive extension.
    /// </summary>
    /// <param name="mediaType">HTTP response media type.</param>
    /// <param name="archiveExtension">Resolved archive extension when available.</param>
    /// <returns>True when the media type maps to a supported archive extension.</returns>
    private static bool TryGetServiceContentRootArchiveExtensionFromMediaType(string? mediaType, out string archiveExtension)
    {
        switch (mediaType?.ToLowerInvariant())
        {
            case "application/zip":
            case "application/x-zip-compressed":
                archiveExtension = ".zip";
                return true;
            case "application/x-tar":
                archiveExtension = ".tar";
                return true;
            case "application/gzip":
            case "application/x-gzip":
                archiveExtension = ".tgz";
                return true;
            default:
                archiveExtension = string.Empty;
                return false;
        }
    }

    /// <summary>
    /// Detects archive extension from file signature when metadata does not provide a usable file name.
    /// </summary>
    /// <param name="archivePath">Downloaded archive payload path.</param>
    /// <param name="archiveExtension">Detected archive extension.</param>
    /// <returns>True when a supported archive signature is recognized.</returns>
    private static bool TryDetectServiceContentRootArchiveExtensionFromSignature(string archivePath, out string archiveExtension)
    {
        archiveExtension = string.Empty;
        try
        {
            Span<byte> signature = stackalloc byte[512];
            using var stream = File.OpenRead(archivePath);
            var bytesRead = stream.Read(signature);
            if (bytesRead <= 0)
            {
                return false;
            }

            if (bytesRead >= 4
                && signature[0] == 0x50
                && signature[1] == 0x4B
                && ((signature[2] == 0x03 && signature[3] == 0x04)
                    || (signature[2] == 0x05 && signature[3] == 0x06)
                    || (signature[2] == 0x07 && signature[3] == 0x08)))
            {
                archiveExtension = ".zip";
                return true;
            }

            if (bytesRead >= 2 && signature[0] == 0x1F && signature[1] == 0x8B)
            {
                archiveExtension = ".tgz";
                return true;
            }

            if (bytesRead >= 262
                && signature[257] == (byte)'u'
                && signature[258] == (byte)'s'
                && signature[259] == (byte)'t'
                && signature[260] == (byte)'a'
                && signature[261] == (byte)'r')
            {
                archiveExtension = ".tar";
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Builds a normalized archive file name using the detected archive extension.
    /// </summary>
    /// <param name="resolvedFileName">Initially resolved file name candidate.</param>
    /// <param name="archiveExtension">Detected archive extension.</param>
    /// <returns>Normalized file name with archive extension.</returns>
    private static string BuildServiceContentRootArchiveFileName(string resolvedFileName, string archiveExtension)
    {
        var baseName = Path.GetFileNameWithoutExtension(resolvedFileName);
        if (archiveExtension.Equals(".tar.gz", StringComparison.OrdinalIgnoreCase)
            && resolvedFileName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
        {
            baseName = Path.GetFileNameWithoutExtension(baseName);
        }

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "content-root";
        }

        return $"{baseName}{archiveExtension}";
    }

    /// <summary>
    /// Resolves a best-effort archive file name from response headers and URI metadata.
    /// </summary>
    /// <param name="uri">Source URI.</param>
    /// <param name="response">HTTP response.</param>
    /// <returns>Resolved file name when available; otherwise null.</returns>
    private static string? TryResolveServiceContentRootArchiveFileName(Uri uri, HttpResponseMessage response)
    {
        var contentDisposition = response.Content.Headers.ContentDisposition;
        var dispositionFileName = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;
        if (!string.IsNullOrWhiteSpace(dispositionFileName))
        {
            var trimmed = dispositionFileName.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                return trimmed;
            }
        }

        var uriFileName = Path.GetFileName(uri.AbsolutePath);
        if (!string.IsNullOrWhiteSpace(uriFileName))
        {
            return uriFileName;
        }

        // Fall back to media-type-based extension inference when no usable file name metadata is available,
        // to at least get a correct extension for archive type detection and validation even if the base name is generic.
        return TryGetServiceContentRootArchiveExtensionFromMediaType(
            response.Content.Headers.ContentType?.MediaType,
            out var archiveExtension)
            ? BuildServiceContentRootArchiveFileName("content-root", archiveExtension)
            : null;
    }

    /// <summary>
    /// Returns true when the package archive path uses the supported extension.
    /// </summary>
    /// <param name="archivePath">Archive file path.</param>
    /// <returns>True when archive extension is supported.</returns>
    private static bool IsSupportedServiceContentRootArchive(string archivePath)
    {
        var lowerPath = archivePath.ToLowerInvariant();
        return lowerPath.EndsWith(ServicePackageExtension, StringComparison.Ordinal)
            || lowerPath.EndsWith(".zip", StringComparison.Ordinal)
            || lowerPath.EndsWith(".tar", StringComparison.Ordinal)
            || lowerPath.EndsWith(".tar.gz", StringComparison.Ordinal)
            || lowerPath.EndsWith(".tgz", StringComparison.Ordinal);
    }

    /// <summary>
    /// Validates a content-root archive checksum when a checksum was provided.
    /// </summary>
    /// <param name="command">Parsed service command.</param>
    /// <param name="archivePath">Archive path to validate.</param>
    /// <param name="error">Error details when checksum validation fails.</param>
    /// <returns>True when checksum is valid or not requested.</returns>
    private static bool TryValidateServiceContentRootArchiveChecksum(ParsedCommand command, string archivePath, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(command.ServiceContentRootChecksum))
        {
            return true;
        }

        var expectedHash = command.ServiceContentRootChecksum.Trim();
        if (!Regex.IsMatch(expectedHash, "^[0-9a-fA-F]+$"))
        {
            error = "--content-root-checksum must be a hexadecimal hash string.";
            return false;
        }

        var algorithmName = string.IsNullOrWhiteSpace(command.ServiceContentRootChecksumAlgorithm)
            ? "sha256"
            : command.ServiceContentRootChecksumAlgorithm.Trim();

        if (!TryCreateChecksumAlgorithm(algorithmName, out var algorithm, out var normalizedAlgorithmName, out error))
        {
            return false;
        }

        using (algorithm)
        using (var stream = File.OpenRead(archivePath))
        {
            var actualHash = Convert.ToHexString(algorithm.ComputeHash(stream));
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Archive checksum mismatch for '{archivePath}'. Expected {normalizedAlgorithmName}:{expectedHash}, got {normalizedAlgorithmName}:{actualHash}.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Creates a hash algorithm instance from a user-provided token.
    /// </summary>
    /// <param name="algorithmToken">Algorithm token from CLI.</param>
    /// <param name="algorithm">Created hash algorithm instance.</param>
    /// <param name="normalizedName">Normalized algorithm name.</param>
    /// <param name="error">Validation error text when algorithm creation fails.</param>
    /// <returns>True when the algorithm token is supported and can be created.</returns>
    private static bool TryCreateChecksumAlgorithm(string algorithmToken, out HashAlgorithm algorithm, out string normalizedName, out string error)
    {
        algorithm = null!;
        normalizedName = string.Empty;
        error = string.Empty;

        var compact = algorithmToken.Replace("-", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();

        Func<HashAlgorithm>? algorithmFactory = compact switch
        {
            "md5" => MD5.Create,
            "sha1" or "sha" => SHA1.Create,
            "sha2" or "sha256" => SHA256.Create,
            "sha384" => SHA384.Create,
            "sha512" => SHA512.Create,
            _ => null,
        };

        if (algorithmFactory is null)
        {
            error = "Unsupported --content-root-checksum-algorithm. Supported values: md5, sha1, sha256, sha384, sha512.";
            return false;
        }

        normalizedName = compact switch
        {
            "sha" => "sha1",
            "sha2" => "sha256",
            _ => compact,
        };

        try
        {
            algorithm = algorithmFactory();
            return true;
        }
        catch (Exception ex)
        {
            error = $"Unable to create checksum algorithm '{normalizedName}'. The algorithm may be disabled by system policy: {ex.Message}";
            algorithm = null!;
            normalizedName = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// Extracts a supported archive into the specified target directory.
    /// </summary>
    /// <param name="archivePath">Archive file path.</param>
    /// <param name="destinationDirectory">Extraction destination directory.</param>
    /// <param name="error">Error details when extraction fails.</param>
    /// <returns>True when extraction succeeds.</returns>
    private static bool TryExtractServiceContentRootArchive(string archivePath, string destinationDirectory, out string error)
    {
        error = string.Empty;

        try
        {
            var lowerPath = archivePath.ToLowerInvariant();
            if (lowerPath.EndsWith(ServicePackageExtension, StringComparison.Ordinal)
                || lowerPath.EndsWith(".zip", StringComparison.Ordinal))
            {
                return TryExtractZipArchiveSafely(archivePath, destinationDirectory, out error);
            }

            if (lowerPath.EndsWith(".tar", StringComparison.Ordinal))
            {
                return TryExtractTarArchiveSafely(File.OpenRead(archivePath), destinationDirectory, out error);
            }

            if (lowerPath.EndsWith(".tar.gz", StringComparison.Ordinal) || lowerPath.EndsWith(".tgz", StringComparison.Ordinal))
            {
                using var archiveStream = File.OpenRead(archivePath);
                using var gzipStream = new GZipStream(archiveStream, CompressionMode.Decompress);
                return TryExtractTarArchiveSafely(gzipStream, destinationDirectory, out error);
            }

            error = $"Unsupported package format. Supported extension: {ServicePackageExtension} (zip payload).";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Failed to extract service content archive '{archivePath}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Extracts a zip archive while enforcing destination path boundaries.
    /// </summary>
    /// <param name="archivePath">Archive file path.</param>
    /// <param name="destinationDirectory">Extraction destination directory.</param>
    /// <param name="error">Error details when extraction fails.</param>
    /// <returns>True when extraction succeeds.</returns>
    private static bool TryExtractZipArchiveSafely(string archivePath, string destinationDirectory, out string error)
    {
        error = string.Empty;
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            var fullDestinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
            if (!IsPathWithinDirectory(fullDestinationPath, destinationDirectory))
            {
                error = $"Archive entry '{entry.FullName}' escapes extraction root.";
                return false;
            }

            var isDirectory = string.IsNullOrEmpty(entry.Name)
                || entry.FullName.EndsWith('/')
                || entry.FullName.EndsWith('\\');
            if (isDirectory)
            {
                _ = Directory.CreateDirectory(fullDestinationPath);
                continue;
            }

            var parentDirectory = Path.GetDirectoryName(fullDestinationPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                _ = Directory.CreateDirectory(parentDirectory);
            }

            entry.ExtractToFile(fullDestinationPath, overwrite: true);
        }

        return true;
    }

    /// <summary>
    /// Extracts a tar stream while enforcing destination path boundaries.
    /// </summary>
    /// <param name="archiveStream">Tar-formatted stream.</param>
    /// <param name="destinationDirectory">Extraction destination directory.</param>
    /// <param name="error">Error details when extraction fails.</param>
    /// <returns>True when extraction succeeds.</returns>
    private static bool TryExtractTarArchiveSafely(Stream archiveStream, string destinationDirectory, out string error)
    {
        error = string.Empty;
        using var reader = new TarReader(archiveStream, leaveOpen: false);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            var fullDestinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.Name));
            if (!IsPathWithinDirectory(fullDestinationPath, destinationDirectory))
            {
                error = $"Archive entry '{entry.Name}' escapes extraction root.";
                return false;
            }

            if (entry.EntryType is TarEntryType.Directory)
            {
                _ = Directory.CreateDirectory(fullDestinationPath);
                continue;
            }

            if (entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile)
            {
                var parentDirectory = Path.GetDirectoryName(fullDestinationPath);
                if (!string.IsNullOrWhiteSpace(parentDirectory))
                {
                    _ = Directory.CreateDirectory(parentDirectory);
                }

                if (entry.DataStream is null)
                {
                    using var emptyFile = File.Create(fullDestinationPath);
                    continue;
                }

                using var output = File.Create(fullDestinationPath);
                entry.DataStream.CopyTo(output);
                continue;
            }
        }

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
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return fullCandidate.Equals(fullDirectory, comparison)
            || fullCandidate.StartsWith(fullDirectory + Path.DirectorySeparatorChar, comparison)
            || fullCandidate.StartsWith(fullDirectory + Path.AltDirectorySeparatorChar, comparison);
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
            var overrideRoot = Path.GetFullPath(deploymentRootOverride);
            if (!TryEnsureDirectoryWritable(overrideRoot, out var overrideError))
            {
                error = $"Unable to use deployment root '{deploymentRootOverride}': {overrideError}";
                return false;
            }

            deploymentRoot = overrideRoot;
            return true;
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
                if (!TryEnsureDirectoryWritable(fullCandidate, out var candidateError))
                {
                    failures.Add($"{candidate} ({candidateError})");
                    continue;
                }

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
    /// Ensures a directory is writable by creating and deleting a short-lived probe file.
    /// </summary>
    /// <param name="directoryPath">Directory path to validate.</param>
    /// <param name="error">Error details when the path is not writable.</param>
    /// <returns>True when the directory can be created and written to.</returns>
    private static bool TryEnsureDirectoryWritable(string directoryPath, out string error)
    {
        error = string.Empty;

        try
        {
            _ = Directory.CreateDirectory(directoryPath);
            var probePath = Path.Combine(directoryPath, $".kestrun-write-probe-{Guid.NewGuid():N}");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Returns candidate deployment roots for service bundle storage.
    /// </summary>
    /// <returns>Candidate absolute or rooted paths in priority order.</returns>
    private static IEnumerable<string> GetServiceDeploymentRootCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                ServiceDeploymentProductFolderName,
                ServiceDeploymentServicesFolderName);
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
        var printedPermissionHint = false;

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
                if (OperatingSystem.IsWindows())
                {
                    TryDeleteDirectoryWithRetry(serviceRoot, maxAttempts: 15, initialDelayMs: 250);
                }
                else
                {
                    TryDeleteDirectoryWithRetry(serviceRoot);
                }
            }
            catch (Exception ex)
            {
                if (IsExpectedUnixProtectedRootCleanupFailure(candidateRoot, ex, deploymentRootOverride))
                {
                    if (!printedPermissionHint)
                    {
                        Console.Error.WriteLine("Info: Skipping cleanup of root-owned service bundle locations. Use sudo to remove legacy bundles under system roots.");
                        printedPermissionHint = true;
                    }

                    continue;
                }

                Console.Error.WriteLine($"Warning: Failed to remove service bundle '{serviceRoot}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Returns true when cleanup failures are expected for protected Unix roots owned by another user.
    /// </summary>
    /// <param name="candidateRoot">Deployment root candidate being cleaned.</param>
    /// <param name="exception">Raised exception.</param>
    /// <param name="deploymentRootOverride">Optional explicit deployment root override.</param>
    /// <returns>True when the error can be downgraded to informational output.</returns>
    private static bool IsExpectedUnixProtectedRootCleanupFailure(string candidateRoot, Exception exception, string? deploymentRootOverride)
    {
        if (!string.IsNullOrWhiteSpace(deploymentRootOverride))
        {
            return false;
        }

        if (exception is not UnauthorizedAccessException)
        {
            return false;
        }

        if (!(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()))
        {
            return false;
        }

        if (IsLikelyRunningAsRootOnUnix())
        {
            return false;
        }

        // Cleanup failures for protected system roots are expected when running as a non-root user on Unix, so downgrade to informational output in that scenario to avoid confusion.
        return IsProtectedUnixServiceRoot(candidateRoot);
    }

    /// <summary>
    /// Returns true when the path is a protected system root used for service bundle fallback on Unix.
    /// </summary>
    /// <param name="candidateRoot">Deployment root candidate.</param>
    /// <returns>True when path is a protected system root.</returns>
    private static bool IsProtectedUnixServiceRoot(string candidateRoot)
    {
        if (string.IsNullOrWhiteSpace(candidateRoot))
        {
            return false;
        }

        var fullCandidate = Path.GetFullPath(candidateRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(fullCandidate, "/var/kestrun/services", StringComparison.Ordinal)
            || string.Equals(fullCandidate, "/usr/local/kestrun/services", StringComparison.Ordinal);
    }

    /// <summary>
    /// Deletes a directory recursively with retry/backoff for transient file-lock scenarios.
    /// </summary>
    /// <param name="directoryPath">Directory path to delete.</param>
    /// <param name="maxAttempts">Maximum number of attempts.</param>
    /// <param name="initialDelayMs">Initial delay between attempts in milliseconds.</param>
    private static void TryDeleteDirectoryWithRetry(string directoryPath, int maxAttempts = 5, int initialDelayMs = 200)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        var attempt = 0;
        var delayMs = initialDelayMs;
        Exception? lastError = null;

        while (attempt < maxAttempts)
        {
            try
            {
                Directory.Delete(directoryPath, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastError = ex;
                attempt += 1;
                if (attempt >= maxAttempts)
                {
                    break;
                }

                Thread.Sleep(delayMs);
                delayMs = Math.Min(delayMs * 2, 2000);
            }
        }

        if (lastError is not null)
        {
            throw lastError;
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
    /// Returns true when the current Linux process is likely running as root.
    /// </summary>
    /// <returns>True when username resolves to root on Linux.</returns>
    private static bool IsLikelyRunningAsRootOnLinux() => OperatingSystem.IsLinux() && string.Equals(Environment.UserName, "root", StringComparison.Ordinal);

    /// <summary>
    /// Returns true when the current Unix process is likely running as root.
    /// </summary>
    /// <returns>True when username resolves to root on Linux or macOS.</returns>
    private static bool IsLikelyRunningAsRootOnUnix() => (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        && string.Equals(Environment.UserName, "root", StringComparison.Ordinal);

    /// <summary>
    /// Writes actionable guidance for common user-level systemd failures on Linux.
    /// </summary>
    /// <param name="result">Captured process result from a failed systemctl --user call.</param>
    private static void WriteLinuxUserSystemdFailureHint(ProcessResult result)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var diagnostics = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
        if (string.IsNullOrWhiteSpace(diagnostics))
        {
            return;
        }

        if (!diagnostics.Contains("Failed to connect to bus", StringComparison.OrdinalIgnoreCase)
            && !diagnostics.Contains("No medium found", StringComparison.OrdinalIgnoreCase)
            && !diagnostics.Contains("Access denied", StringComparison.OrdinalIgnoreCase)
            && !diagnostics.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Console.Error.WriteLine("Hint: Linux service commands use user-level systemd units (systemctl --user).");
        Console.Error.WriteLine("Run install/start/stop/query/remove as the same non-root user that installed the unit.");
        Console.Error.WriteLine("If running over SSH or a headless session, enable linger: sudo loginctl enable-linger <user>.");
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
    /// Resolves a module manifest path for run mode, preferring bundled service payload when no explicit path is provided.
    /// </summary>
    /// <param name="kestrunManifestPath">Optional explicit manifest path.</param>
    /// <param name="kestrunFolder">Optional module folder path.</param>
    /// <returns>Absolute path to the resolved module manifest, or null when not found.</returns>
    private static string? ResolveRunModuleManifestPath(string? kestrunManifestPath, string? kestrunFolder)
    {
        if (!string.IsNullOrWhiteSpace(kestrunManifestPath) || !string.IsNullOrWhiteSpace(kestrunFolder))
        {
            return LocateModuleManifest(kestrunManifestPath, kestrunFolder);
        }

        if (TryResolvePowerShellModulesPayloadFromToolDistribution(out var modulesPayloadPath))
        {
            var bundledManifestPath = Path.Combine(modulesPayloadPath, ModuleName, ModuleManifestFileName);
            if (File.Exists(bundledManifestPath))
            {
                return Path.GetFullPath(bundledManifestPath);
            }
        }

        return LocateModuleManifest(null, null);
    }

    /// <summary>
    /// Builds arguments for direct foreground run mode on the dedicated service-host executable.
    /// </summary>
    /// <param name="runnerExecutablePath">Runner executable path.</param>
    /// <param name="scriptPath">Absolute script path.</param>
    /// <param name="moduleManifestPath">Absolute module manifest path.</param>
    /// <param name="scriptArguments">Script arguments.</param>
    /// <param name="discoverPowerShellHome">When true, pass --discover-pshome.</param>
    /// <returns>Ordered argument list.</returns>
    private static IReadOnlyList<string> BuildDedicatedServiceHostRunArguments(
        string runnerExecutablePath,
        string scriptPath,
        string moduleManifestPath,
        IReadOnlyList<string> scriptArguments,
        bool discoverPowerShellHome)
    {
        var arguments = new List<string>(12 + scriptArguments.Count)
        {
            "--runner-exe",
            Path.GetFullPath(runnerExecutablePath),
            "--run",
            Path.GetFullPath(scriptPath),
            "--kestrun-manifest",
            Path.GetFullPath(moduleManifestPath),
        };

        if (discoverPowerShellHome)
        {
            arguments.Add("--discover-pshome");
        }

        if (scriptArguments.Count > 0)
        {
            arguments.Add("--arguments");
            arguments.AddRange(scriptArguments);
        }

        return arguments;
    }

    /// <summary>
    /// Resolves whether service-host should auto-discover PSHOME for the selected manifest path.
    /// </summary>
    /// <param name="moduleManifestPath">Absolute path to Kestrun.psd1.</param>
    /// <returns>True when --discover-pshome should be used.</returns>
    private static bool ShouldDiscoverPowerShellHomeForManifest(string moduleManifestPath)
    {
        var fullManifestPath = Path.GetFullPath(moduleManifestPath);
        var moduleDirectory = Path.GetDirectoryName(fullManifestPath);
        if (string.IsNullOrWhiteSpace(moduleDirectory))
        {
            return true;
        }

        var moduleRoot = Directory.GetParent(moduleDirectory);
        var serviceRoot = moduleRoot?.Parent?.FullName;
        if (string.IsNullOrWhiteSpace(serviceRoot))
        {
            return true;
        }

        var modulesDirectory = Path.Combine(serviceRoot, "Modules");
        return !Directory.Exists(modulesDirectory);
    }

    /// <summary>
    /// Resolves the current executable path when available, otherwise falls back to the provided value.
    /// </summary>
    /// <param name="fallbackPath">Fallback path when current process path is unavailable.</param>
    /// <returns>Absolute executable path.</returns>
    private static string ResolveCurrentProcessPathOrFallback(string fallbackPath)
        => !string.IsNullOrWhiteSpace(Environment.ProcessPath) && File.Exists(Environment.ProcessPath)
            ? Path.GetFullPath(Environment.ProcessPath)
            : Path.GetFullPath(fallbackPath);

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
        var modulePath = GetPowerShellModulePath(scope);
        var moduleRoot = Path.Combine(modulePath, ModuleName);
        return TryGetLatestInstalledModuleVersionTextFromModuleRoot(moduleRoot, out versionText);
    }

    /// <summary>
    /// Attempts to read the latest installed module version text from a specific module root path.
    /// </summary>
    /// <param name="moduleRoot">Root directory containing versioned module folders.</param>
    /// <param name="versionText">Installed semantic version text when available.</param>
    /// <returns>True when an installed version was found in the module root.</returns>
    private static bool TryGetLatestInstalledModuleVersionTextFromModuleRoot(string moduleRoot, out string versionText)
    {
        versionText = string.Empty;
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
            var prereleaseMatch = ModulePrereleasePatternRegex.Match(content);
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
        => TryGetLatestGalleryVersionStringFromClient(GalleryHttpClient, out version, out errorText);

    /// <summary>
    /// Attempts to query the latest Kestrun module version string from PowerShell Gallery using the specified HTTP client.
    /// </summary>
    /// <param name="httpClient">HTTP client used for the gallery request.</param>
    /// <param name="version">Latest gallery version string when available.</param>
    /// <param name="errorText">Error details when discovery fails.</param>
    /// <returns>True when latest gallery version was discovered.</returns>
    private static bool TryGetLatestGalleryVersionStringFromClient(HttpClient httpClient, out string version, out string errorText)
    {
        version = string.Empty;
        if (!TryGetGalleryModuleVersionsFromClient(httpClient, out var versions, out errorText))
        {
            return false;
        }

        var latestVersion = versions[0];
        for (var index = 1; index < versions.Count; index++)
        {
            if (CompareModuleVersionValues(versions[index], latestVersion) > 0)
            {
                latestVersion = versions[index];
            }
        }

        version = latestVersion;
        return !string.IsNullOrWhiteSpace(version);
    }

    /// <summary>
    /// Queries all available Kestrun module versions from PowerShell Gallery.
    /// </summary>
    /// <param name="versions">Discovered gallery versions.</param>
    /// <param name="errorText">Error details when discovery fails.</param>
    /// <returns>True when at least one version was discovered.</returns>
    private static bool TryGetGalleryModuleVersions(out List<string> versions, out string errorText)
        => TryGetGalleryModuleVersionsFromClient(GalleryHttpClient, out versions, out errorText);

    /// <summary>
    /// Queries all available Kestrun module versions from PowerShell Gallery using the specified HTTP client.
    /// </summary>
    /// <param name="httpClient">HTTP client used for the gallery request.</param>
    /// <param name="versions">Discovered gallery versions.</param>
    /// <param name="errorText">Error details when discovery fails.</param>
    /// <returns>True when at least one version was discovered.</returns>
    private static bool TryGetGalleryModuleVersionsFromClient(HttpClient httpClient, out List<string> versions, out string errorText)
    {
        versions = [];
        errorText = string.Empty;

        try
        {
            var requestUri = $"{PowerShellGalleryApiBaseUri}/FindPackagesById()?id='{Uri.EscapeDataString(ModuleName)}'";
            using var response = httpClient.GetAsync(requestUri).GetAwaiter().GetResult();
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

            return TryParseGalleryModuleVersions(content, out versions, out errorText);
        }
        catch (Exception ex)
        {
            errorText = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Parses gallery feed XML and extracts module versions.
    /// </summary>
    /// <param name="content">Gallery feed XML payload.</param>
    /// <param name="versions">Discovered gallery versions.</param>
    /// <param name="errorText">Error details when parsing fails.</param>
    /// <returns>True when at least one version was discovered.</returns>
    private static bool TryParseGalleryModuleVersions(string content, out List<string> versions, out string errorText)
    {
        versions = [];
        errorText = string.Empty;

        try
        {
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
    /// Creates the shared HTTP client used for service content-root archive downloads.
    /// </summary>
    /// <returns>Configured HTTP client instance.</returns>
    private static HttpClient CreateServiceContentRootHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5),
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
            var match = ModuleVersionPatternRegex.Match(content);
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
        parsedCommand = new ParsedCommand(CommandMode.Run, string.Empty, false, [], null, null, null, false, null, null, null, null, ModuleStorageScope.Local, false, null, null, null, null, null, null, null, null, null, null, false, [], false, false, false, false);
        if (args.Length == 0)
        {
            error = $"No command provided. Use '{ProductName} help' to list commands.";
            return false;
        }

        if (!TryParseLeadingKestrunOptions(args, out var commandTokenIndex, out var kestrunFolder, out var kestrunManifestPath, out error))
        {
            return false;
        }

        if (commandTokenIndex >= args.Length)
        {
            error = $"No command provided. Use '{ProductName} help' to list commands.";
            return false;
        }

        return TryParseCommandFromToken(args, commandTokenIndex, kestrunFolder, kestrunManifestPath, out parsedCommand, out error);
    }

    /// <summary>
    /// Parses leading global Kestrun options that may appear before the command token.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="commandTokenIndex">Index of the command token after global options.</param>
    /// <param name="kestrunFolder">Optional module folder path supplied via global option.</param>
    /// <param name="kestrunManifestPath">Optional module manifest path supplied via global option.</param>
    /// <param name="error">Error message when a required option value is missing.</param>
    /// <returns>True when leading options are parsed successfully.</returns>
    private static bool TryParseLeadingKestrunOptions(
        string[] args,
        out int commandTokenIndex,
        out string? kestrunFolder,
        out string? kestrunManifestPath,
        out string error)
    {
        commandTokenIndex = 0;
        kestrunFolder = null;
        kestrunManifestPath = null;
        error = string.Empty;

        while (commandTokenIndex < args.Length)
        {
            var current = args[commandTokenIndex];
            if (current is "--kestrun-folder" or "-k")
            {
                if (!TryConsumeLeadingOptionValue(args, ref commandTokenIndex, "--kestrun-folder", out var folderValue, out error))
                {
                    return false;
                }

                kestrunFolder = folderValue;
                continue;
            }

            if (current is "--kestrun-manifest" or "-m")
            {
                if (!TryConsumeLeadingOptionValue(args, ref commandTokenIndex, "--kestrun-manifest", out var manifestValue, out error))
                {
                    return false;
                }

                kestrunManifestPath = manifestValue;
                continue;
            }

            break;
        }

        return true;
    }

    /// <summary>
    /// Consumes a global option value and advances the parse index.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="index">Current option index, advanced when consumption succeeds.</param>
    /// <param name="optionName">Canonical option name used in diagnostics.</param>
    /// <param name="value">Consumed option value.</param>
    /// <param name="error">Error text when the value is missing.</param>
    /// <returns>True when the option value is consumed.</returns>
    private static bool TryConsumeLeadingOptionValue(string[] args, ref int index, string optionName, out string value, out string error)
    {
        value = string.Empty;
        if (index + 1 >= args.Length)
        {
            error = $"Missing value for {optionName}.";
            return false;
        }

        value = args[index + 1];
        index += 2;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Dispatches command parsing based on the selected command token.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="commandTokenIndex">Index of the command token.</param>
    /// <param name="kestrunFolder">Optional module folder path.</param>
    /// <param name="kestrunManifestPath">Optional module manifest path.</param>
    /// <param name="parsedCommand">Parsed command payload.</param>
    /// <param name="error">Error message when dispatch fails.</param>
    /// <returns>True when command parsing succeeds.</returns>
    private static bool TryParseCommandFromToken(
        string[] args,
        int commandTokenIndex,
        string? kestrunFolder,
        string? kestrunManifestPath,
        out ParsedCommand parsedCommand,
        out string error)
    {
        var commandToken = args[commandTokenIndex];
        if (string.Equals(commandToken, "run", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseRunArguments(args, commandTokenIndex + 1, kestrunFolder, kestrunManifestPath, out parsedCommand, out error);
        }

        if (string.Equals(commandToken, "service", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseServiceArguments(args, commandTokenIndex + 1, kestrunFolder, kestrunManifestPath, out parsedCommand, out error);
        }

        if (string.Equals(commandToken, "module", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseModuleArguments(args, commandTokenIndex + 1, out parsedCommand, out error);
        }

        parsedCommand = new ParsedCommand(CommandMode.Run, string.Empty, false, [], null, null, null, false, null, null, null, null, ModuleStorageScope.Local, false, null, null, null, null, null, null, null, null, null, null, false, [], false, false, false, false);
        error = $"Unknown command: {commandToken}. Use '{ProductName} help' to list commands.";
        return false;
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
        Console.WriteLine("  run       Run a PowerShell script (default script: ./Service.ps1)");
        Console.WriteLine("  module    Manage Kestrun module (install/update/remove/info)");
        Console.WriteLine("  service   Manage service lifecycle (install/update/remove/start/stop/query/info)");
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
                Console.WriteLine("  - If no script is provided, ./Service.ps1 is used.");
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
                Console.WriteLine("  kestrun [--nocheck] [--kestrun-manifest <path-to-Kestrun.psd1>] service install [--package <path-or-url-to-.krpack>] [--service-log-path <path-to-log-file>] [--service-user <account>] [--service-password <password>] [--deployment-root <folder>] [--runtime-source <path-or-url>] [--runtime-package <path-to-.nupkg-or-folder>] [--runtime-version <version>] [--runtime-package-id <package-id>] [--runtime-cache <folder>] [--content-root-checksum <hex>] [--content-root-checksum-algorithm <name>] [--content-root-bearer-token <token>] [--content-root-header <name:value> ...] [--content-root-ignore-certificate] [--arguments <script arguments...>]");
                Console.WriteLine("  kestrun [--nocheck] service update --name <service-name> [--package <path-or-url-to-.krpack>] [--kestrun | --kestrun-module <path-to-Kestrun.psd1-or-folder> | --kestrun-manifest <path-to-Kestrun.psd1-or-folder>] [--deployment-root <folder>] [--content-root-checksum <hex>] [--content-root-checksum-algorithm <name>] [--content-root-bearer-token <token>] [--content-root-header <name:value> ...] [--content-root-ignore-certificate] [--failback]");
                Console.WriteLine("  kestrun service remove --name <service-name>");
                Console.WriteLine("  kestrun service start --name <service-name> [--json | --raw]");
                Console.WriteLine("  kestrun service stop --name <service-name> [--json | --raw]");
                Console.WriteLine("  kestrun service query --name <service-name> [--json | --raw]");
                Console.WriteLine("  kestrun service info [--name <service-name>] [--json]");
                Console.WriteLine();
                Console.WriteLine("Options (service install):");
                Console.WriteLine("  --package <path-or-url>     Required .krpack (zip) package containing Service.psd1 and app files.");
                Console.WriteLine("  --content-root-checksum <h> Verify package checksum before extraction (hex string).");
                Console.WriteLine("  --content-root-checksum-algorithm <name>  Hash algorithm: md5, sha1, sha256, sha384, sha512 (default: sha256).");
                Console.WriteLine("  --content-root-bearer-token <token>  Add Authorization: Bearer <token> for HTTP(S) package download.");
                Console.WriteLine("  --content-root-header <name:value>  Add custom HTTP request header for HTTP(S) package download. Repeatable.");
                Console.WriteLine("  --content-root-ignore-certificate  Ignore HTTPS certificate validation for package download (insecure).");
                Console.WriteLine("  --deployment-root <folder>  Override where per-service bundles are created (default is OS-specific).");
                Console.WriteLine("  --runtime-source <path-or-url>  Override runtime acquisition using a local folder, local .nupkg, direct .nupkg URL, NuGet service index, or flat-container base URL.");
                Console.WriteLine("  --runtime-package <path>    Use an explicit local Kestrun.Service.<rid> .nupkg for offline installs.");
                Console.WriteLine("  --runtime-version <version> Override the runtime package version (defaults to the current tool version). When used without --package, only the runtime cache is populated.");
                Console.WriteLine("  --runtime-package-id <id>   Override the runtime package id (defaults to Kestrun.Service.<rid>).");
                Console.WriteLine("  --runtime-cache <folder>    Override the local runtime package cache directory.");
                Console.WriteLine("  --kestrun-manifest <path>   Use an explicit Kestrun.psd1 manifest for the service runtime.");
                Console.WriteLine("  --service-log-path <path>   Set service bootstrap/operation log file path.");
                Console.WriteLine("  --service-user <account>    Run installed service/daemon under a specific OS account.");
                Console.WriteLine("  --service-password <secret> Password for --service-user on Windows service accounts.");
                Console.WriteLine("  --arguments <args...>       Pass remaining values to the installed script.");
                Console.WriteLine("  --kestrun                   For service update: use repository module at src/PowerShell/Kestrun when newer than bundled module.");
                Console.WriteLine("  --kestrun-module <path>     For service update: module manifest path or folder to refresh bundled Kestrun module.");
                Console.WriteLine("  --failback                  For service update: restore application/module from latest backup and delete that backup folder.");
                Console.WriteLine("  --json                      For service start/stop/query/info: output JSON instead of table/human-readable text.");
                Console.WriteLine("  --raw                       For service start/stop/query: output native OS command output.");
                Console.WriteLine();
                Console.WriteLine("Notes:");
                Console.WriteLine("  - install registers the service/daemon but does not auto-start it.");
                Console.WriteLine("  - update fails when the service is running; stop it first.");
                Console.WriteLine("  - update requires at least one of --package or --kestrun-module/--kestrun-manifest unless --failback is used.");
                Console.WriteLine("  - --kestrun updates bundled module only when repository module version is newer; otherwise update is skipped with an informational message.");
                Console.WriteLine("  - --failback restores from latest backup and fails when no backup is available.");
                Console.WriteLine("  - info without --name lists installed Kestrun services.");
                Console.WriteLine("  - Service name and entry point are read from Service.psd1 in the package.");
                Console.WriteLine("  - Service.psd1 requires FormatVersion='1.0', Name, EntryPoint, and Description.");
                Console.WriteLine("  - Package file must use .krpack extension and contain zip content.");
                Console.WriteLine("  - install resolves a runtime package for the current RID using Kestrun.Service.<rid> packages.");
                Console.WriteLine("  - install caches canonical runtime packages under packages/<id>/<version>/<id>.<version>.nupkg and extracted working payloads under expanded/<id>/... .");
                Console.WriteLine("  - install can be used without --package to prefetch a runtime package into cache only; supply at least one runtime acquisition option.");
                Console.WriteLine("  - install does not fall back to the runtime bundled with Kestrun.Tool when package acquisition fails.");
                Console.WriteLine("  - use --runtime-package for offline installs or --runtime-source to point at a local feed/NuGet endpoint.");
                Console.WriteLine("  - --content-root-checksum is validated against the package file before extraction.");
                Console.WriteLine("  - --content-root-bearer-token is used for HTTP(S) package URLs and HTTP(S) runtime-source downloads.");
                Console.WriteLine("  - --content-root-header is used for HTTP(S) package URLs and HTTP(S) runtime-source downloads; it can be supplied multiple times.");
                Console.WriteLine("  - --content-root-ignore-certificate applies only to HTTPS package URLs/runtime-source URLs and is insecure.");
                Console.WriteLine("  - --deployment-root overrides the OS default bundle root used during install and remove cleanup.");
                Console.WriteLine("  - --service-user enables platform account mapping: Windows service account, Linux systemd User=, macOS LaunchDaemon UserName.");
                Console.WriteLine("  - install snapshots runtime/module/script plus the dedicated service host from the resolved runtime package into a per-service bundle before registration.");
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
}
