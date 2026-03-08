using System.Diagnostics;
using System.Management.Automation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Kestrun.Runner;

/// <summary>
/// Provides shared runtime and process helpers for Kestrun script runner hosts.
/// </summary>
public static class RunnerRuntime
{
    private static readonly Lock AssemblyLoadSync = new();
    private static string? s_kestrunModuleLibPath;
    private static bool s_dependencyResolverRegistered;

    /// <summary>
    /// Ensures the runner is executing on .NET 10.
    /// </summary>
    /// <param name="productName">Product name used in the exception message.</param>
    public static void EnsureNet10Runtime(string productName)
    {
        var framework = RuntimeInformation.FrameworkDescription;
        if (!framework.Contains(".NET 10", StringComparison.OrdinalIgnoreCase))
        {
            throw new RuntimeException($"{productName} requires .NET 10 runtime. Current runtime: {framework}");
        }
    }

    /// <summary>
    /// Ensures Kestrun.dll from the selected module root is loaded into the default context.
    /// </summary>
    /// <param name="moduleManifestPath">Absolute path to Kestrun.psd1.</param>
    public static void EnsureKestrunAssemblyPreloaded(string moduleManifestPath)
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
                var loadedPath = string.IsNullOrWhiteSpace(alreadyLoaded.Location)
                    ? string.Empty
                    : Path.GetFullPath(alreadyLoaded.Location);
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
    /// Ensures PowerShell built-in modules are discoverable for embedded runspace execution.
    /// </summary>
    /// <param name="createFallbackDirectories">When true, creates a writable fallback PSHOME and module folder if no installation is discovered.</param>
    public static void EnsurePowerShellRuntimeHome(bool createFallbackDirectories)
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

        if (!createFallbackDirectories)
        {
            return;
        }

        var fallbackPsHome = GetFallbackPowerShellHomePath();
        TryEnsureDirectory(fallbackPsHome);

        var modulesPath = Path.Combine(fallbackPsHome, "Modules");
        TryEnsureDirectory(modulesPath);

