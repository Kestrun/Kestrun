namespace Kestrun.Tool;

internal static partial class Program
{
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

        if (OperatingSystem.IsLinux() && !string.IsNullOrWhiteSpace(command.ServiceUser) && !IsLikelyRunningAsRootOnLinux())
        {
            Console.Error.WriteLine("Linux system service install with --service-user requires root privileges.");
            return 1;
        }

        if (OperatingSystem.IsMacOS() && !string.IsNullOrWhiteSpace(command.ServiceUser) && !IsLikelyRunningAsRootOnUnix())
        {
            Console.Error.WriteLine("macOS system daemon install with --service-user requires root privileges.");
            return 1;
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
            var result = InstallLinuxUserDaemon(serviceName, serviceBundle.ServiceHostExecutablePath, daemonArgs, workingDirectory, command.ServiceUser);
            WriteServiceOperationResult("install", "linux", serviceName, result, command.ServiceLogPath);
            return result;
        }

        if (OperatingSystem.IsMacOS())
        {
            var result = InstallMacLaunchAgent(serviceName, serviceBundle.ServiceHostExecutablePath, daemonArgs, workingDirectory, command.ServiceUser);
            WriteServiceOperationResult("install", "macos", serviceName, result, command.ServiceLogPath);
            return result;
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

            WriteServiceOperationResult("remove", "linux", serviceName, result, command.ServiceLogPath);

            return result;
        }

        if (OperatingSystem.IsMacOS())
        {
            result = RemoveMacLaunchAgent(serviceName);
            if (result == 0)
            {
                TryRemoveServiceBundle(serviceName, command.ServiceDeploymentRoot);
            }

            WriteServiceOperationResult("remove", "macos", serviceName, result, command.ServiceLogPath);

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
            var result = StartLinuxUserDaemon(serviceName);
            WriteServiceOperationResult("start", "linux", serviceName, result, command.ServiceLogPath);
            return result;
        }

        if (OperatingSystem.IsMacOS())
        {
            var result = StartMacLaunchAgent(serviceName);
            WriteServiceOperationResult("start", "macos", serviceName, result, command.ServiceLogPath);
            return result;
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
            var result = StopLinuxUserDaemon(serviceName);
            WriteServiceOperationResult("stop", "linux", serviceName, result, command.ServiceLogPath);
            return result;
        }

        if (OperatingSystem.IsMacOS())
        {
            var result = StopMacLaunchAgent(serviceName);
            WriteServiceOperationResult("stop", "macos", serviceName, result, command.ServiceLogPath);
            return result;
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
            var result = QueryLinuxUserDaemon(serviceName);
            WriteServiceOperationResult("query", "linux", serviceName, result, command.ServiceLogPath);
            return result;
        }

        if (OperatingSystem.IsMacOS())
        {
            var result = QueryMacLaunchAgent(serviceName);
            WriteServiceOperationResult("query", "macos", serviceName, result, command.ServiceLogPath);
            return result;
        }

        Console.Error.WriteLine("Service query is not supported on this OS.");
        return 1;
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
    /// Installs a systemd unit (user scope by default; system scope when serviceUser is provided).
    /// </summary>
    /// <param name="serviceName">Unit base name.</param>
    /// <param name="exePath">Executable path.</param>
    /// <param name="runnerArgs">Runner arguments.</param>
    /// <param name="workingDirectory">Working directory for the unit.</param>
    /// <param name="serviceUser">Optional service account for system scope.</param>
    /// <returns>Process exit code.</returns>
    private static int InstallLinuxUserDaemon(string serviceName, string exePath, IReadOnlyList<string> runnerArgs, string workingDirectory, string? serviceUser)
    {
        var useSystemScope = !string.IsNullOrWhiteSpace(serviceUser);

        if (useSystemScope && !IsLikelyRunningAsRootOnLinux())
        {
            Console.Error.WriteLine("Linux system service install with --service-user requires root privileges.");
            return 1;
        }

        if (!useSystemScope && IsLikelyRunningAsRootOnLinux())
        {
            Console.Error.WriteLine("Warning: Running as root installs a root user-level unit via systemctl --user.");
            Console.Error.WriteLine("That unit is managed from root's user session and is separate from your regular user units.");
        }

        var unitDirectory = useSystemScope
            ? "/etc/systemd/system"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "systemd", "user");
        _ = Directory.CreateDirectory(unitDirectory);

