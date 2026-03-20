
using System.Globalization;

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
        if (!TryResolveInstallServiceInputs(
                command,
                out var serviceName,
                out var serviceVersion,
                out var effectiveServiceLogPath,
                out var scriptSource,
                out var moduleManifestPath,
                out var inputExitCode))
        {
            return inputExitCode;
        }

        try
        {
            if (!TryRunInstallServicePreflight(command, serviceName, moduleManifestPath, effectiveServiceLogPath, skipGalleryCheck, out var preflightExitCode))
            {
                return preflightExitCode;
            }

            if (!TryPrepareInstallServiceBundle(command, serviceName, serviceVersion, scriptSource, moduleManifestPath, out var serviceBundle, out var bundleExitCode))
            {
                return bundleExitCode;
            }
            // Service bundle preparation should not fail silently, but check for null just in case.
            return InstallPreparedServiceForCurrentPlatform(command, serviceName, effectiveServiceLogPath, serviceBundle);
        }
        finally
        {
            TryCleanupTemporaryServiceContentRoot(scriptSource.TemporaryContentRootPath);
        }
    }

    /// <summary>
    /// Resolves and validates install-service inputs that are independent of operating-system install mechanics.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <param name="serviceName">Resolved service name.</param>
    /// <param name="serviceVersion">Resolved service descriptor version when provided.</param>
    /// <param name="effectiveServiceLogPath">Effective service log path (CLI override or descriptor value).</param>
    /// <param name="scriptSource">Resolved service script source.</param>
    /// <param name="moduleManifestPath">Resolved module manifest path.</param>
    /// <param name="exitCode">Exit code when validation fails.</param>
    /// <returns>True when inputs are valid and resolved.</returns>
    private static bool TryResolveInstallServiceInputs(
        ParsedCommand command,
        out string serviceName,
        out string? serviceVersion,
        out string? effectiveServiceLogPath,
        out ResolvedServiceScriptSource scriptSource,
        out string moduleManifestPath,
        out int exitCode)
    {
        serviceName = string.Empty;
        serviceVersion = null;
        effectiveServiceLogPath = null;
        scriptSource = CreateEmptyResolvedServiceScriptSource();
        moduleManifestPath = string.Empty;
        exitCode = 0;

        if (!TryResolveServiceScriptSource(command, out scriptSource, out var scriptError))
        {
            Console.Error.WriteLine(scriptError);
            exitCode = 2;
            return false;
        }

        var resolvedServiceName = string.IsNullOrWhiteSpace(scriptSource.DescriptorServiceName)
            ? command.ServiceName
            : scriptSource.DescriptorServiceName;

        if (string.IsNullOrWhiteSpace(resolvedServiceName))
        {
            Console.Error.WriteLine("Service name is required in Service.psd1 (Name) when using --package.");
            exitCode = 2;
            return false;
        }

        serviceName = resolvedServiceName;
        serviceVersion = scriptSource.DescriptorServiceVersion;
        effectiveServiceLogPath = !string.IsNullOrWhiteSpace(command.ServiceLogPath)
            ? command.ServiceLogPath
            : scriptSource.DescriptorServiceLogPath;

        var cleanupScriptSourceOnFailure = true;
        try
        {
            var locatedModuleManifestPath = LocateModuleManifest(command.KestrunManifestPath, command.KestrunFolder);
            if (locatedModuleManifestPath is null)
            {
                WriteModuleNotFoundMessage(command.KestrunManifestPath, command.KestrunFolder, Console.Error.WriteLine);
                exitCode = 3;
                return false;
            }

            moduleManifestPath = locatedModuleManifestPath;
            cleanupScriptSourceOnFailure = false;
            return true;
        }
        finally
        {
            if (cleanupScriptSourceOnFailure)
            {
                TryCleanupTemporaryServiceContentRoot(scriptSource.TemporaryContentRootPath);
            }
        }
    }

    /// <summary>
    /// Removes a temporary service content root directory when archive extraction mode was used.
    /// </summary>
    /// <param name="temporaryContentRootPath">Temporary extraction path.</param>
    private static void TryCleanupTemporaryServiceContentRoot(string? temporaryContentRootPath)
    {
        if (string.IsNullOrWhiteSpace(temporaryContentRootPath) || !Directory.Exists(temporaryContentRootPath))
        {
            return;
        }

        try
        {
            TryDeleteDirectoryWithRetry(temporaryContentRootPath, maxAttempts: 5, initialDelayMs: 50);
        }
        catch
        {
            // Best-effort cleanup; do not fail install/remove flow on temp directory cleanup errors.
        }
    }

    /// <summary>
    /// Performs install-service preflight checks such as Windows elevation checks, gallery warnings, and privileged-user validation.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <param name="serviceName">Resolved service name.</param>
    /// <param name="moduleManifestPath">Resolved module manifest path.</param>
    /// <param name="serviceLogPath">Effective service log path.</param>
    /// <param name="skipGalleryCheck">True when gallery checks should be skipped.</param>
    /// <param name="exitCode">Exit code when a preflight check fails.</param>
    /// <returns>True when preflight checks pass.</returns>
    private static bool TryRunInstallServicePreflight(
        ParsedCommand command,
        string serviceName,
        string moduleManifestPath,
        string? serviceLogPath,
        bool skipGalleryCheck,
        out int exitCode)
    {
        exitCode = 0;

        if (OperatingSystem.IsWindows() && !TryPreflightWindowsServiceInstall(command, serviceName, out var preflightExitCode))
        {
            exitCode = preflightExitCode;
            return false;
        }

        if (!skipGalleryCheck)
        {
            WarnIfNewerGalleryVersionExists(moduleManifestPath, serviceLogPath);
        }

        if (OperatingSystem.IsLinux() && !string.IsNullOrWhiteSpace(command.ServiceUser) && !IsLikelyRunningAsRootOnLinux())
        {
            Console.Error.WriteLine("Linux system service install with --service-user requires root privileges.");
            exitCode = 1;
            return false;
        }

        if (OperatingSystem.IsMacOS() && !string.IsNullOrWhiteSpace(command.ServiceUser) && !IsLikelyRunningAsRootOnUnix())
        {
            Console.Error.WriteLine("macOS system daemon install with --service-user requires root privileges.");
            exitCode = 1;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Creates the service deployment bundle required by platform-specific service registration.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <param name="serviceName">Resolved service name.</param>
    /// <param name="serviceVersion">Optional resolved service version.</param>
    /// <param name="scriptSource">Resolved service script source.</param>
    /// <param name="moduleManifestPath">Resolved module manifest path.</param>
    /// <param name="serviceBundle">Prepared service bundle.</param>
    /// <param name="exitCode">Exit code when bundle preparation fails.</param>
    /// <returns>True when bundle preparation succeeds.</returns>
    private static bool TryPrepareInstallServiceBundle(
        ParsedCommand command,
        string serviceName,
        string? serviceVersion,
        ResolvedServiceScriptSource scriptSource,
        string moduleManifestPath,
        out ServiceBundleLayout serviceBundle,
        out int exitCode)
    {
        serviceBundle = default!;
        exitCode = 0;

        if (!TryPrepareServiceBundle(
                serviceName,
                scriptSource.FullScriptPath,
                moduleManifestPath,
                scriptSource.FullContentRoot,
                scriptSource.RelativeScriptPath,
                out var preparedServiceBundle,
                out var bundleError,
                command.ServiceDeploymentRoot,
                serviceVersion))
        {
            Console.Error.WriteLine(bundleError);
            exitCode = 1;
            return false;
        }

        if (preparedServiceBundle is null)
        {
            Console.Error.WriteLine("Service bundle preparation failed.");
            exitCode = 1;
            return false;
        }

        serviceBundle = preparedServiceBundle;
        return true;
    }

    /// <summary>
    /// Installs a prepared service bundle using the platform-specific daemon/service mechanism.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <param name="serviceName">Resolved service name.</param>
    /// <param name="serviceLogPath">Effective service log path.</param>
    /// <param name="serviceBundle">Prepared service bundle.</param>
    /// <returns>Process exit code.</returns>
    private static int InstallPreparedServiceForCurrentPlatform(ParsedCommand command, string serviceName, string? serviceLogPath, ServiceBundleLayout serviceBundle)
    {
        var daemonArgs = BuildDaemonHostArgumentsForService(
            serviceName,
            serviceBundle.ServiceHostExecutablePath,
            serviceBundle.RuntimeExecutablePath,
            serviceBundle.ScriptPath,
            serviceBundle.ModuleManifestPath,
            command.ScriptArguments,
            serviceLogPath);
        var workingDirectory = Path.GetDirectoryName(serviceBundle.ScriptPath) ?? Environment.CurrentDirectory;

        if (OperatingSystem.IsWindows())
        {
            return InstallWindowsService(
                command,
                serviceName,
                serviceLogPath,
                serviceBundle.ServiceHostExecutablePath,
                serviceBundle.RuntimeExecutablePath,
                serviceBundle.ScriptPath,
                serviceBundle.ModuleManifestPath);
        }

        if (OperatingSystem.IsLinux())
        {
            var result = InstallLinuxUserDaemon(serviceName, serviceBundle.ServiceHostExecutablePath, daemonArgs, workingDirectory, command.ServiceUser);
            WriteServiceOperationResult("install", "linux", serviceName, result, serviceLogPath);
            return result;
        }

        if (OperatingSystem.IsMacOS())
        {
            var result = InstallMacLaunchAgent(serviceName, serviceBundle.ServiceHostExecutablePath, daemonArgs, workingDirectory, command.ServiceUser);
            WriteServiceOperationResult("install", "macos", serviceName, result, serviceLogPath);
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
        ServiceControlResult result;

        if (OperatingSystem.IsWindows())
        {
            result = StartWindowsService(serviceName, command.ServiceLogPath, command.RawOutput);
            return WriteServiceControlResult(command, result);
        }

        if (OperatingSystem.IsLinux())
        {
            result = StartLinuxUserDaemon(serviceName, command.ServiceLogPath, command.RawOutput);
            return WriteServiceControlResult(command, result);
        }

        if (OperatingSystem.IsMacOS())
        {
            result = StartMacLaunchAgent(serviceName, command.ServiceLogPath, command.RawOutput);
            return WriteServiceControlResult(command, result);
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
        ServiceControlResult result;

        if (OperatingSystem.IsWindows())
        {
            result = StopWindowsService(serviceName, command.ServiceLogPath, command.RawOutput);
            return WriteServiceControlResult(command, result);
        }

        if (OperatingSystem.IsLinux())
        {
            result = StopLinuxUserDaemon(serviceName, command.ServiceLogPath, command.RawOutput);
            return WriteServiceControlResult(command, result);
        }

        if (OperatingSystem.IsMacOS())
        {
            result = StopMacLaunchAgent(serviceName, command.ServiceLogPath, command.RawOutput);
            return WriteServiceControlResult(command, result);
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
        ServiceControlResult result;

        if (OperatingSystem.IsWindows())
        {
            result = QueryWindowsService(serviceName, command.ServiceLogPath, command.RawOutput);
            return WriteServiceControlResult(command, result);
        }

        if (OperatingSystem.IsLinux())
        {
            result = QueryLinuxUserDaemon(serviceName, command.ServiceLogPath, command.RawOutput);
            return WriteServiceControlResult(command, result);
        }

        if (OperatingSystem.IsMacOS())
        {
            result = QueryMacLaunchAgent(serviceName, command.ServiceLogPath, command.RawOutput);
            return WriteServiceControlResult(command, result);
        }

        Console.Error.WriteLine("Service query is not supported on this OS.");
        return 1;
    }

    /// <summary>
    /// Represents normalized start/stop/query operation output.
    /// </summary>
    /// <param name="Operation">Operation token (start/stop/query).</param>
    /// <param name="ServiceName">Service identifier.</param>
    /// <param name="Platform">Platform token (windows/linux/macos).</param>
    /// <param name="State">Normalized service state.</param>
    /// <param name="Pid">Service process id when available.</param>
    /// <param name="ExitCode">Command exit code.</param>
    /// <param name="Message">Human-readable status message.</param>
    /// <param name="RawOutput">Raw standard output from the OS command when available.</param>
    /// <param name="RawError">Raw standard error from the OS command when available.</param>
    private sealed record ServiceControlResult(
        string Operation,
        string ServiceName,
        string Platform,
        string State,
        int? Pid,
        int ExitCode,
        string Message,
        string RawOutput,
        string RawError)
    {
        /// <summary>
        /// Returns true when the operation succeeded.
        /// </summary>
        public bool Success => ExitCode == 0;
    }

    /// <summary>
    /// Writes a service control result using table/json/raw output selection.
    /// </summary>
    /// <param name="command">Parsed command containing output switches.</param>
    /// <param name="result">Normalized service operation result.</param>
    /// <returns>Operation exit code.</returns>
    private static int WriteServiceControlResult(ParsedCommand command, ServiceControlResult result)
    {
        if (command.RawOutput)
        {
            if (!string.IsNullOrWhiteSpace(result.RawOutput))
            {
                Console.WriteLine(result.RawOutput.TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(result.RawError))
            {
                Console.Error.WriteLine(result.RawError.TrimEnd());
            }

            return result.ExitCode;
        }

        if (command.JsonOutput)
        {
            var payload = new
            {
                result.Operation,
                result.ServiceName,
                result.Platform,
                Status = result.Success ? "success" : "failed",
                result.State,
                PID = result.Pid,
                result.ExitCode,
                result.Message,
            };

            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
            }));
            return result.ExitCode;
        }

        var columns = new[] { "Operation", "Service", "Platform", "Status", "State", "PID", "ExitCode", "Message" };
        var values = new[]
        {
            result.Operation,
            result.ServiceName,
            result.Platform,
            result.Success ? "success" : "failed",
            result.State,
            result.Pid?.ToString(CultureInfo.InvariantCulture) ?? "-",
            result.ExitCode.ToString(CultureInfo.InvariantCulture),
            result.Message,
        };

        var widths = columns
            .Select((header, index) => Math.Max(header.Length, values[index].Length))
            .ToArray();

        Console.WriteLine(string.Join(" | ", columns.Select((header, index) => header.PadRight(widths[index]))));
        Console.WriteLine(string.Join("-+-", widths.Select(static width => new string('-', width))));
        Console.WriteLine(string.Join(" | ", values.Select((value, index) => value.PadRight(widths[index]))));
        return result.ExitCode;
    }

    /// <summary>
    /// Returns installed service descriptor metadata plus the resolved service bundle path.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <returns>Process exit code.</returns>
    private static int InfoService(ParsedCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.ServiceName))
        {
            if (!TryResolveInstalledServiceBundleRoot(command.ServiceName, command.ServiceDeploymentRoot, out var serviceRootPath, out var resolutionError))
            {
                Console.Error.WriteLine(resolutionError);
                return 1;
            }

            var scriptRoot = Path.Combine(serviceRootPath, ServiceBundleScriptDirectoryName);
            if (!TryResolveServiceInstallDescriptor(scriptRoot, out var descriptor, out var descriptorError))
            {
                Console.Error.WriteLine(descriptorError);
                return 1;
            }

            var descriptorPath = Path.Combine(scriptRoot, ServiceDescriptorFileName);
            var backups = GetServiceBackupSnapshots(serviceRootPath);
            var payload = new
            {
                command.ServiceName,
                ServicePath = serviceRootPath,
                DescriptorPath = descriptorPath,
                Descriptor = new
                {
                    descriptor.FormatVersion,
                    descriptor.Name,
                    descriptor.EntryPoint,
                    descriptor.Description,
                    descriptor.Version,
                    descriptor.ServiceLogPath,
                },
                Backups = backups.Select(static backup => new
                {
                    backup.Version,
                    UpdatedAtUtc = backup.UpdatedAtUtc?.ToString("o"),
                    backup.Path,
                }),
            };

            if (command.JsonOutput)
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                }));
            }
            else
            {
                WriteServiceInfoHumanReadable(payload.ServiceName, payload.ServicePath, payload.DescriptorPath, descriptor, backups);
            }

            return 0;
        }

        if (!TryEnumerateInstalledServiceBundleRoots(command.ServiceDeploymentRoot, out var bundleRoots, out var enumerateError))
        {
            Console.Error.WriteLine(enumerateError);
            return 1;
        }

        var services = new List<(string ServiceName, string ServicePath, string DescriptorPath, ServiceInstallDescriptor Descriptor, IReadOnlyList<ServiceBackupSnapshot> Backups)>();
        foreach (var bundleRoot in bundleRoots)
        {
            var scriptRoot = Path.Combine(bundleRoot, ServiceBundleScriptDirectoryName);
            if (!TryResolveServiceInstallDescriptor(scriptRoot, out var descriptor, out _))
            {
                continue;
            }

            var descriptorPath = Path.Combine(scriptRoot, ServiceDescriptorFileName);
            var backups = GetServiceBackupSnapshots(bundleRoot);
            services.Add((descriptor.Name, bundleRoot, descriptorPath, descriptor, backups));
        }

        if (services.Count == 0)
        {
            Console.Error.WriteLine("No installed Kestrun services were found.");
            return 1;
        }

        if (command.JsonOutput)
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                Services = services.Select(static service => new
                {
                    service.ServiceName,
                    service.ServicePath,
                    service.DescriptorPath,
                    Descriptor = new
                    {
                        service.Descriptor.FormatVersion,
                        service.Descriptor.Name,
                        service.Descriptor.EntryPoint,
                        service.Descriptor.Description,
                        service.Descriptor.Version,
                        service.Descriptor.ServiceLogPath,
                    },
                    Backups = service.Backups.Select(static backup => new
                    {
                        backup.Version,
                        UpdatedAtUtc = backup.UpdatedAtUtc?.ToString("o"),
                        backup.Path,
                    }),
                }),
            }, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
            }));

            return 0;
        }

        foreach (var (ServiceName, ServicePath, DescriptorPath, Descriptor, Backups) in services)
        {
            WriteServiceInfoHumanReadable(ServiceName, ServicePath, DescriptorPath, Descriptor, Backups);
            Console.WriteLine();
        }

        return 0;
    }

    /// <summary>
    /// Writes service information using a human-readable text format.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="servicePath">Service bundle path.</param>
    /// <param name="descriptorPath">Service descriptor file path.</param>
    /// <param name="descriptor">Parsed descriptor payload.</param>
    /// <param name="backups">Available backup snapshots for the service.</param>
    private static void WriteServiceInfoHumanReadable(
        string serviceName,
        string servicePath,
        string descriptorPath,
        ServiceInstallDescriptor descriptor,
        IReadOnlyList<ServiceBackupSnapshot> backups)
    {
        Console.WriteLine($"Name: {serviceName}");
        Console.WriteLine($"Path: {servicePath}");
        Console.WriteLine($"Descriptor: {descriptorPath}");
        Console.WriteLine($"FormatVersion: {descriptor.FormatVersion}");
        Console.WriteLine($"EntryPoint: {descriptor.EntryPoint}");
        Console.WriteLine($"Description: {descriptor.Description}");
        Console.WriteLine($"Version: {(string.IsNullOrWhiteSpace(descriptor.Version) ? "(not set)" : descriptor.Version)}");
        Console.WriteLine($"ServiceLogPath: {(string.IsNullOrWhiteSpace(descriptor.ServiceLogPath) ? "(not set)" : descriptor.ServiceLogPath)}");

        if (backups.Count == 0)
        {
            Console.WriteLine("Backups: (none)");
            return;
        }

        Console.WriteLine($"Backups: {backups.Count}");
        foreach (var backup in backups)
        {
            var updatedAt = backup.UpdatedAtUtc?.ToString("yyyy-MM-dd HH:mm:ss 'UTC'") ?? "(unknown)";
            Console.WriteLine($"  {backup.Version} | {updatedAt} | {backup.Path}");
        }
    }

    /// <summary>
    /// Represents one service backup snapshot.
    /// </summary>
    /// <param name="Version">Backup snapshot version token (folder name).</param>
    /// <param name="UpdatedAtUtc">Parsed update timestamp in UTC when available.</param>
    /// <param name="Path">Backup directory path.</param>
    private sealed record ServiceBackupSnapshot(string Version, DateTimeOffset? UpdatedAtUtc, string Path);

    /// <summary>
    /// Enumerates service backup snapshots from the backup root ordered from newest to oldest.
    /// </summary>
    /// <param name="serviceRootPath">Service root path.</param>
    /// <returns>Ordered backup snapshot list.</returns>
    private static List<ServiceBackupSnapshot> GetServiceBackupSnapshots(string serviceRootPath)
    {
        var backupRoot = Path.Combine(serviceRootPath, "backup");
        return !Directory.Exists(backupRoot)
            ? []
            : [.. Directory
            .GetDirectories(backupRoot)
            .Select(static directoryPath =>
            {
                var versionToken = Path.GetFileName(directoryPath);
                DateTimeOffset? updatedAtUtc = null;
                if (DateTime.TryParseExact(
                        versionToken,
                        "yyyyMMddHHmmss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var parsedUtc))
                {
                    updatedAtUtc = new DateTimeOffset(parsedUtc, TimeSpan.Zero);
                }

                return new ServiceBackupSnapshot(versionToken, updatedAtUtc, Path.GetFullPath(directoryPath));
            })
            .OrderByDescending(static backup => backup.UpdatedAtUtc)
            .ThenByDescending(static backup => backup.Version, StringComparer.OrdinalIgnoreCase)
            .ToList()];
    }

    /// <summary>
    /// Enumerates installed service bundle roots across deployment roots.
    /// </summary>
    /// <param name="deploymentRootOverride">Optional deployment root override.</param>
    /// <param name="bundleRoots">Resolved service bundle roots.</param>
    /// <param name="error">Enumeration error details.</param>
    /// <returns>True when at least one service bundle root is found.</returns>
    private static bool TryEnumerateInstalledServiceBundleRoots(string? deploymentRootOverride, out List<string> bundleRoots, out string error)
    {
        bundleRoots = [];
        error = string.Empty;

        var candidateRoots = new List<string>();
        if (!string.IsNullOrWhiteSpace(deploymentRootOverride))
        {
            candidateRoots.Add(deploymentRootOverride);
        }

        candidateRoots.AddRange(GetServiceDeploymentRootCandidates());

        foreach (var root in candidateRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                continue;
            }

            foreach (var serviceBaseRoot in Directory.GetDirectories(root))
            {
                var directDescriptorPath = Path.Combine(serviceBaseRoot, ServiceBundleScriptDirectoryName, ServiceDescriptorFileName);
                if (File.Exists(directDescriptorPath))
                {
                    bundleRoots.Add(Path.GetFullPath(serviceBaseRoot));
                }
            }
        }

        bundleRoots = [.. bundleRoots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)];

        if (bundleRoots.Count == 0)
        {
            error = "No installed Kestrun services were found.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Updates an installed service bundle from a package and/or module manifest source.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <returns>Process exit code.</returns>
    private static int UpdateService(ParsedCommand command)
    {
        if (!TryValidateUpdateServiceCommand(command, out var hasPackageUpdate, out var hasModuleUpdate, out var validationExitCode))
        {
            return validationExitCode;
        }

        if (!TryResolveUpdateServiceIdentity(command, hasPackageUpdate, out var serviceName, out var scriptSource, out var packageSourceResolved, out var identityExitCode))
        {
            return identityExitCode;
        }

        try
        {
            return ExecuteServiceUpdateFlow(
                command,
                serviceName,
                hasPackageUpdate,
                hasModuleUpdate,
                ref scriptSource,
                ref packageSourceResolved);
        }
        finally
        {
            TryCleanupTemporaryServiceContentRoot(scriptSource.TemporaryContentRootPath);
        }
    }

    /// <summary>
    /// Executes the resolved service update workflow including failback and update operations.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <param name="serviceName">Resolved service name.</param>
    /// <param name="hasPackageUpdate">True when package/content-root update was requested.</param>
    /// <param name="hasModuleUpdate">True when module update was requested.</param>
    /// <param name="scriptSource">Resolved package script source; may be populated lazily.</param>
    /// <param name="packageSourceResolved">True when <paramref name="scriptSource"/> is already resolved.</param>
    /// <returns>Process exit code.</returns>
    private static int ExecuteServiceUpdateFlow(
        ParsedCommand command,
        string serviceName,
        bool hasPackageUpdate,
        bool hasModuleUpdate,
        ref ResolvedServiceScriptSource scriptSource,
        ref bool packageSourceResolved)
    {
        if (!TryPrepareServiceUpdateExecution(serviceName, command.ServiceDeploymentRoot, out var paths, out var prepareExitCode))
        {
            return prepareExitCode;
        }

        if (command.ServiceFailback)
        {
            return TryExecuteServiceFailback(paths, out var failbackExitCode)
                ? 0
                : failbackExitCode;
        }

        if (!TryRunServiceUpdateOperations(
                command,
                hasPackageUpdate,
                hasModuleUpdate,
                paths,
                ref scriptSource,
                ref packageSourceResolved,
                out var applicationUpdated,
                out var moduleUpdated,
                out var serviceHostUpdated,
                out var updateExitCode))
        {
            return updateExitCode;
        }

        WriteServiceUpdateSummary(serviceName, paths, applicationUpdated, moduleUpdated, serviceHostUpdated);
        return 0;
    }

    /// <summary>
    /// Validates service run state and resolves installed bundle paths for update execution.
    /// </summary>
    /// <param name="serviceName">Resolved service name.</param>
    /// <param name="deploymentRootOverride">Optional deployment root override.</param>
    /// <param name="paths">Resolved service update path set.</param>
    /// <param name="exitCode">Exit code when validation or path resolution fails.</param>
    /// <returns>True when update execution prerequisites are satisfied.</returns>
    private static bool TryPrepareServiceUpdateExecution(
        string serviceName,
        string? deploymentRootOverride,
        out ServiceUpdatePaths paths,
        out int exitCode)
    {
        paths = default;
        exitCode = 0;

        if (!TryEnsureServiceIsStopped(serviceName, out var runningError))
        {
            Console.Error.WriteLine(runningError);
            exitCode = 1;
            return false;
        }

        if (!TryResolveServiceUpdatePaths(serviceName, deploymentRootOverride, out paths, out var pathExitCode))
        {
            exitCode = pathExitCode;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Applies package, module, and service-host updates for a resolved service bundle.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <param name="hasPackageUpdate">True when package/content-root update was requested.</param>
    /// <param name="hasModuleUpdate">True when module update was requested.</param>
    /// <param name="paths">Resolved service update path set.</param>
    /// <param name="scriptSource">Resolved package script source; may be populated when required.</param>
    /// <param name="packageSourceResolved">True when <paramref name="scriptSource"/> is already resolved.</param>
    /// <param name="applicationUpdated">True when application files were updated.</param>
    /// <param name="moduleUpdated">True when module files were updated.</param>
    /// <param name="serviceHostUpdated">True when service host binaries were updated.</param>
    /// <param name="exitCode">Exit code when any update stage fails.</param>
    /// <returns>True when all requested update stages succeed.</returns>
    private static bool TryRunServiceUpdateOperations(
        ParsedCommand command,
        bool hasPackageUpdate,
        bool hasModuleUpdate,
        ServiceUpdatePaths paths,
        ref ResolvedServiceScriptSource scriptSource,
        ref bool packageSourceResolved,
        out bool applicationUpdated,
        out bool moduleUpdated,
        out bool serviceHostUpdated,
        out int exitCode)
    {
        moduleUpdated = false;
        serviceHostUpdated = false;
        exitCode = 0;

        if (!TryApplyServicePackageUpdate(command, hasPackageUpdate, paths, ref scriptSource, ref packageSourceResolved, out applicationUpdated, out var packageExitCode))
        {
            exitCode = packageExitCode;
            return false;
        }

        if (!TryApplyServiceModuleUpdate(command, hasModuleUpdate, paths, out moduleUpdated, out var moduleExitCode))
        {
            exitCode = moduleExitCode;
            return false;
        }

        if (!TryApplyServiceHostUpdate(paths, out serviceHostUpdated, out var hostExitCode))
        {
            exitCode = hostExitCode;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates the supported option combinations for service update.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <param name="hasPackageUpdate">True when package/content-root update was requested.</param>
    /// <param name="hasModuleUpdate">True when module update was requested.</param>
    /// <param name="exitCode">Validation exit code when invalid options are supplied.</param>
    /// <returns>True when option combinations are valid.</returns>
    private static bool TryValidateUpdateServiceCommand(
        ParsedCommand command,
        out bool hasPackageUpdate,
        out bool hasModuleUpdate,
        out int exitCode)
    {
        hasPackageUpdate = !string.IsNullOrWhiteSpace(command.ServiceContentRoot);
        hasModuleUpdate = !string.IsNullOrWhiteSpace(command.KestrunManifestPath) || command.ServiceUseRepositoryKestrun;
        exitCode = 0;

        if (command.ServiceFailback && (!string.IsNullOrWhiteSpace(command.KestrunManifestPath) || command.ServiceUseRepositoryKestrun))
        {
            Console.Error.WriteLine("--failback cannot be combined with --kestrun, --kestrun-module, or --kestrun-manifest.");
            exitCode = 2;
            return false;
        }

        if (command.ServiceUseRepositoryKestrun && !string.IsNullOrWhiteSpace(command.KestrunManifestPath))
        {
            Console.Error.WriteLine("--kestrun cannot be combined with --kestrun-module or --kestrun-manifest.");
            exitCode = 2;
            return false;
        }

        if (!command.ServiceFailback && !hasPackageUpdate && !hasModuleUpdate)
        {
            Console.Error.WriteLine("Service update requires --package and/or --kestrun-module/--kestrun-manifest, or use --failback.");
            exitCode = 2;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves service identity and initial package metadata required by update and failback flows.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <param name="hasPackageUpdate">True when package/content-root update was requested.</param>
    /// <param name="serviceName">Resolved service name.</param>
    /// <param name="scriptSource">Resolved package script source when required.</param>
    /// <param name="packageSourceResolved">True when <paramref name="scriptSource"/> was resolved.</param>
    /// <param name="exitCode">Exit code when identity resolution fails.</param>
    /// <returns>True when identity resolution succeeds.</returns>
    private static bool TryResolveUpdateServiceIdentity(
        ParsedCommand command,
        bool hasPackageUpdate,
        out string serviceName,
        out ResolvedServiceScriptSource scriptSource,
        out bool packageSourceResolved,
        out int exitCode)
    {
        serviceName = command.ServiceName ?? string.Empty;
        scriptSource = CreateEmptyResolvedServiceScriptSource();
        packageSourceResolved = false;
        exitCode = 0;

        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            return true;
        }

        if (!hasPackageUpdate)
        {
            Console.Error.WriteLine("Service name is required. Use --name <value>.");
            exitCode = 2;
            return false;
        }

        if (!TryResolveServiceScriptSource(command, out scriptSource, out var scriptError))
        {
            Console.Error.WriteLine(scriptError);
            exitCode = 2;
            return false;
        }

        packageSourceResolved = true;
        if (string.IsNullOrWhiteSpace(scriptSource.DescriptorServiceName))
        {
            Console.Error.WriteLine("Service name is required in Service.psd1 (Name) when using --package.");
            exitCode = 2;
            return false;
        }

        serviceName = scriptSource.DescriptorServiceName;
        return true;
    }

    /// <summary>
    /// Resolves the installed service bundle paths required by update operations.
    /// </summary>
    /// <param name="serviceName">Target service name.</param>
    /// <param name="deploymentRootOverride">Optional deployment root override.</param>
    /// <param name="paths">Resolved service update path set.</param>
    /// <param name="exitCode">Exit code when path resolution fails.</param>
    /// <returns>True when service bundle paths are resolved.</returns>
    private static bool TryResolveServiceUpdatePaths(
        string serviceName,
        string? deploymentRootOverride,
        out ServiceUpdatePaths paths,
        out int exitCode)
    {
        paths = default;
        exitCode = 0;

        if (!TryResolveInstalledServiceBundleRoot(serviceName, deploymentRootOverride, out var serviceRootPath, out var resolutionError))
        {
            Console.Error.WriteLine(resolutionError);
            exitCode = 1;
            return false;
        }

        paths = new ServiceUpdatePaths(
            serviceRootPath,
            Path.Combine(serviceRootPath, ServiceBundleScriptDirectoryName),
            Path.Combine(serviceRootPath, ServiceBundleModulesDirectoryName, ModuleName),
            Path.Combine(serviceRootPath, "backup", DateTime.UtcNow.ToString("yyyyMMddHHmmss")));
        return true;
    }

    /// <summary>
    /// Executes service failback and writes a JSON summary payload.
    /// </summary>
    /// <param name="paths">Resolved service update path set.</param>
    /// <param name="exitCode">Exit code when failback fails.</param>
    /// <returns>True when failback succeeds.</returns>
    private static bool TryExecuteServiceFailback(ServiceUpdatePaths paths, out int exitCode)
    {
        exitCode = 0;

        if (!TryFailbackServiceFromBackup(paths.ServiceRootPath, paths.ScriptRoot, paths.ModuleRoot, out var failbackSummary, out var failbackError))
        {
            Console.Error.WriteLine(failbackError);
            exitCode = 1;
            return false;
        }

        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(failbackSummary, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
        }));

        return true;
    }

    /// <summary>
    /// Applies package/application update to the installed service bundle.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <param name="hasPackageUpdate">True when package update was requested.</param>
    /// <param name="paths">Resolved service update path set.</param>
    /// <param name="scriptSource">Current resolved script source; may be populated when needed.</param>
    /// <param name="packageSourceResolved">True when <paramref name="scriptSource"/> is already resolved.</param>
    /// <param name="applicationUpdated">True when application files were updated.</param>
    /// <param name="exitCode">Exit code when update fails.</param>
    /// <returns>True when package update succeeds or is not requested.</returns>
    private static bool TryApplyServicePackageUpdate(
        ParsedCommand command,
        bool hasPackageUpdate,
        ServiceUpdatePaths paths,
        ref ResolvedServiceScriptSource scriptSource,
        ref bool packageSourceResolved,
        out bool applicationUpdated,
        out int exitCode)
    {
        applicationUpdated = false;
        exitCode = 0;

        if (!hasPackageUpdate)
        {
            return true;
        }

        if (!TryEnsureServicePackageSourceResolved(command, ref scriptSource, ref packageSourceResolved, out exitCode))
        {
            return false;
        }

        if (!TryValidateServicePackageUpdateContext(paths.ScriptRoot, scriptSource, out var contentRoot, out exitCode))
        {
            return false;
        }

        if (!TryApplyServiceApplicationReplacement(paths, contentRoot, scriptSource.DescriptorPreservePaths, out exitCode))
        {
            return false;
        }

        applicationUpdated = true;
        return true;
    }

    /// <summary>
    /// Ensures package script metadata is resolved for service package update operations.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <param name="scriptSource">Current resolved script source; populated when resolution is required.</param>
    /// <param name="packageSourceResolved">True when <paramref name="scriptSource"/> is already resolved.</param>
    /// <param name="exitCode">Exit code when source resolution fails.</param>
    /// <returns>True when package script source is available.</returns>
    private static bool TryEnsureServicePackageSourceResolved(
        ParsedCommand command,
        ref ResolvedServiceScriptSource scriptSource,
        ref bool packageSourceResolved,
        out int exitCode)
    {
        exitCode = 0;
        if (packageSourceResolved)
        {
            return true;
        }

        if (!TryResolveServiceScriptSource(command, out scriptSource, out var scriptError))
        {
            Console.Error.WriteLine(scriptError);
            exitCode = 2;
            return false;
        }

        packageSourceResolved = true;
        return true;
    }

    /// <summary>
    /// Validates package update preconditions against the installed service descriptor and content root.
    /// </summary>
    /// <param name="scriptRoot">Installed service script root.</param>
    /// <param name="scriptSource">Resolved incoming package script source.</param>
    /// <param name="contentRoot">Validated package content root path.</param>
    /// <param name="exitCode">Exit code when validation fails.</param>
    /// <returns>True when package update preconditions are satisfied.</returns>
    private static bool TryValidateServicePackageUpdateContext(
        string scriptRoot,
        ResolvedServiceScriptSource scriptSource,
        out string contentRoot,
        out int exitCode)
    {
        contentRoot = string.Empty;
        exitCode = 0;

        if (!TryResolveServiceInstallDescriptor(scriptRoot, out var runningDescriptor, out var currentDescriptorError))
        {
            Console.Error.WriteLine(currentDescriptorError);
            exitCode = 1;
            return false;
        }

        if (!TryValidateServicePackageVersionUpdate(
                runningDescriptor.Version,
                scriptSource.DescriptorServiceVersion,
                out _,
                out var versionWarning,
                out var versionValidationError))
        {
            Console.Error.WriteLine(versionValidationError);
            exitCode = 1;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(versionWarning))
        {
            Console.WriteLine(versionWarning);
        }

        if (string.IsNullOrWhiteSpace(scriptSource.FullContentRoot) || !Directory.Exists(scriptSource.FullContentRoot))
        {
            Console.Error.WriteLine("Resolved package content root is not available for update.");
            exitCode = 1;
            return false;
        }

        contentRoot = scriptSource.FullContentRoot;

        return true;
    }

    /// <summary>
    /// Backs up and replaces the installed service application directory from package content.
    /// </summary>
    /// <param name="paths">Resolved service update path set.</param>
    /// <param name="contentRoot">Validated source content root path.</param>
    /// <param name="preserveRelativePaths">Optional preserve-path entries from the service descriptor.</param>
    /// <param name="exitCode">Exit code when replacement fails.</param>
    /// <returns>True when backup and replacement succeed.</returns>
    private static bool TryApplyServiceApplicationReplacement(
        ServiceUpdatePaths paths,
        string contentRoot,
        IReadOnlyList<string>? preserveRelativePaths,
        out int exitCode)
    {
        exitCode = 0;

        if (!TryBackupDirectory(paths.ScriptRoot, Path.Combine(paths.BackupRoot, "application"), out var backupAppError))
        {
            Console.Error.WriteLine(backupAppError);
            exitCode = 1;
            return false;
        }

        if (!TryReplaceDirectoryFromSource(
            contentRoot,
                paths.ScriptRoot,
                "Updating service application",
                out var appReplaceError,
                exclusionPatterns: null,
            preserveRelativePaths: preserveRelativePaths))
        {
            Console.Error.WriteLine(appReplaceError);
            exitCode = 1;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Applies module update logic for either repository module replacement or explicit manifest replacement.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <param name="hasModuleUpdate">True when module update was requested.</param>
    /// <param name="paths">Resolved service update path set.</param>
    /// <param name="moduleUpdated">True when bundled module files were replaced.</param>
    /// <param name="exitCode">Exit code when module update fails.</param>
    /// <returns>True when module update succeeds or is not requested.</returns>
    private static bool TryApplyServiceModuleUpdate(
        ParsedCommand command,
        bool hasModuleUpdate,
        ServiceUpdatePaths paths,
        out bool moduleUpdated,
        out int exitCode)
    {
        moduleUpdated = false;
        exitCode = 0;

        if (!hasModuleUpdate)
        {
            return true;
        }

        if (!TryResolveUpdateManifestPath(command, out var manifestPath, out exitCode))
        {
            return false;
        }

        if (!TryResolveSourceModuleRoot(manifestPath, out var sourceModuleRoot, out var sourceModuleRootError))
        {
            Console.Error.WriteLine(sourceModuleRootError);
            exitCode = 1;
            return false;
        }

        if (!command.ServiceUseRepositoryKestrun)
        {
            return TryApplyDirectModuleReplacement(sourceModuleRoot, paths, out moduleUpdated, out exitCode);
        }

        var bundledManifestPath = Path.Combine(paths.ModuleRoot, ModuleManifestFileName);
        if (!TryEvaluateRepositoryModuleUpdateNeeded(manifestPath, bundledManifestPath, out var shouldUpdateBundledModule, out var moduleDecisionMessage, out var moduleDecisionError))
        {
            Console.Error.WriteLine(moduleDecisionError);
            exitCode = 1;
            return false;
        }

        if (!shouldUpdateBundledModule)
        {
            Console.WriteLine(moduleDecisionMessage);
            return true;
        }

        return TryApplyDirectModuleReplacement(sourceModuleRoot, paths, out moduleUpdated, out exitCode);
    }

    /// <summary>
    /// Replaces the bundled module directory from the provided source module root with backup creation.
    /// </summary>
    /// <param name="sourceModuleRoot">Source module root directory.</param>
    /// <param name="paths">Resolved service update path set.</param>
    /// <param name="moduleUpdated">True when module replacement succeeds.</param>
    /// <param name="exitCode">Exit code when replacement fails.</param>
    /// <returns>True when module replacement succeeds.</returns>
    private static bool TryApplyDirectModuleReplacement(
        string sourceModuleRoot,
        ServiceUpdatePaths paths,
        out bool moduleUpdated,
        out int exitCode)
    {
        moduleUpdated = false;
        exitCode = 0;

        if (!TryBackupDirectory(paths.ModuleRoot, Path.Combine(paths.BackupRoot, "module"), out var backupModuleError))
        {
            Console.Error.WriteLine(backupModuleError);
            exitCode = 1;
            return false;
        }

        if (!TryReplaceDirectoryFromSource(sourceModuleRoot, paths.ModuleRoot, "Updating bundled Kestrun module", out var moduleReplaceError, ServiceBundleModuleExclusionPatterns))
        {
            Console.Error.WriteLine(moduleReplaceError);
            exitCode = 1;
            return false;
        }

        moduleUpdated = true;
        return true;
    }

    /// <summary>
    /// Resolves the manifest path to use for service module update.
    /// </summary>
    /// <param name="command">Parsed command information.</param>
    /// <param name="manifestPath">Resolved manifest path.</param>
    /// <param name="exitCode">Exit code when resolution fails.</param>
    /// <returns>True when manifest resolution succeeds.</returns>
    private static bool TryResolveUpdateManifestPath(ParsedCommand command, out string manifestPath, out int exitCode)
    {
        manifestPath = string.Empty;
        exitCode = 0;

        var resolvedManifestPath = command.ServiceUseRepositoryKestrun
            ? ResolveRepositoryModuleManifestPath()
            : LocateModuleManifest(command.KestrunManifestPath, command.KestrunFolder);

        if (resolvedManifestPath is null)
        {
            if (command.ServiceUseRepositoryKestrun)
            {
                Console.Error.WriteLine("Unable to locate repository module manifest at 'src/PowerShell/Kestrun/Kestrun.psd1' from the current working tree.");
                exitCode = 1;
                return false;
            }

            WriteModuleNotFoundMessage(command.KestrunManifestPath, command.KestrunFolder, Console.Error.WriteLine);
            exitCode = 3;
            return false;
        }

        manifestPath = resolvedManifestPath;
        return true;
    }

    /// <summary>
    /// Resolves and validates the source module root directory from a manifest path.
    /// </summary>
    /// <param name="manifestPath">Module manifest path.</param>
    /// <param name="sourceModuleRoot">Resolved module root directory.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when the source module root is valid.</returns>
    private static bool TryResolveSourceModuleRoot(string manifestPath, out string sourceModuleRoot, out string error)
    {
        sourceModuleRoot = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? string.Empty;
        error = string.Empty;

        if (!string.IsNullOrWhiteSpace(sourceModuleRoot) && Directory.Exists(sourceModuleRoot))
        {
            return true;
        }

        error = $"Unable to resolve module root from manifest path: {manifestPath}";
        return false;
    }

    /// <summary>
    /// Applies service-host runtime update when a newer host binary is available.
    /// </summary>
    /// <param name="paths">Resolved service update path set.</param>
    /// <param name="serviceHostUpdated">True when service host binaries were updated.</param>
    /// <param name="exitCode">Exit code when update fails.</param>
    /// <returns>True when service host update succeeds.</returns>
    private static bool TryApplyServiceHostUpdate(ServiceUpdatePaths paths, out bool serviceHostUpdated, out int exitCode)
    {
        exitCode = 0;

        var runtimeDirectory = Path.Combine(paths.ServiceRootPath, ServiceBundleRuntimeDirectoryName);
        if (TryUpdateBundledServiceHostIfNewer(runtimeDirectory, Path.Combine(paths.BackupRoot, "servicehost"), out var hostUpdateError, out serviceHostUpdated))
        {
            return true;
        }

        Console.Error.WriteLine(hostUpdateError);
        exitCode = 1;
        return false;
    }

    /// <summary>
    /// Writes service update results as indented JSON.
    /// </summary>
    /// <param name="serviceName">Resolved service name.</param>
    /// <param name="paths">Resolved service update path set.</param>
    /// <param name="applicationUpdated">True when application files were updated.</param>
    /// <param name="moduleUpdated">True when module files were updated.</param>
    /// <param name="serviceHostUpdated">True when service host was updated.</param>
    private static void WriteServiceUpdateSummary(
        string serviceName,
        ServiceUpdatePaths paths,
        bool applicationUpdated,
        bool moduleUpdated,
        bool serviceHostUpdated)
    {
        var summary = new
        {
            ServiceName = serviceName,
            ServicePath = paths.ServiceRootPath,
            ApplicationUpdated = applicationUpdated,
            ModuleUpdated = moduleUpdated,
            ServiceHostUpdated = serviceHostUpdated,
            BackupPath = Directory.Exists(paths.BackupRoot) ? paths.BackupRoot : null,
        };

        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(summary, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
        }));
    }

    /// <summary>
    /// Contains resolved directory paths used by service update and failback operations.
    /// </summary>
    /// <param name="ServiceRootPath">Installed service bundle root path.</param>
    /// <param name="ScriptRoot">Installed service script directory path.</param>
    /// <param name="ModuleRoot">Installed bundled module root path.</param>
    /// <param name="BackupRoot">Backup snapshot root path for the current update operation.</param>
    private readonly record struct ServiceUpdatePaths(
        string ServiceRootPath,
        string ScriptRoot,
        string ModuleRoot,
        string BackupRoot);

    /// <summary>
    /// Resolves the repository-local Kestrun manifest path by scanning current directory ancestors.
    /// </summary>
    /// <returns>Absolute manifest path when found; otherwise null.</returns>
    private static string? ResolveRepositoryModuleManifestPath()
    {
        foreach (var parent in EnumerateDirectoryAndParents(Environment.CurrentDirectory))
        {
            var candidate = Path.Combine(parent, "src", "PowerShell", ModuleName, ModuleManifestFileName);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether repository module content should replace the bundled module based on semantic version comparison.
    /// </summary>
    /// <param name="repositoryManifestPath">Repository Kestrun manifest path.</param>
    /// <param name="bundledManifestPath">Bundled service module manifest path.</param>
    /// <param name="shouldUpdate">True when the bundled module should be replaced.</param>
    /// <param name="message">Decision summary message when no update is required.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when comparison succeeds.</returns>
    private static bool TryEvaluateRepositoryModuleUpdateNeeded(
        string repositoryManifestPath,
        string bundledManifestPath,
        out bool shouldUpdate,
        out string message,
        out string error)
    {
        shouldUpdate = false;
        message = string.Empty;
        error = string.Empty;

        if (!TryReadModuleSemanticVersionFromManifest(repositoryManifestPath, out var repositoryVersion))
        {
            error = $"Unable to read ModuleVersion from repository manifest '{repositoryManifestPath}'.";
            return false;
        }

        if (!File.Exists(bundledManifestPath))
        {
            shouldUpdate = true;
            return true;
        }

        if (!TryReadModuleSemanticVersionFromManifest(bundledManifestPath, out var bundledVersion))
        {
            error = $"Unable to read ModuleVersion from bundled manifest '{bundledManifestPath}'.";
            return false;
        }

        var comparison = CompareModuleVersionValues(repositoryVersion, bundledVersion);
        if (comparison > 0)
        {
            shouldUpdate = true;
            return true;
        }

        message = $"Bundled Kestrun module version '{bundledVersion}' is current or newer than repository version '{repositoryVersion}'. Skipping module update.";
        return true;
    }

    /// <summary>
    /// Restores service application/module directories from the latest backup folder and removes the consumed backup.
    /// </summary>
    /// <param name="serviceRootPath">Resolved service bundle root.</param>
    /// <param name="scriptRoot">Target script root directory.</param>
    /// <param name="moduleRoot">Target bundled module root directory.</param>
    /// <param name="summary">Serialized summary payload.</param>
    /// <param name="error">Failback error details.</param>
    /// <returns>True when failback succeeds.</returns>
    private static bool TryFailbackServiceFromBackup(
        string serviceRootPath,
        string scriptRoot,
        string moduleRoot,
        out object summary,
        out string error)
    {
        summary = new { };

        if (!TryResolveLatestServiceBackupDirectory(serviceRootPath, out var latestBackupPath, out error))
        {
            return false;
        }

        var backupApplicationPath = Path.Combine(latestBackupPath, "application");
        var backupModulePath = Path.Combine(latestBackupPath, "module");
        var hasApplicationBackup = Directory.Exists(backupApplicationPath);
        var hasModuleBackup = Directory.Exists(backupModulePath);

        if (!hasApplicationBackup && !hasModuleBackup)
        {
            error = $"Backup '{latestBackupPath}' does not contain application or module content.";
            return false;
        }

        if (hasApplicationBackup
            && !TryReplaceDirectoryFromSource(backupApplicationPath, scriptRoot, "Failback service application", out var applicationRestoreError))
        {
            error = applicationRestoreError;
            return false;
        }

        if (hasModuleBackup
            && !TryReplaceDirectoryFromSource(backupModulePath, moduleRoot, "Failback bundled Kestrun module", out var moduleRestoreError))
        {
            error = moduleRestoreError;
            return false;
        }

        try
        {
            TryDeleteDirectoryWithRetry(latestBackupPath, maxAttempts: 5, initialDelayMs: 50);
        }
        catch (Exception ex)
        {
            error = $"Failback succeeded but backup folder '{latestBackupPath}' could not be removed: {ex.Message}";
            return false;
        }

        summary = new
        {
            ServicePath = serviceRootPath,
            ApplicationReverted = hasApplicationBackup,
            ModuleReverted = hasModuleBackup,
            ConsumedBackupPath = latestBackupPath,
            BackupRemoved = true,
        };

        return true;
    }

    /// <summary>
    /// Resolves the latest service backup directory from the service backup root.
    /// </summary>
    /// <param name="serviceRootPath">Resolved service bundle root.</param>
    /// <param name="backupDirectoryPath">Resolved latest backup path.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when a backup directory exists.</returns>
    private static bool TryResolveLatestServiceBackupDirectory(string serviceRootPath, out string backupDirectoryPath, out string error)
    {
        backupDirectoryPath = string.Empty;
        error = string.Empty;

        var backupRoot = Path.Combine(serviceRootPath, "backup");
        if (!Directory.Exists(backupRoot))
        {
            error = $"No backup folder found under '{backupRoot}'.";
            return false;
        }

        var candidates = Directory
            .GetDirectories(backupRoot)
            .Select(static path => new
            {
                Path = path,
                Name = Path.GetFileName(path),
                LastWriteUtc = Directory.GetLastWriteTimeUtc(path),
            })
            .OrderByDescending(static candidate => candidate.Name)
            .ThenByDescending(static candidate => candidate.LastWriteUtc)
            .ToList();

        if (candidates.Count == 0)
        {
            error = $"No backup folder found under '{backupRoot}'.";
            return false;
        }

        backupDirectoryPath = candidates[0].Path;
        return true;
    }

    /// <summary>
    /// Validates that a descriptor version string is present and compatible with System.Version.
    /// </summary>
    /// <param name="descriptorVersion">Descriptor version string.</param>
    /// <param name="version">Parsed version.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when version parsing succeeds.</returns>
    private static bool TryParseServiceDescriptorVersion(string? descriptorVersion, out Version version, out string error)
    {
        version = new Version(0, 0);
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(descriptorVersion))
        {
            error = "Service descriptor Version is required for update comparison.";
            return false;
        }

        if (!Version.TryParse(descriptorVersion.Trim(), out var parsedVersion) || parsedVersion is null)
        {
            error = $"Service descriptor Version '{descriptorVersion}' is not compatible with System.Version.";
            return false;
        }

        version = parsedVersion;
        return true;
    }

    /// <summary>
    /// Validates package-version progression for service updates.
    /// </summary>
    /// <param name="installedDescriptorVersion">Installed descriptor version.</param>
    /// <param name="packageDescriptorVersion">Incoming package descriptor version.</param>
    /// <param name="packageVersion">Parsed incoming package version.</param>
    /// <param name="warning">Optional warning when installed version metadata is missing.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when package update version checks pass.</returns>
    private static bool TryValidateServicePackageVersionUpdate(
        string? installedDescriptorVersion,
        string? packageDescriptorVersion,
        out Version packageVersion,
        out string? warning,
        out string error)
    {
        packageVersion = new Version(0, 0);
        warning = null;
        error = string.Empty;

        if (!TryParseServiceDescriptorVersion(packageDescriptorVersion, out var parsedPackageVersion, out var packageVersionError))
        {
            error = $"Unable to compare package version: {packageVersionError}";
            return false;
        }

        packageVersion = parsedPackageVersion;

        if (string.IsNullOrWhiteSpace(installedDescriptorVersion))
        {
            warning = "Installed service descriptor Version is missing. Skipping installed-version comparison for this update.";
            return true;
        }

        if (!TryParseServiceDescriptorVersion(installedDescriptorVersion, out var installedVersion, out var installedVersionError))
        {
            error = $"Unable to compare installed version: {installedVersionError}";
            return false;
        }

        if (packageVersion <= installedVersion)
        {
            error = $"Package version '{packageVersion}' must be greater than installed version '{installedVersion}'.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns false when the service is currently running.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="error">State validation details.</param>
    /// <returns>True when service is stopped or inactive.</returns>
    private static bool TryEnsureServiceIsStopped(string serviceName, out string error)
    {
        if (OperatingSystem.IsWindows())
        {
            return TryEnsureWindowsServiceIsStopped(serviceName, out error);
        }

        if (OperatingSystem.IsLinux())
        {
            return TryEnsureLinuxServiceIsStopped(serviceName, out error);
        }

        if (OperatingSystem.IsMacOS())
        {
            return TryEnsureMacServiceIsStopped(serviceName, out error);
        }

        error = "Service update is not supported on this OS.";
        return false;
    }

    /// <summary>
    /// Returns false when a Windows service is currently running.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="error">State validation details.</param>
    /// <returns>True when service is stopped or inactive.</returns>
    private static bool TryEnsureWindowsServiceIsStopped(string serviceName, out string error)
    {
        var queryResult = RunProcess("sc.exe", ["query", serviceName], writeStandardOutput: false);
        if (queryResult.ExitCode != 0)
        {
            error = string.IsNullOrWhiteSpace(queryResult.Error)
                ? $"Unable to query service '{serviceName}'."
                : queryResult.Error.Trim();
            return false;
        }

        var stateLine = queryResult.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(static line => line.Contains("STATE", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(stateLine) && stateLine.Contains("RUNNING", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Service '{serviceName}' is running. Stop it before update.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Returns false when a Linux service unit is currently active.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="error">State validation details.</param>
    /// <returns>True when service is stopped or inactive.</returns>
    private static bool TryEnsureLinuxServiceIsStopped(string serviceName, out string error)
    {
        var useSystemScope = IsLinuxSystemUnitInstalled(serviceName);
        var unitName = GetLinuxUnitName(serviceName);
        var activeResult = RunLinuxSystemctl(useSystemScope, ["is-active", unitName]);
        if (activeResult.ExitCode == 0)
        {
            error = $"Service '{serviceName}' is running. Stop it before update.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Returns false when a macOS launchd service is currently running.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="error">State validation details.</param>
    /// <returns>True when service is stopped or inactive.</returns>
    private static bool TryEnsureMacServiceIsStopped(string serviceName, out string error)
    {
        var useSystemScope = IsMacSystemLaunchDaemonInstalled(serviceName);
        var result = useSystemScope
            ? RunProcess("launchctl", ["print", $"system/{serviceName}"])
            : RunProcess("launchctl", ["list", serviceName]);

        if (result.ExitCode != 0)
        {
            error = string.Empty;
            return true;
        }

        if (useSystemScope)
        {
            var running = result.Output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(static line => line.Contains("state = running", StringComparison.OrdinalIgnoreCase));

            if (running)
            {
                error = $"Service '{serviceName}' is running. Stop it before update.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        var columns = result.Output.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (columns.Length > 0 && int.TryParse(columns[0], out var pid) && pid > 0)
        {
            error = $"Service '{serviceName}' is running. Stop it before update.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Creates a backup copy of a directory when it exists.
    /// </summary>
    /// <param name="sourceDirectory">Directory to back up.</param>
    /// <param name="backupDirectory">Backup destination directory.</param>
    /// <param name="error">Backup error details.</param>
    /// <returns>True when backup succeeds or source does not exist.</returns>
    private static bool TryBackupDirectory(string sourceDirectory, string backupDirectory, out string error)
    {
        error = string.Empty;
        if (!Directory.Exists(sourceDirectory))
        {
            return true;
        }

        try
        {
            _ = Directory.CreateDirectory(backupDirectory);
            CopyDirectoryContents(sourceDirectory, backupDirectory, showProgress: false, "Creating backup", exclusionPatterns: null);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to back up '{sourceDirectory}' to '{backupDirectory}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Replaces a target directory from a source directory.
    /// </summary>
    /// <param name="sourceDirectory">Source directory.</param>
    /// <param name="targetDirectory">Target directory.</param>
    /// <param name="operationName">Operation label for progress output.</param>
    /// <param name="error">Replacement error details.</param>
    /// <param name="exclusionPatterns">Optional exclusion patterns.</param>
    /// <returns>True when replacement succeeds.</returns>
    private static bool TryReplaceDirectoryFromSource(
        string sourceDirectory,
        string targetDirectory,
        string operationName,
        out string error,
        IReadOnlyList<string>? exclusionPatterns = null,
        IReadOnlyList<string>? preserveRelativePaths = null)
    {
        error = string.Empty;
        string? preserveStagingRoot = null;
        try
        {
            if (preserveRelativePaths is not null
                && preserveRelativePaths.Count > 0
                && Directory.Exists(targetDirectory)
                && !TryStagePreservedPaths(targetDirectory, preserveRelativePaths, out preserveStagingRoot, out error))
            {
                return false;
            }

            if (Directory.Exists(targetDirectory))
            {
                Directory.Delete(targetDirectory, recursive: true);
            }

            _ = Directory.CreateDirectory(targetDirectory);
            CopyDirectoryContents(sourceDirectory, targetDirectory, showProgress: !Console.IsOutputRedirected, operationName, exclusionPatterns);

            return string.IsNullOrWhiteSpace(preserveStagingRoot)
                || TryRestorePreservedPaths(preserveStagingRoot, targetDirectory, out error);
        }
        catch (Exception ex)
        {
            error = $"Failed to replace '{targetDirectory}' from '{sourceDirectory}': {ex.Message}";
            return false;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(preserveStagingRoot) && Directory.Exists(preserveStagingRoot))
            {
                try
                {
                    TryDeleteDirectoryWithRetry(preserveStagingRoot, maxAttempts: 5, initialDelayMs: 50);
                }
                catch
                {
                    // Best-effort cleanup for preserve staging directory.
                }
            }
        }
    }

    /// <summary>
    /// Stages preserve-path files/directories from an existing target directory into a temporary folder.
    /// </summary>
    /// <param name="targetDirectory">Existing target directory whose content is being replaced.</param>
    /// <param name="preserveRelativePaths">Relative preserve paths declared in the package descriptor.</param>
    /// <param name="preserveStagingRoot">Temporary preserve staging root path.</param>
    /// <param name="error">Staging error details.</param>
    /// <returns>True when staging succeeds.</returns>
    private static bool TryStagePreservedPaths(
        string targetDirectory,
        IReadOnlyList<string> preserveRelativePaths,
        out string preserveStagingRoot,
        out string error)
    {
        preserveStagingRoot = Path.Combine(Path.GetTempPath(), $"kestrun-preserve-{Guid.NewGuid():N}");
        error = string.Empty;

        var targetRootFullPath = Path.GetFullPath(targetDirectory);
        var preservePathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var normalizedPreservePaths = new HashSet<string>(preservePathComparer);
        foreach (var preservePath in preserveRelativePaths)
        {
            if (!TryNormalizePreservePath(preservePath, out var normalizedPath, out error))
            {
                return false;
            }

            _ = normalizedPreservePaths.Add(normalizedPath);
        }

        _ = Directory.CreateDirectory(preserveStagingRoot);
        foreach (var normalizedPath in normalizedPreservePaths)
        {
            var sourcePath = Path.GetFullPath(Path.Combine(targetRootFullPath, normalizedPath));
            if (!IsPathWithinDirectory(sourcePath, targetRootFullPath))
            {
                error = $"PreservePaths entry '{normalizedPath}' escapes the service application root.";
                return false;
            }

            var stagedPath = Path.Combine(preserveStagingRoot, normalizedPath);
            var stagedDirectory = Path.GetDirectoryName(stagedPath);
            if (!string.IsNullOrWhiteSpace(stagedDirectory))
            {
                _ = Directory.CreateDirectory(stagedDirectory);
            }

            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, stagedPath, overwrite: true);
                continue;
            }

            if (Directory.Exists(sourcePath))
            {
                _ = Directory.CreateDirectory(stagedPath);
                CopyDirectoryContents(sourcePath, stagedPath, showProgress: false, "Staging preserved paths", exclusionPatterns: null);
            }
        }

        return true;
    }

    /// <summary>
    /// Restores staged preserve-path files/directories into the replaced target directory.
    /// </summary>
    /// <param name="preserveStagingRoot">Preserve staging root path.</param>
    /// <param name="targetDirectory">Replacement target directory.</param>
    /// <param name="error">Restore error details.</param>
    /// <returns>True when restore succeeds.</returns>
    private static bool TryRestorePreservedPaths(string preserveStagingRoot, string targetDirectory, out string error)
    {
        error = string.Empty;
        try
        {
            foreach (var directoryPath in Directory.GetDirectories(preserveStagingRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(preserveStagingRoot, directoryPath);
                var destinationDirectory = Path.Combine(targetDirectory, relativePath);
                _ = Directory.CreateDirectory(destinationDirectory);
            }

            foreach (var filePath in Directory.GetFiles(preserveStagingRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(preserveStagingRoot, filePath);
                var destinationPath = Path.Combine(targetDirectory, relativePath);
                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    _ = Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(filePath, destinationPath, overwrite: true);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to restore preserved paths into '{targetDirectory}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Validates and normalizes one PreservePaths entry.
    /// </summary>
    /// <param name="rawPath">Raw path value from the descriptor.</param>
    /// <param name="normalizedPath">Normalized relative path.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when the preserve path is valid.</returns>
    private static bool TryNormalizePreservePath(string rawPath, out string normalizedPath, out string error)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            error = $"Service descriptor '{ServiceDescriptorFileName}' contains an empty PreservePaths entry.";
            return false;
        }

        var candidate = rawPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).Trim();
        if (Path.IsPathRooted(candidate))
        {
            error = $"Service descriptor '{ServiceDescriptorFileName}' PreservePaths entry '{rawPath}' must be relative.";
            return false;
        }

        var candidatePath = candidate.TrimEnd(Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(candidatePath)
            || string.Equals(candidatePath, ".", StringComparison.Ordinal)
            || candidatePath.Split(Path.DirectorySeparatorChar).Any(static segment => string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            error = $"Service descriptor '{ServiceDescriptorFileName}' PreservePaths entry '{rawPath}' is invalid.";
            return false;
        }

        normalizedPath = candidatePath;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Updates bundled service-host binary when the tool-shipped host is newer.
    /// </summary>
    /// <param name="runtimeDirectory">Service runtime directory.</param>
    /// <param name="backupDirectory">Backup directory for replaced host binary.</param>
    /// <param name="error">Update error details.</param>
    /// <param name="updated">True when host binary was replaced.</param>
    /// <returns>True when host check/update succeeds.</returns>
    private static bool TryUpdateBundledServiceHostIfNewer(string runtimeDirectory, string backupDirectory, out string error, out bool updated)
    {
        updated = false;
        if (!TryResolveServiceHostUpdatePaths(runtimeDirectory, out var sourceHostPath, out var targetHostPath, out error))
        {
            return false;
        }

        if (!File.Exists(targetHostPath))
        {
            return TryCopyServiceHostBinary(sourceHostPath, targetHostPath, out error, out updated);
        }

        if (!ShouldReplaceBundledServiceHostBinary(sourceHostPath, targetHostPath))
        {
            updated = false;
            return true;
        }

        return TryBackupAndReplaceServiceHostBinary(sourceHostPath, targetHostPath, backupDirectory, out error, out updated);
    }

    /// <summary>
    /// Resolves source and target service-host paths used by runtime host update operations.
    /// </summary>
    /// <param name="runtimeDirectory">Service runtime directory.</param>
    /// <param name="sourceHostPath">Tool-distributed host executable path.</param>
    /// <param name="targetHostPath">Installed runtime host executable path.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when path resolution succeeds.</returns>
    private static bool TryResolveServiceHostUpdatePaths(
        string runtimeDirectory,
        out string sourceHostPath,
        out string targetHostPath,
        out string error)
    {
        targetHostPath = string.Empty;
        error = string.Empty;

        if (!TryResolveDedicatedServiceHostExecutableFromToolDistribution(out sourceHostPath))
        {
            error = "Unable to resolve bundled service-host from Kestrun.Tool distribution.";
            return false;
        }

        targetHostPath = Path.Combine(runtimeDirectory, Path.GetFileName(sourceHostPath));
        return true;
    }

    /// <summary>
    /// Copies a service-host executable to the target runtime path and applies Unix execute permissions when required.
    /// </summary>
    /// <param name="sourceHostPath">Source host executable path.</param>
    /// <param name="targetHostPath">Target runtime host executable path.</param>
    /// <param name="error">Copy error details.</param>
    /// <param name="updated">True when copy succeeds.</param>
    /// <returns>True when copy succeeds.</returns>
    private static bool TryCopyServiceHostBinary(string sourceHostPath, string targetHostPath, out string error, out bool updated)
    {
        error = string.Empty;
        updated = false;

        try
        {
            File.Copy(sourceHostPath, targetHostPath, overwrite: true);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                TryEnsureServiceRuntimeExecutablePermissions(targetHostPath);
            }

            updated = true;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to update bundled service-host: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Determines whether the bundled host binary should replace the installed runtime host binary.
    /// </summary>
    /// <param name="sourceHostPath">Tool-distributed host executable path.</param>
    /// <param name="targetHostPath">Installed runtime host executable path.</param>
    /// <returns>True when replacement should occur.</returns>
    private static bool ShouldReplaceBundledServiceHostBinary(string sourceHostPath, string targetHostPath)
    {
        var hasSourceVersion = TryReadFileVersion(sourceHostPath, out var sourceVersion) && sourceVersion is not null;
        var hasTargetVersion = TryReadFileVersion(targetHostPath, out var targetVersion) && targetVersion is not null;

        return !hasSourceVersion || !hasTargetVersion || sourceVersion > targetVersion;
    }

    /// <summary>
    /// Backs up the installed runtime host binary and replaces it with the bundled host binary.
    /// </summary>
    /// <param name="sourceHostPath">Tool-distributed host executable path.</param>
    /// <param name="targetHostPath">Installed runtime host executable path.</param>
    /// <param name="backupDirectory">Backup directory for the previous runtime host binary.</param>
    /// <param name="error">Replacement error details.</param>
    /// <param name="updated">True when replacement succeeds.</param>
    /// <returns>True when backup and replacement succeed.</returns>
    private static bool TryBackupAndReplaceServiceHostBinary(
        string sourceHostPath,
        string targetHostPath,
        string backupDirectory,
        out string error,
        out bool updated)
    {
        updated = false;

        try
        {
            _ = Directory.CreateDirectory(backupDirectory);
            File.Copy(targetHostPath, Path.Combine(backupDirectory, Path.GetFileName(targetHostPath)), overwrite: true);
        }
        catch (Exception ex)
        {
            error = $"Failed to update bundled service-host: {ex.Message}";
            return false;
        }

        return TryCopyServiceHostBinary(sourceHostPath, targetHostPath, out error, out updated);
    }

    /// <summary>
    /// Reads file version metadata from a binary file.
    /// </summary>
    /// <param name="filePath">Binary file path.</param>
    /// <param name="version">Parsed version when available.</param>
    /// <returns>True when version parsing succeeds.</returns>
    private static bool TryReadFileVersion(string filePath, out Version? version)
    {
        version = null;
        try
        {
            var fileVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(filePath);
            if (!string.IsNullOrWhiteSpace(fileVersionInfo.FileVersion)
                && Version.TryParse(fileVersionInfo.FileVersion, out var parsedFileVersion)
                && parsedFileVersion is not null)
            {
                version = parsedFileVersion;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(fileVersionInfo.ProductVersion)
                && Version.TryParse(fileVersionInfo.ProductVersion, out var parsedProductVersion)
                && parsedProductVersion is not null)
            {
                version = parsedProductVersion;
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
    /// Resolves an installed service bundle path by service name across deployment roots.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="deploymentRootOverride">Optional deployment root override.</param>
    /// <param name="serviceRootPath">Resolved service bundle root path.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when a matching installed service bundle is found.</returns>
    private static bool TryResolveInstalledServiceBundleRoot(string serviceName, string? deploymentRootOverride, out string serviceRootPath, out string error)
    {
        serviceRootPath = string.Empty;
        error = string.Empty;

        var serviceDirectoryName = GetServiceDeploymentDirectoryName(serviceName);
        var candidateRoots = new List<string>();
        if (!string.IsNullOrWhiteSpace(deploymentRootOverride))
        {
            candidateRoots.Add(deploymentRootOverride);
        }

        candidateRoots.AddRange(GetServiceDeploymentRootCandidates());

        foreach (var root in candidateRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var serviceBaseRoot = Path.Combine(root, serviceDirectoryName);
            if (!Directory.Exists(serviceBaseRoot))
            {
                continue;
            }

            var directDescriptorPath = Path.Combine(serviceBaseRoot, ServiceBundleScriptDirectoryName, ServiceDescriptorFileName);
            if (File.Exists(directDescriptorPath))
            {
                serviceRootPath = Path.GetFullPath(serviceBaseRoot);
                return true;
            }
        }

        error = $"Installed service bundle not found for '{serviceName}'.";
        return false;
    }

    /// <summary>
    /// Starts a Windows service using sc.exe.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <returns>Process exit code.</returns>
    private static ServiceControlResult StartWindowsService(string serviceName, string? configuredLogPath, bool rawOutput)
    {
        var result = RunProcess("sc.exe", ["start", serviceName], writeStandardOutput: false);
        if (result.ExitCode != 0)
        {
            WriteServiceOperationLog(
                $"operation='start' service='{serviceName}' platform='windows' result='failed' exitCode={result.ExitCode} error='{result.Error.Trim()}'",
                configuredLogPath,
                serviceName);
            return new ServiceControlResult("start", serviceName, "windows", "unknown", null, result.ExitCode, "Failed to start service.", result.Output, result.Error);
        }

        WriteServiceOperationLog(
            $"operation='start' service='{serviceName}' platform='windows' result='success' exitCode=0",
            configuredLogPath,
            serviceName);
        return new ServiceControlResult("start", serviceName, "windows", "running", null, 0, "Service started.", result.Output, result.Error);
    }

    /// <summary>
    /// Stops a Windows service using sc.exe.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <returns>Process exit code.</returns>
    private static ServiceControlResult StopWindowsService(string serviceName, string? configuredLogPath, bool rawOutput)
    {
        var result = RunProcess("sc.exe", ["stop", serviceName], writeStandardOutput: false);
        if (result.ExitCode != 0)
        {
            if (IsWindowsServiceAlreadyStopped(result))
            {
                WriteServiceOperationLog(
                    $"operation='stop' service='{serviceName}' platform='windows' result='success' exitCode=0 note='already stopped'",
                    configuredLogPath,
                    serviceName);
                return new ServiceControlResult("stop", serviceName, "windows", "stopped", null, 0, "Service is already stopped.", result.Output, result.Error);
            }

            WriteServiceOperationLog(
                $"operation='stop' service='{serviceName}' platform='windows' result='failed' exitCode={result.ExitCode} error='{result.Error.Trim()}'",
                configuredLogPath,
                serviceName);
            return new ServiceControlResult("stop", serviceName, "windows", "unknown", null, result.ExitCode, "Failed to stop service.", result.Output, result.Error);
        }

        WriteServiceOperationLog(
            $"operation='stop' service='{serviceName}' platform='windows' result='success' exitCode=0",
            configuredLogPath,
            serviceName);
        return new ServiceControlResult("stop", serviceName, "windows", "stopped", null, 0, "Service stopped.", result.Output, result.Error);
    }

    /// <summary>
    /// Returns true when SCM stop command indicates the service was not running.
    /// </summary>
    /// <param name="result">SCM command result.</param>
    /// <returns>True when SCM returned service-not-started semantics.</returns>
    private static bool IsWindowsServiceAlreadyStopped(ProcessResult result)
    {
        var text = $"{result.Output}\n{result.Error}";
        return text.Contains("1062", StringComparison.OrdinalIgnoreCase)
            || text.Contains("has not been started", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not started", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Queries a Windows service using sc.exe.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <returns>Process exit code.</returns>
    private static ServiceControlResult QueryWindowsService(string serviceName, string? configuredLogPath, bool rawOutput)
    {
        var result = RunProcess("sc.exe", ["queryex", serviceName], writeStandardOutput: false);
        if (result.ExitCode != 0)
        {
            WriteServiceOperationLog(
                $"operation='query' service='{serviceName}' platform='windows' result='failed' exitCode={result.ExitCode} error='{result.Error.Trim()}'",
                configuredLogPath,
                serviceName);
            return new ServiceControlResult("query", serviceName, "windows", "unknown", null, result.ExitCode, "Failed to query service.", result.Output, result.Error);
        }

        var stateLine = result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(static line => line.Contains("STATE", StringComparison.OrdinalIgnoreCase)) ?? "STATE: unknown";
        var state = stateLine.Contains("RUNNING", StringComparison.OrdinalIgnoreCase)
            ? "running"
            : stateLine.Contains("STOPPED", StringComparison.OrdinalIgnoreCase)
                ? "stopped"
                : "unknown";
        var pid = TryExtractWindowsServicePid(result.Output);

        WriteServiceOperationLog(
            $"operation='query' service='{serviceName}' platform='windows' result='success' exitCode=0 state='{stateLine}'",
            configuredLogPath,
            serviceName);

        return new ServiceControlResult("query", serviceName, "windows", state, pid, 0, stateLine, result.Output, result.Error);
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
    private static ServiceControlResult StartLinuxUserDaemon(string serviceName, string? configuredLogPath, bool rawOutput)
    {
        var useSystemScope = IsLinuxSystemUnitInstalled(serviceName);
        var unitName = GetLinuxUnitName(serviceName);
        var result = RunLinuxSystemctl(useSystemScope, ["start", unitName], writeStandardOutput: false);
        if (result.ExitCode != 0)
        {
            WriteServiceOperationLog(
                $"operation='start' service='{serviceName}' platform='linux' result='failed' exitCode={result.ExitCode} error='{result.Error.Trim()}'",
                configuredLogPath,
                serviceName);
            return new ServiceControlResult("start", serviceName, "linux", "unknown", null, result.ExitCode, "Failed to start service.", result.Output, result.Error);
        }

        WriteServiceOperationResult("start", "linux", serviceName, 0, configuredLogPath);
        return new ServiceControlResult("start", serviceName, "linux", "running", null, 0, "Service started.", result.Output, result.Error);
    }

    /// <summary>
    /// Stops a Linux user-level systemd unit.
    /// </summary>
    /// <param name="serviceName">Unit base name.</param>
    /// <returns>Process exit code.</returns>
    private static ServiceControlResult StopLinuxUserDaemon(string serviceName, string? configuredLogPath, bool rawOutput)
    {
        var useSystemScope = IsLinuxSystemUnitInstalled(serviceName);
        var unitName = GetLinuxUnitName(serviceName);
        var result = RunLinuxSystemctl(useSystemScope, ["stop", unitName], writeStandardOutput: false);
        if (result.ExitCode != 0)
        {
            if (IsLinuxServiceAlreadyStopped(result))
            {
                WriteServiceOperationLog(
                    $"operation='stop' service='{serviceName}' platform='linux' result='success' exitCode=0 note='already stopped'",
                    configuredLogPath,
                    serviceName);
                return new ServiceControlResult("stop", serviceName, "linux", "stopped", null, 0, "Service is already stopped.", result.Output, result.Error);
            }

            WriteServiceOperationLog(
                $"operation='stop' service='{serviceName}' platform='linux' result='failed' exitCode={result.ExitCode} error='{result.Error.Trim()}'",
                configuredLogPath,
                serviceName);
            return new ServiceControlResult("stop", serviceName, "linux", "unknown", null, result.ExitCode, "Failed to stop service.", result.Output, result.Error);
        }

        WriteServiceOperationResult("stop", "linux", serviceName, 0, configuredLogPath);
        return new ServiceControlResult("stop", serviceName, "linux", "stopped", null, 0, "Service stopped.", result.Output, result.Error);
    }

    /// <summary>
    /// Returns true when systemctl stop indicates the unit is already inactive or absent.
    /// </summary>
    /// <param name="result">Systemctl command result.</param>
    /// <returns>True when stop semantics indicate no-op success.</returns>
    private static bool IsLinuxServiceAlreadyStopped(ProcessResult result)
    {
        var text = $"{result.Output}\n{result.Error}";
        return text.Contains("not loaded", StringComparison.OrdinalIgnoreCase)
            || text.Contains("inactive", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not running", StringComparison.OrdinalIgnoreCase)
            || text.Contains("could not be found", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Queries a Linux user-level systemd unit.
    /// </summary>
    /// <param name="serviceName">Unit base name.</param>
    /// <returns>Process exit code.</returns>
    private static ServiceControlResult QueryLinuxUserDaemon(string serviceName, string? configuredLogPath, bool rawOutput)
    {
        var useSystemScope = IsLinuxSystemUnitInstalled(serviceName);
        var unitName = GetLinuxUnitName(serviceName);
        var queryArgs = rawOutput ? (IReadOnlyList<string>)["status", unitName] : ["is-active", unitName];
        var result = RunLinuxSystemctl(useSystemScope, queryArgs, writeStandardOutput: false);
        if (result.ExitCode != 0)
        {
            WriteServiceOperationLog(
                $"operation='query' service='{serviceName}' platform='linux' result='failed' exitCode={result.ExitCode} error='{result.Error.Trim()}'",
                configuredLogPath,
                serviceName);
            return new ServiceControlResult("query", serviceName, "linux", "unknown", null, result.ExitCode, "Failed to query service.", result.Output, result.Error);
        }

        var normalizedOutput = result.Output.Trim();
        var state = normalizedOutput.StartsWith("active", StringComparison.OrdinalIgnoreCase) ? "running" : "unknown";
        var pid = TryQueryLinuxServicePid(useSystemScope, unitName);
        WriteServiceOperationResult("query", "linux", serviceName, 0, configuredLogPath);
        return new ServiceControlResult("query", serviceName, "linux", state, pid, 0, string.IsNullOrWhiteSpace(normalizedOutput) ? "Service queried." : normalizedOutput, result.Output, result.Error);
    }

    /// <summary>
    /// Runs systemctl in user or system scope.
    /// </summary>
    /// <param name="useSystemScope">True for system scope; false for user scope.</param>
    /// <param name="arguments">Arguments after optional scope switch.</param>
    /// <returns>Process execution result.</returns>
    private static ProcessResult RunLinuxSystemctl(bool useSystemScope, IReadOnlyList<string> arguments, bool writeStandardOutput = true)
    {
        return useSystemScope
            ? RunProcess("systemctl", arguments, writeStandardOutput)
            : RunProcess("systemctl", ["--user", .. arguments], writeStandardOutput);
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
    private static ServiceControlResult StartMacLaunchAgent(string serviceName, string? configuredLogPath, bool rawOutput)
    {
        var useSystemScope = IsMacSystemLaunchDaemonInstalled(serviceName);
        var agentDirectory = useSystemScope
            ? "/Library/LaunchDaemons"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
        var plistPath = Path.Combine(agentDirectory, $"{serviceName}.plist");
        if (!File.Exists(plistPath))
        {
            return new ServiceControlResult("start", serviceName, "macos", "unknown", null, 2, $"Launch agent plist not found: {plistPath}", string.Empty, string.Empty);
        }

        var result = useSystemScope
            ? RunProcess("launchctl", ["bootstrap", "system", plistPath], writeStandardOutput: false)
            : RunProcess("launchctl", ["load", "-w", plistPath], writeStandardOutput: false);
        if (result.ExitCode != 0)
        {
            WriteServiceOperationLog(
                $"operation='start' service='{serviceName}' platform='macos' result='failed' exitCode={result.ExitCode} error='{result.Error.Trim()}'",
                configuredLogPath,
                serviceName);
            return new ServiceControlResult("start", serviceName, "macos", "unknown", null, result.ExitCode, "Failed to start service.", result.Output, result.Error);
        }

        WriteServiceOperationResult("start", "macos", serviceName, 0, configuredLogPath);
        return new ServiceControlResult("start", serviceName, "macos", "running", null, 0, "Service started.", result.Output, result.Error);
    }

    /// <summary>
    /// Stops a macOS launch agent by unloading its plist.
    /// </summary>
    /// <param name="serviceName">Agent label.</param>
    /// <returns>Process exit code.</returns>
    private static ServiceControlResult StopMacLaunchAgent(string serviceName, string? configuredLogPath, bool rawOutput)
    {
        var useSystemScope = IsMacSystemLaunchDaemonInstalled(serviceName);
        var agentDirectory = useSystemScope
            ? "/Library/LaunchDaemons"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
        var plistPath = Path.Combine(agentDirectory, $"{serviceName}.plist");
        if (!File.Exists(plistPath))
        {
            return new ServiceControlResult("stop", serviceName, "macos", "unknown", null, 2, $"Launch agent plist not found: {plistPath}", string.Empty, string.Empty);
        }

        var result = useSystemScope
            ? RunProcess("launchctl", ["bootout", $"system/{serviceName}"], writeStandardOutput: false)
            : RunProcess("launchctl", ["unload", plistPath], writeStandardOutput: false);
        if (result.ExitCode != 0)
        {
            if (IsMacServiceAlreadyStopped(result))
            {
                WriteServiceOperationLog(
                    $"operation='stop' service='{serviceName}' platform='macos' result='success' exitCode=0 note='already stopped'",
                    configuredLogPath,
                    serviceName);
                return new ServiceControlResult("stop", serviceName, "macos", "stopped", null, 0, "Service is already stopped.", result.Output, result.Error);
            }

            WriteServiceOperationLog(
                $"operation='stop' service='{serviceName}' platform='macos' result='failed' exitCode={result.ExitCode} error='{result.Error.Trim()}'",
                configuredLogPath,
                serviceName);
            return new ServiceControlResult("stop", serviceName, "macos", "unknown", null, result.ExitCode, "Failed to stop service.", result.Output, result.Error);
        }

        WriteServiceOperationResult("stop", "macos", serviceName, 0, configuredLogPath);
        return new ServiceControlResult("stop", serviceName, "macos", "stopped", null, 0, "Service stopped.", result.Output, result.Error);
    }

    /// <summary>
    /// Returns true when launchctl stop semantics indicate service is not currently running.
    /// </summary>
    /// <param name="result">Launchctl command result.</param>
    /// <returns>True when stop is effectively a no-op success.</returns>
    private static bool IsMacServiceAlreadyStopped(ProcessResult result)
    {
        var text = $"{result.Output}\n{result.Error}";
        return text.Contains("Could not find specified service", StringComparison.OrdinalIgnoreCase)
            || text.Contains("No such process", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not loaded", StringComparison.OrdinalIgnoreCase)
            || text.Contains("service is not loaded", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Queries a macOS launch agent by label.
    /// </summary>
    /// <param name="serviceName">Agent label.</param>
    /// <returns>Process exit code.</returns>
    private static ServiceControlResult QueryMacLaunchAgent(string serviceName, string? configuredLogPath, bool rawOutput)
    {
        var useSystemScope = IsMacSystemLaunchDaemonInstalled(serviceName);
        var result = useSystemScope
            ? RunProcess("launchctl", ["print", $"system/{serviceName}"], writeStandardOutput: false)
            : RunProcess("launchctl", ["list", serviceName], writeStandardOutput: false);
        if (result.ExitCode != 0)
        {
            WriteServiceOperationLog(
                $"operation='query' service='{serviceName}' platform='macos' result='failed' exitCode={result.ExitCode} error='{result.Error.Trim()}'",
                configuredLogPath,
                serviceName);
            return new ServiceControlResult("query", serviceName, "macos", "unknown", null, result.ExitCode, "Failed to query service.", result.Output, result.Error);
        }

        var state = result.Output.Contains("\"PID\" =", StringComparison.OrdinalIgnoreCase)
            || result.Output.Contains("pid =", StringComparison.OrdinalIgnoreCase)
            ? "running"
            : "loaded";
        var pid = TryExtractMacServicePid(result.Output);

        WriteServiceOperationResult("query", "macos", serviceName, 0, configuredLogPath);
        return new ServiceControlResult("query", serviceName, "macos", state, pid, 0, "Service queried.", result.Output, result.Error);
    }

    /// <summary>
    /// Extracts a Windows service PID from sc.exe queryex output.
    /// </summary>
    /// <param name="output">Raw command output.</param>
    /// <returns>Parsed PID when available.</returns>
    private static int? TryExtractWindowsServicePid(string output)
    {
        var pidLine = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(static line => line.Contains("PID", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(pidLine))
        {
            return null;
        }

        var parts = pidLine.Split(':', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) && pid > 0
            ? pid
            : null;
    }

    /// <summary>
    /// Queries Linux MainPID for a systemd unit.
    /// </summary>
    /// <param name="useSystemScope">True for system scope; false for user scope.</param>
    /// <param name="unitName">Systemd unit name.</param>
    /// <returns>Main PID when available.</returns>
    private static int? TryQueryLinuxServicePid(bool useSystemScope, string unitName)
    {
        var pidResult = RunLinuxSystemctl(useSystemScope, ["show", "-p", "MainPID", "--value", unitName], writeStandardOutput: false);
        if (pidResult.ExitCode != 0)
        {
            return null;
        }

        var text = pidResult.Output.Trim();
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) && pid > 0
            ? pid
            : null;
    }

    /// <summary>
    /// Extracts a macOS launchd PID from launchctl output.
    /// </summary>
    /// <param name="output">Raw command output.</param>
    /// <returns>Parsed PID when available.</returns>
    private static int? TryExtractMacServicePid(string output)
    {
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (line.Contains("pid =", StringComparison.OrdinalIgnoreCase))
            {
                var value = line[(line.IndexOf('=') + 1)..].Trim().TrimEnd(';');
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) && pid > 0)
                {
                    return pid;
                }
            }

            var tokens = line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 0 && int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var listPid) && listPid > 0)
            {
                return listPid;
            }
        }

        return null;
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
    /// <param name="serviceVersion">Optional service version from descriptor metadata.</param>
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
        string? deploymentRootOverride = null,
        string? serviceVersion = null)
    {
        serviceBundle = null;
        error = string.Empty;

        if (!TryResolveServiceBundleContext(
                serviceName,
                sourceScriptPath,
                sourceModuleManifestPath,
                deploymentRootOverride,
                serviceVersion,
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
    /// <param name="serviceVersion">Optional service version from descriptor metadata.</param>
    /// <param name="context">Resolved bundle path context.</param>
    /// <param name="error">Error details when resolution fails.</param>
    /// <returns>True when context resolution succeeds.</returns>
    private static bool TryResolveServiceBundleContext(
        string serviceName,
        string sourceScriptPath,
        string sourceModuleManifestPath,
        string? deploymentRootOverride,
        string? serviceVersion,
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