        Environment.SetEnvironmentVariable("PSHOME", fallbackPsHome);
        EnsurePsModulePathContains(modulesPath);
    }

    /// <summary>
    /// Verifies that the loaded Kestrun assembly contains the expected host manager type.
    /// </summary>
    /// <returns>True when the expected Kestrun host manager type is available.</returns>
    public static bool HasKestrunHostManagerType()
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
    public static async Task RequestManagedStopAsync()
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
    /// Resolves a bootstrap log path from an optional configured path and default file name.
    /// </summary>
    /// <param name="configuredPath">Configured file or directory path.</param>
    /// <param name="defaultFileName">Default log file name when no path is configured.</param>
    /// <returns>Resolved absolute log file path.</returns>
    public static string ResolveBootstrapLogPath(string? configuredPath, string defaultFileName)
    {
        var defaultDirectory = GetDefaultBootstrapLogDirectory();
        var defaultPath = Path.Combine(defaultDirectory, defaultFileName);

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return defaultPath;
        }

        var fullPath = Path.GetFullPath(configuredPath);
        return Directory.Exists(fullPath)
            || configuredPath.EndsWith('\\')
            || configuredPath.EndsWith('/')
            ? Path.Combine(fullPath, defaultFileName)
            : fullPath;
    }

    /// <summary>
    /// Returns the default bootstrap log directory for the current platform.
    /// </summary>
    /// <returns>Writable preferred log directory path.</returns>
    private static string GetDefaultBootstrapLogDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Kestrun",
                "logs");
        }

        if (OperatingSystem.IsLinux())
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, ".local", "share", "kestrun", "logs");
        }

        if (OperatingSystem.IsMacOS())
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "Library", "Application Support", "Kestrun", "logs");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kestrun",
            "logs");
    }

    /// <summary>
    /// Dispatches non-empty PowerShell pipeline output text to a caller-provided sink.
    /// </summary>
    /// <param name="output">PowerShell pipeline output collection.</param>
    /// <param name="onOutput">Callback invoked for each selected output line.</param>
    /// <param name="skipWhitespace">When true, whitespace-only values are ignored.</param>
    public static void DispatchPowerShellOutput(IEnumerable<PSObject> output, Action<string> onOutput, bool skipWhitespace)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(onOutput);

        foreach (var item in output)
        {
            if (item is null)
            {
                continue;
            }

            var value = item.BaseObject?.ToString() ?? item.ToString();
            if (skipWhitespace && string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            onOutput(value ?? string.Empty);
        }
    }

    /// <summary>
    /// Dispatches PowerShell non-output streams to caller-provided handlers.
    /// </summary>
    /// <param name="streams">PowerShell data streams.</param>
    /// <param name="onWarning">Optional warning message handler.</param>
    /// <param name="onVerbose">Optional verbose message handler.</param>
    /// <param name="onDebug">Optional debug message handler.</param>
    /// <param name="onInformation">Optional information message handler.</param>
    /// <param name="onError">Optional error message handler.</param>
    /// <param name="skipWhitespace">When true, whitespace-only values are ignored.</param>
    public static void DispatchPowerShellStreams(
        PSDataStreams streams,
        Action<string>? onWarning,
        Action<string>? onVerbose,
        Action<string>? onDebug,
        Action<string>? onInformation,
        Action<string>? onError,
        bool skipWhitespace)
    {
        ArgumentNullException.ThrowIfNull(streams);

        DispatchMessages(streams.Warning, static record => record.Message, onWarning, skipWhitespace);
        DispatchMessages(streams.Verbose, static record => record.Message, onVerbose, skipWhitespace);
        DispatchMessages(streams.Debug, static record => record.Message, onDebug, skipWhitespace);
        DispatchMessages(streams.Information, static record => record.MessageData?.ToString() ?? record.ToString(), onInformation, skipWhitespace);
        DispatchMessages(streams.Error, static record => record.ToString(), onError, skipWhitespace);
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
    /// Dispatches formatted stream records through an optional callback.
    /// </summary>
    /// <typeparam name="TRecord">PowerShell stream record type.</typeparam>
    /// <param name="records">Record sequence.</param>
    /// <param name="formatter">Record-to-message formatter.</param>
    /// <param name="callback">Optional callback invoked for each message.</param>
    /// <param name="skipWhitespace">When true, whitespace-only values are ignored.</param>
    private static void DispatchMessages<TRecord>(
        IEnumerable<TRecord> records,
        Func<TRecord, string?> formatter,
        Action<string>? callback,
        bool skipWhitespace)
    {
        if (callback is null)
        {
            return;
        }

        foreach (var record in records)
        {
            var message = formatter(record);
            if (skipWhitespace && string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            callback(message ?? string.Empty);
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

        var envPsHome = Environment.GetEnvironmentVariable("PSHOME");
        if (!string.IsNullOrWhiteSpace(envPsHome))
        {
            candidates.Add(Path.GetFullPath(envPsHome));
        }

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                candidates.Add(Path.Combine(programFiles, "PowerShell", "7"));
                candidates.Add(Path.Combine(programFiles, "PowerShell", "7-preview"));
            }

            var whereResult = RunProcessCapture("where.exe", ["pwsh"]);
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

            var whichResult = RunProcessCapture("which", ["pwsh"]);
            if (whichResult.ExitCode == 0)
            {
                var discovered = whichResult.Output
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                candidates.AddRange(discovered);
            }

            candidates = [
                .. candidates.Select(path =>
                    path.EndsWith("pwsh", StringComparison.OrdinalIgnoreCase)
                        ? Path.GetDirectoryName(path) ?? path
                        : path)
            ];
        }

        return candidates
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns a writable fallback PSHOME location based on operating system.
    /// </summary>
    /// <returns>Fallback PSHOME absolute path.</returns>
    private static string GetFallbackPowerShellHomePath()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "PowerShell", "7");
        }

        if (OperatingSystem.IsLinux())
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, ".local", "share", "powershell", "7");
        }

        if (OperatingSystem.IsMacOS())
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "Library", "Application Support", "powershell", "7");
        }

        var localFallback = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localFallback, "powershell", "7");
    }

    /// <summary>
    /// Ensures a directory exists without throwing.
    /// </summary>
    /// <param name="path">Directory path to create.</param>
    private static void TryEnsureDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            _ = Directory.CreateDirectory(path);
        }
        catch
        {
            // Best-effort bootstrap path creation.
        }
    }

    /// <summary>
    /// Runs a process and captures output for diagnostics.
    /// </summary>
    /// <param name="fileName">Executable to run.</param>
    /// <param name="arguments">Argument tokens.</param>
    /// <returns>Process result data.</returns>
    private static ProcessResult RunProcessCapture(string fileName, IReadOnlyList<string> arguments)
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
}
