using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.PowerShell;

namespace Kestrun.ScriptRunner;

internal static class Program
{
    private const string ModuleManifestFileName = "Kestrun.psd1";
    private const string ModuleName = "Kestrun";
    private const string DefaultScriptFileName = "server.ps1";

    private static int Main(string[] args)
    {
        if (ShouldShowHelp(args))
        {
            PrintUsage();
            return 0;
        }

        if (!TryParseArguments(args, out var scriptPath, out var scriptArguments, out var kestrunFolder, out var parseError))
        {
            Console.Error.WriteLine(parseError);
            PrintUsage();
            return 2;
        }

        var fullScriptPath = Path.GetFullPath(scriptPath);
        if (!File.Exists(fullScriptPath))
        {
            Console.Error.WriteLine($"Script file not found: {fullScriptPath}");
            return 2;
        }

        var moduleManifestPath = LocateModuleManifest(kestrunFolder);
        if (moduleManifestPath is null)
        {
            if (!string.IsNullOrWhiteSpace(kestrunFolder))
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
    /// Executes the target script in a runspace that has Kestrun imported by manifest path.
    /// </summary>
    /// <param name="scriptPath">Absolute path to the script to execute.</param>
    /// <param name="scriptArguments">Command-line arguments passed to the target script.</param>
    /// <param name="moduleManifestPath">Absolute path to Kestrun.psd1.</param>
    /// <returns>Process exit code.</returns>
    private static int ExecuteScript(string scriptPath, IReadOnlyList<string> scriptArguments, string moduleManifestPath)
    {
        var sessionState = InitialSessionState.CreateDefault2();
        if (OperatingSystem.IsWindows())
        {
            sessionState.ExecutionPolicy = ExecutionPolicy.Unrestricted;
        }

        sessionState.ImportPSModule([moduleManifestPath]);

        using var runspace = RunspaceFactory.CreateRunspace(sessionState);
        runspace.Open();

        runspace.SessionStateProxy.SetVariable("__krRunnerScriptPath", scriptPath);
        runspace.SessionStateProxy.SetVariable("__krRunnerScriptArgs", scriptArguments.ToArray());
        runspace.SessionStateProxy.SetVariable("__krRunnerQuiet", true);

        using var powershell = PowerShell.Create();
        powershell.Runspace = runspace;
        // Dot-source the script into the current scope so function metadata used by OpenAPI discovery remains visible.
        _ = powershell.AddScript(". $__krRunnerScriptPath @__krRunnerScriptArgs", useLocalScope: false);

        var output = powershell.Invoke();

        WriteOutput(output);
        WriteStreams(powershell.Streams);

        return powershell.HadErrors ? 1 : 0;
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
    /// Tries to parse command-line arguments into script path and script arguments.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="scriptPath">Resolved script path argument.</param>
    /// <param name="scriptArguments">Remaining script arguments.</param>
    /// <param name="kestrunFolder">Optional folder containing Kestrun.psd1.</param>
    /// <param name="error">Error message when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private static bool TryParseArguments(string[] args, out string scriptPath, out string[] scriptArguments, out string? kestrunFolder, out string error)
    {
        scriptPath = string.Empty;
        scriptArguments = [];
        kestrunFolder = null;
        error = string.Empty;

        var scriptPathSet = false;
        var index = 0;
        while (index < args.Length)
        {
            var current = args[index];
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

            if (current is "--arguments" or "--")
            {
                scriptArguments = [.. args.Skip(index + 1)];
                index = args.Length;
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

        return true;
    }

    /// <summary>
    /// Determines whether the command-line input asks for usage help.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <returns>True when help should be displayed.</returns>
    private static bool ShouldShowHelp(string[] args)
    {
        if (!args.Any(IsHelpToken))
        {
            return false;
        }

        var filtered = new List<string>(args.Length);
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] is "--kestrun-folder" or "-k")
            {
                index += 1;
                continue;
            }

            filtered.Add(args[index]);
        }

        return filtered.Count == 1 && IsHelpToken(filtered[0]);
    }

    /// <summary>
    /// Checks whether an argument token requests usage help.
    /// </summary>
    /// <param name="token">Command-line token to inspect.</param>
    /// <returns>True when the token is a help switch.</returns>
    private static bool IsHelpToken(string token) => token is "-h" or "--help" or "/?";

    /// <summary>
    /// Prints command usage and discovery hints.
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine("Usage: krun [--kestrun-folder <folder>] [<main.ps1>] [--arguments <script arguments...>]");
        Console.WriteLine("Runs a PowerShell script after importing Kestrun.psd1 into the runspace.");
        Console.WriteLine("If no script is provided, ./server.ps1 is used.");
        Console.WriteLine("Script arguments must be passed after --arguments (or --).");
        Console.WriteLine("If --kestrun-folder is omitted, Kestrun.psd1 is searched under the executable folder and then PSModulePath.");
    }

    /// <summary>
    /// Locates Kestrun.psd1 without launching an external pwsh process.
    /// </summary>
    /// <param name="kestrunFolder">Optional folder containing Kestrun.psd1.</param>
    /// <returns>Absolute manifest path when found; otherwise null.</returns>
    private static string? LocateModuleManifest(string? kestrunFolder)
    {
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