        var unitName = GetLinuxUnitName(serviceName);
        var unitPath = Path.Combine(unitDirectory, unitName);
        var unitContent = BuildLinuxSystemdUnitContent(serviceName, exePath, runnerArgs, workingDirectory, serviceUser);

        File.WriteAllText(unitPath, unitContent);

        var reloadResult = RunLinuxSystemctl(useSystemScope, ["daemon-reload"]);
        if (reloadResult.ExitCode != 0)
        {
            Console.Error.WriteLine(reloadResult.Error);
            if (!useSystemScope)
            {
                WriteLinuxUserSystemdFailureHint(reloadResult);
            }

            return reloadResult.ExitCode;
        }

        var enableResult = RunLinuxSystemctl(useSystemScope, ["enable", unitName]);
        if (enableResult.ExitCode != 0)
        {
            Console.Error.WriteLine(enableResult.Error);
            if (!useSystemScope)
            {
                WriteLinuxUserSystemdFailureHint(enableResult);
            }

            return enableResult.ExitCode;
        }

        Console.WriteLine(useSystemScope
            ? $"Installed Linux system daemon '{unitName}' for user '{serviceUser}' (not started)."
            : $"Installed Linux user daemon '{unitName}' (not started).");
        return 0;
    }

    /// <summary>
    /// Builds Linux systemd unit file content for a service install.
    /// </summary>
    /// <param name="serviceName">Service name used for Description.</param>
    /// <param name="exePath">Executable path for the runner.</param>
    /// <param name="runnerArgs">Arguments passed to the runner executable.</param>
    /// <param name="workingDirectory">Working directory for the systemd unit.</param>
    /// <param name="serviceUser">Optional Linux user account for system-scoped units.</param>
    /// <returns>Rendered unit file content.</returns>
    private static string BuildLinuxSystemdUnitContent(string serviceName, string exePath, IReadOnlyList<string> runnerArgs, string workingDirectory, string? serviceUser)
    {
        var useSystemScope = !string.IsNullOrWhiteSpace(serviceUser);
        var execStart = string.Join(" ", new[] { EscapeSystemdToken(exePath) }.Concat(runnerArgs.Select(EscapeSystemdToken)));

        return string.Join('\n',
            "[Unit]",
            $"Description={serviceName}",
            "After=network.target",
            "",
            "[Service]",
            "Type=simple",
            useSystemScope ? $"User={serviceUser}" : string.Empty,
            $"WorkingDirectory={workingDirectory}",
            $"ExecStart={execStart}",
            "Restart=always",
            "RestartSec=2",
            "",
            "[Install]",
            useSystemScope ? "WantedBy=multi-user.target" : "WantedBy=default.target",
            "");
    }

    /// <summary>
    /// Removes a user-level systemd unit.
    /// </summary>
    /// <param name="serviceName">Unit base name.</param>
    /// <returns>Process exit code.</returns>
    private static int RemoveLinuxUserDaemon(string serviceName)
    {
        var useSystemScope = IsLinuxSystemUnitInstalled(serviceName);
        var unitDirectory = useSystemScope
            ? "/etc/systemd/system"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "systemd", "user");
        var unitName = GetLinuxUnitName(serviceName);
        var unitPath = Path.Combine(unitDirectory, unitName);

        _ = RunLinuxSystemctl(useSystemScope, ["disable", "--now", unitName]);
        if (File.Exists(unitPath))
        {
            File.Delete(unitPath);
        }

        var reloadResult = RunLinuxSystemctl(useSystemScope, ["daemon-reload"]);
        if (reloadResult.ExitCode != 0)
        {
            Console.Error.WriteLine(reloadResult.Error);
            if (!useSystemScope)
            {
                WriteLinuxUserSystemdFailureHint(reloadResult);
            }

            return reloadResult.ExitCode;
        }

        Console.WriteLine(useSystemScope
            ? $"Removed Linux system daemon '{unitName}'."
            : $"Removed Linux user daemon '{unitName}'.");
        return 0;
    }

    /// <summary>
    /// Starts a Linux user-level systemd unit.
    /// </summary>
    /// <param name="serviceName">Unit base name.</param>
    /// <returns>Process exit code.</returns>
    private static int StartLinuxUserDaemon(string serviceName)
    {
        var useSystemScope = IsLinuxSystemUnitInstalled(serviceName);
        var unitName = GetLinuxUnitName(serviceName);
        var result = RunLinuxSystemctl(useSystemScope, ["start", unitName]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            if (!useSystemScope)
            {
                WriteLinuxUserSystemdFailureHint(result);
            }

            return result.ExitCode;
        }

        Console.WriteLine(useSystemScope
            ? $"Started Linux system daemon '{unitName}'."
            : $"Started Linux user daemon '{unitName}'.");
        return 0;
    }

    /// <summary>
    /// Stops a Linux user-level systemd unit.
    /// </summary>
    /// <param name="serviceName">Unit base name.</param>
    /// <returns>Process exit code.</returns>
    private static int StopLinuxUserDaemon(string serviceName)
    {
        var useSystemScope = IsLinuxSystemUnitInstalled(serviceName);
        var unitName = GetLinuxUnitName(serviceName);
        var result = RunLinuxSystemctl(useSystemScope, ["stop", unitName]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            if (!useSystemScope)
            {
                WriteLinuxUserSystemdFailureHint(result);
            }

            return result.ExitCode;
        }

        Console.WriteLine(useSystemScope
            ? $"Stopped Linux system daemon '{unitName}'."
            : $"Stopped Linux user daemon '{unitName}'.");
        return 0;
    }

    /// <summary>
    /// Queries a Linux user-level systemd unit.
    /// </summary>
    /// <param name="serviceName">Unit base name.</param>
    /// <returns>Process exit code.</returns>
    private static int QueryLinuxUserDaemon(string serviceName)
    {
        var useSystemScope = IsLinuxSystemUnitInstalled(serviceName);
        var unitName = GetLinuxUnitName(serviceName);
        var result = RunLinuxSystemctl(useSystemScope, ["status", unitName]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            if (!useSystemScope)
            {
                WriteLinuxUserSystemdFailureHint(result);
            }

            return result.ExitCode;
        }

        return 0;
    }

    /// <summary>
    /// Runs systemctl in user or system scope.
    /// </summary>
    /// <param name="useSystemScope">True for system scope; false for user scope.</param>
    /// <param name="arguments">Arguments after optional scope switch.</param>
    /// <returns>Process execution result.</returns>
    private static ProcessResult RunLinuxSystemctl(bool useSystemScope, IReadOnlyList<string> arguments)
    {
        return useSystemScope
            ? RunProcess("systemctl", arguments)
            : RunProcess("systemctl", ["--user", .. arguments]);
    }

    /// <summary>
    /// Returns true when a system-scoped unit file exists for the service.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <returns>True when a system unit exists under /etc/systemd/system.</returns>
    private static bool IsLinuxSystemUnitInstalled(string serviceName)
    {
        var unitName = GetLinuxUnitName(serviceName);
        var systemUnitPath = Path.Combine("/etc/systemd/system", unitName);
        return File.Exists(systemUnitPath);
    }

    /// <summary>
    /// Installs a macOS launch agent plist (or launch daemon when a service user is specified).
    /// </summary>
    /// <param name="serviceName">Agent label.</param>
    /// <param name="exePath">Executable path.</param>
    /// <param name="runnerArgs">Runner arguments.</param>
    /// <param name="workingDirectory">Working directory for launchd.</param>
    /// <param name="serviceUser">Optional service account for system daemon scope.</param>
    /// <returns>Process exit code.</returns>
    private static int InstallMacLaunchAgent(string serviceName, string exePath, IReadOnlyList<string> runnerArgs, string workingDirectory, string? serviceUser)
    {
        var useSystemScope = !string.IsNullOrWhiteSpace(serviceUser);
        if (useSystemScope && !IsLikelyRunningAsRootOnUnix())
        {
            Console.Error.WriteLine("macOS system daemon install with --service-user requires root privileges.");
            return 1;
        }

        var agentDirectory = useSystemScope
            ? "/Library/LaunchDaemons"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
        _ = Directory.CreateDirectory(agentDirectory);

        var plistName = $"{serviceName}.plist";
        var plistPath = Path.Combine(agentDirectory, plistName);
        var programArgs = new[] { exePath }.Concat(runnerArgs).ToArray();
        var plistContent = BuildLaunchdPlist(serviceName, workingDirectory, programArgs, serviceUser);
        File.WriteAllText(plistPath, plistContent);

        Console.WriteLine(useSystemScope
            ? $"Installed macOS launch daemon '{serviceName}' for user '{serviceUser}' (not started)."
            : $"Installed macOS launch agent '{serviceName}' (not started).");
        return 0;
    }

    /// <summary>
    /// Removes a macOS launch agent plist.
    /// </summary>
    /// <param name="serviceName">Agent label.</param>
    /// <returns>Process exit code.</returns>
    private static int RemoveMacLaunchAgent(string serviceName)
    {
        var useSystemScope = IsMacSystemLaunchDaemonInstalled(serviceName);
        var agentDirectory = useSystemScope
            ? "/Library/LaunchDaemons"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
        var plistPath = Path.Combine(agentDirectory, $"{serviceName}.plist");

        // Unload the agent before deleting the plist to ensure launchd doesn't keep a stale reference to the file.
        _ = useSystemScope
            ? RunProcess("launchctl", ["bootout", $"system/{serviceName}"])
            : RunProcess("launchctl", ["unload", plistPath]);

        // It's possible for the unload to fail if the agent isn't running, but we want to attempt it anyway to avoid leaving a stale loaded agent if the plist is present.
        if (File.Exists(plistPath))
        {
            File.Delete(plistPath);
        }

        Console.WriteLine(useSystemScope
            ? $"Removed macOS launch daemon '{serviceName}'."
            : $"Removed macOS launch agent '{serviceName}'.");
        return 0;
    }

    /// <summary>
    /// Starts a macOS launch agent by loading its plist.
    /// </summary>
    /// <param name="serviceName">Agent label.</param>
    /// <returns>Process exit code.</returns>
    private static int StartMacLaunchAgent(string serviceName)
    {
        var useSystemScope = IsMacSystemLaunchDaemonInstalled(serviceName);
        var agentDirectory = useSystemScope
            ? "/Library/LaunchDaemons"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
        var plistPath = Path.Combine(agentDirectory, $"{serviceName}.plist");
        if (!File.Exists(plistPath))
        {
            Console.Error.WriteLine($"Launch agent plist not found: {plistPath}");
            return 2;
        }

        var result = useSystemScope
            ? RunProcess("launchctl", ["bootstrap", "system", plistPath])
            : RunProcess("launchctl", ["load", "-w", plistPath]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            return result.ExitCode;
        }

        Console.WriteLine(useSystemScope
            ? $"Started macOS launch daemon '{serviceName}'."
            : $"Started macOS launch agent '{serviceName}'.");
        return 0;
    }

    /// <summary>
    /// Stops a macOS launch agent by unloading its plist.
    /// </summary>
    /// <param name="serviceName">Agent label.</param>
    /// <returns>Process exit code.</returns>
    private static int StopMacLaunchAgent(string serviceName)
    {
        var useSystemScope = IsMacSystemLaunchDaemonInstalled(serviceName);
        var agentDirectory = useSystemScope
            ? "/Library/LaunchDaemons"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
        var plistPath = Path.Combine(agentDirectory, $"{serviceName}.plist");
        if (!File.Exists(plistPath))
        {
            Console.Error.WriteLine($"Launch agent plist not found: {plistPath}");
            return 2;
        }

        var result = useSystemScope
            ? RunProcess("launchctl", ["bootout", $"system/{serviceName}"])
            : RunProcess("launchctl", ["unload", plistPath]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            return result.ExitCode;
        }

        Console.WriteLine(useSystemScope
            ? $"Stopped macOS launch daemon '{serviceName}'."
            : $"Stopped macOS launch agent '{serviceName}'.");
        return 0;
    }

    /// <summary>
    /// Queries a macOS launch agent by label.
    /// </summary>
    /// <param name="serviceName">Agent label.</param>
    /// <returns>Process exit code.</returns>
    private static int QueryMacLaunchAgent(string serviceName)
    {
        var useSystemScope = IsMacSystemLaunchDaemonInstalled(serviceName);
        var result = useSystemScope
            ? RunProcess("launchctl", ["print", $"system/{serviceName}"])
            : RunProcess("launchctl", ["list", serviceName]);
        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error);
            return result.ExitCode;
        }

        return 0;
    }

    /// <summary>
    /// Returns true when a system-scoped launch daemon plist exists for the service.
    /// </summary>
    /// <param name="serviceName">Service label.</param>
    /// <returns>True when plist exists under /Library/LaunchDaemons.</returns>
    private static bool IsMacSystemLaunchDaemonInstalled(string serviceName)
    {
        var plistPath = Path.Combine("/Library/LaunchDaemons", $"{serviceName}.plist");
        return File.Exists(plistPath);
    }

    /// <summary>
    /// Builds a launchd plist document for a persistent launch agent/daemon.
    /// </summary>
    /// <param name="label">Launchd label.</param>
    /// <param name="workingDirectory">Working directory.</param>
    /// <param name="programArguments">Program argument list.</param>
    /// <param name="serviceUser">Optional macOS account name for LaunchDaemon UserName.</param>
    /// <returns>XML plist content.</returns>
    private static string BuildLaunchdPlist(string label, string workingDirectory, IReadOnlyList<string> programArguments, string? serviceUser)
    {
        var argsXml = string.Join(string.Empty, programArguments.Select(arg => $"\n    <string>{EscapeXml(arg)}</string>"));
        var userXml = string.IsNullOrWhiteSpace(serviceUser)
            ? string.Empty
            : $"\n  <key>UserName</key>\n  <string>{EscapeXml(serviceUser)}</string>";
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
    <string>{EscapeXml(workingDirectory)}</string>{userXml}
  <key>RunAtLoad</key>
  <true/>
  <key>KeepAlive</key>
  <true/>
</dict>
</plist>
""";
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

        if (!TryResolveServiceBundleContext(
                serviceName,
                sourceScriptPath,
                sourceModuleManifestPath,
                deploymentRootOverride,
                out var context,
                out error))
        {
            return false;
        }

        var showProgress = !Console.IsOutputRedirected;
        using var bundleProgress = showProgress
            ? new ConsoleProgressBar("Preparing service bundle", 5, FormatServiceBundleStepProgressDetail)
            : null;
        var completedBundleSteps = 0;
        bundleProgress?.Report(0);

        try
        {
            RecreateServiceBundleDirectories(context);
            completedBundleSteps++;
            bundleProgress?.Report(completedBundleSteps);

            var bundledRuntimePath = CopyServiceRuntimeExecutable(context);
            completedBundleSteps++;
            bundleProgress?.Report(completedBundleSteps);

            if (!TryCopyServiceHostExecutable(context.RuntimeDirectory, out var bundledServiceHostPath, out error))
            {
                return false;
            }

            if (!TryCopyBundledToolModules(context.ModulesDirectory, showProgress, out error))
            {
                return false;
            }

            completedBundleSteps++;
            bundleProgress?.Report(completedBundleSteps);

            EnsureBundleExecutablesAreRunnable(bundledRuntimePath, bundledServiceHostPath);

            if (!TryCopyServiceModuleFiles(context, showProgress, out var bundledManifestPath, out error))
            {
                return false;
            }

            completedBundleSteps++;
            bundleProgress?.Report(completedBundleSteps);

            if (!TryCopyServiceScriptFiles(context, sourceContentRoot, relativeScriptPath, showProgress, out var bundledScriptPath, out error))
            {
                return false;
            }

            completedBundleSteps++;
            bundleProgress?.Report(completedBundleSteps);
            bundleProgress?.Complete(completedBundleSteps);

            serviceBundle = new ServiceBundleLayout(
                Path.GetFullPath(context.ServiceRoot),
                Path.GetFullPath(bundledRuntimePath),
                Path.GetFullPath(bundledServiceHostPath),
                Path.GetFullPath(bundledScriptPath),
                Path.GetFullPath(bundledManifestPath));
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to prepare service bundle at '{context.ServiceRoot}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Resolves and validates all source and destination paths required to build a service bundle.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="sourceScriptPath">Source script path.</param>
    /// <param name="sourceModuleManifestPath">Source module manifest path.</param>
    /// <param name="deploymentRootOverride">Optional deployment root override for tests.</param>
    /// <param name="context">Resolved bundle path context.</param>
    /// <param name="error">Error details when resolution fails.</param>
    /// <returns>True when context resolution succeeds.</returns>
    private static bool TryResolveServiceBundleContext(
        string serviceName,
        string sourceScriptPath,
        string sourceModuleManifestPath,
        string? deploymentRootOverride,
        out ServiceBundleContext context,
        out string error)
    {
        context = default;
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

        if (!TryResolveServiceDeploymentRoot(deploymentRootOverride, out var deploymentRoot, out var deploymentError))
        {
            error = deploymentError;
            return false;
        }

        var serviceDirectoryName = GetServiceDeploymentDirectoryName(serviceName);
        var serviceRoot = Path.Combine(deploymentRoot, serviceDirectoryName);
        var runtimeDirectory = Path.Combine(serviceRoot, ServiceBundleRuntimeDirectoryName);
        var modulesDirectory = Path.Combine(serviceRoot, ServiceBundleModulesDirectoryName);
        var moduleDirectory = Path.Combine(modulesDirectory, ModuleName);
        var scriptDirectory = Path.Combine(serviceRoot, ServiceBundleScriptDirectoryName);
        var moduleRoot = Path.GetDirectoryName(fullManifestPath)!;

        context = new ServiceBundleContext(
            fullScriptPath,
            fullManifestPath,
            runtimeExecutablePath,
            moduleRoot,
            serviceRoot,
            runtimeDirectory,
            modulesDirectory,
            moduleDirectory,
            scriptDirectory);
        return true;
    }

    /// <summary>
    /// Recreates the target service bundle directory structure from scratch.
    /// </summary>
    /// <param name="context">Resolved service bundle context.</param>
    private static void RecreateServiceBundleDirectories(ServiceBundleContext context)
    {
        if (Directory.Exists(context.ServiceRoot))
        {
            Directory.Delete(context.ServiceRoot, recursive: true);
        }

        _ = Directory.CreateDirectory(context.RuntimeDirectory);
        _ = Directory.CreateDirectory(context.ModulesDirectory);
        _ = Directory.CreateDirectory(context.ModuleDirectory);
        _ = Directory.CreateDirectory(context.ScriptDirectory);
    }

    /// <summary>
    /// Copies the resolved runtime executable into the service bundle runtime directory.
    /// </summary>
    /// <param name="context">Resolved service bundle context.</param>
    /// <returns>Bundled runtime executable path.</returns>
    private static string CopyServiceRuntimeExecutable(ServiceBundleContext context)
    {
        var bundledRuntimePath = Path.Combine(context.RuntimeDirectory, Path.GetFileName(context.RuntimeExecutablePath));
        File.Copy(context.RuntimeExecutablePath, bundledRuntimePath, overwrite: true);
        return bundledRuntimePath;
    }

    /// <summary>
    /// Copies the dedicated service host executable into the service bundle runtime directory.
    /// </summary>
    /// <param name="runtimeDirectory">Runtime directory path.</param>
    /// <param name="bundledServiceHostPath">Bundled service host executable path.</param>
    /// <param name="error">Error details when host resolution or copy fails.</param>
    /// <returns>True when the service host executable is copied successfully.</returns>
    private static bool TryCopyServiceHostExecutable(string runtimeDirectory, out string bundledServiceHostPath, out string error)
    {
        bundledServiceHostPath = string.Empty;
        error = string.Empty;

        if (!TryResolveDedicatedServiceHostExecutableFromToolDistribution(out var serviceHostExecutablePath))
        {
            error = $"Unable to locate dedicated service host for current RID in Kestrun.Tool distribution. Expected '{(OperatingSystem.IsWindows() ? "kestrun-service-host.exe" : "kestrun-service-host")}' under 'kestrun-service/<rid>/'. Reinstall or update Kestrun.Tool.";
            return false;
        }

        bundledServiceHostPath = Path.Combine(runtimeDirectory, Path.GetFileName(serviceHostExecutablePath));
        File.Copy(serviceHostExecutablePath, bundledServiceHostPath, overwrite: true);
        return true;
    }

    /// <summary>
    /// Copies bundled PowerShell modules payload from the tool distribution into the service bundle.
    /// </summary>
    /// <param name="modulesDirectory">Destination modules directory in the service bundle.</param>
    /// <param name="showProgress">True to print copy progress details.</param>
    /// <param name="error">Error details when payload resolution fails.</param>
    /// <returns>True when bundled modules copy succeeds.</returns>
    private static bool TryCopyBundledToolModules(string modulesDirectory, bool showProgress, out string error)
    {
        error = string.Empty;

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
        return true;
    }

    /// <summary>
    /// Ensures copied runtime executables are executable on Unix-like platforms.
    /// </summary>
    /// <param name="bundledRuntimePath">Bundled runtime executable path.</param>
    /// <param name="bundledServiceHostPath">Bundled service host executable path.</param>
    private static void EnsureBundleExecutablesAreRunnable(string bundledRuntimePath, string bundledServiceHostPath)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        TryEnsureServiceRuntimeExecutablePermissions(bundledRuntimePath);
        if (!string.Equals(bundledRuntimePath, bundledServiceHostPath, StringComparison.OrdinalIgnoreCase))
        {
            TryEnsureServiceRuntimeExecutablePermissions(bundledServiceHostPath);
        }
    }

    /// <summary>
    /// Copies module files into the service bundle and validates that the module manifest is present.
    /// </summary>
    /// <param name="context">Resolved service bundle context.</param>
    /// <param name="showProgress">True to print copy progress details.</param>
    /// <param name="bundledManifestPath">Resulting bundled manifest path.</param>
    /// <param name="error">Error details when the manifest is not present after copy.</param>
    /// <returns>True when module files are copied and manifest validation succeeds.</returns>
    private static bool TryCopyServiceModuleFiles(ServiceBundleContext context, bool showProgress, out string bundledManifestPath, out string error)
    {
        error = string.Empty;

        CopyDirectoryContents(
            context.ModuleRoot,
            context.ModuleDirectory,
            showProgress,
            "Bundling module files",
            ServiceBundleModuleExclusionPatterns);

        bundledManifestPath = Path.Combine(context.ModuleDirectory, Path.GetFileName(context.FullManifestPath));
        if (File.Exists(bundledManifestPath))
        {
            return true;
        }

        error = $"Service bundle copy did not include module manifest: {bundledManifestPath}";
        return false;
    }

    /// <summary>
    /// Copies service script files into the service bundle and validates that the entry script exists.
    /// </summary>
    /// <param name="context">Resolved service bundle context.</param>
    /// <param name="sourceContentRoot">Optional script content root for folder copy mode.</param>
    /// <param name="relativeScriptPath">Relative script path under the script folder.</param>
    /// <param name="showProgress">True to print copy progress details.</param>
    /// <param name="bundledScriptPath">Resulting bundled script entrypoint path.</param>
    /// <param name="error">Error details when the bundled script is not present.</param>
    /// <returns>True when script copy and validation succeed.</returns>
    private static bool TryCopyServiceScriptFiles(
        ServiceBundleContext context,
        string? sourceContentRoot,
        string relativeScriptPath,
        bool showProgress,
        out string bundledScriptPath,
        out string error)
    {
        error = string.Empty;
        bundledScriptPath = Path.Combine(context.ScriptDirectory, relativeScriptPath.Replace('/', Path.DirectorySeparatorChar));

        if (string.IsNullOrWhiteSpace(sourceContentRoot))
        {
            var bundledScriptDirectory = Path.GetDirectoryName(bundledScriptPath);
            if (!string.IsNullOrWhiteSpace(bundledScriptDirectory))
            {
                _ = Directory.CreateDirectory(bundledScriptDirectory);
            }

            File.Copy(context.FullScriptPath, bundledScriptPath, overwrite: true);
        }
        else
        {
            CopyDirectoryContents(
                sourceContentRoot,
                context.ScriptDirectory,
                showProgress,
                "Bundling service script folder",
                exclusionPatterns: null);
        }

        if (File.Exists(bundledScriptPath))
        {
            return true;
        }

        error = $"Service bundle copy did not include script: {bundledScriptPath}";
        return false;
    }

    /// <summary>
    /// Stores resolved paths used during service bundle creation.
    /// </summary>
    /// <param name="FullScriptPath">Resolved source script path.</param>
    /// <param name="FullManifestPath">Resolved source module manifest path.</param>
    /// <param name="RuntimeExecutablePath">Resolved runtime executable source path.</param>
    /// <param name="ModuleRoot">Resolved module directory root.</param>
    /// <param name="ServiceRoot">Resolved service bundle root path.</param>
    /// <param name="RuntimeDirectory">Resolved runtime directory inside bundle.</param>
    /// <param name="ModulesDirectory">Resolved modules directory inside bundle.</param>
    /// <param name="ModuleDirectory">Resolved module-specific directory inside bundle.</param>
    /// <param name="ScriptDirectory">Resolved script directory inside bundle.</param>
    private readonly record struct ServiceBundleContext(
        string FullScriptPath,
        string FullManifestPath,
        string RuntimeExecutablePath,
        string ModuleRoot,
        string ServiceRoot,
        string RuntimeDirectory,
        string ModulesDirectory,
        string ModuleDirectory,
        string ScriptDirectory);
}
