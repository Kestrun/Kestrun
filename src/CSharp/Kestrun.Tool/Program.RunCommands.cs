using System.Diagnostics;

namespace Kestrun.Tool;

internal static partial class Program
{
    /// <summary>
    /// Executes the target script by delegating to the dedicated service-host executable.
    /// </summary>
    /// <param name="scriptPath">Absolute path to the script to execute.</param>
    /// <param name="scriptArguments">Command-line arguments passed to the target script.</param>
    /// <param name="moduleManifestPath">Absolute path to Kestrun.psd1.</param>
    /// <returns>Process exit code.</returns>
    private static int ExecuteScriptViaServiceHost(string scriptPath, IReadOnlyList<string> scriptArguments, string moduleManifestPath)
    {
        if (!TryResolveDedicatedServiceHostExecutableFromToolDistribution(out var serviceHostExecutablePath))
        {
            Console.Error.WriteLine("Unable to locate dedicated service host for current RID in Kestrun.Tool distribution.");
            Console.Error.WriteLine("Expected 'kestrun-service/<rid>/(kestrun-service-host|kestrun-service-host.exe)'. Reinstall or update Kestrun.Tool.");
            return 1;
        }

        var runnerExecutablePath = ResolveCurrentProcessPathOrFallback(serviceHostExecutablePath);
        var hostArguments = BuildDedicatedServiceHostRunArguments(
            runnerExecutablePath,
            scriptPath,
            moduleManifestPath,
            scriptArguments,
            ShouldDiscoverPowerShellHomeForManifest(moduleManifestPath));

        return RunForegroundProcess(serviceHostExecutablePath, hostArguments);
    }

    /// <summary>
    /// Runs a child process in foreground mode, inheriting the current console handles.
    /// </summary>
    /// <param name="fileName">Executable to run.</param>
    /// <param name="arguments">Argument tokens.</param>
    /// <returns>Process exit code.</returns>
    private static int RunForegroundProcess(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            Console.Error.WriteLine($"Failed to start process: {fileName}");
            return 1;
        }

        process.WaitForExit();
        return process.ExitCode;
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
        parsedCommand = new ParsedCommand(CommandMode.Run, string.Empty, [], kestrunFolder, kestrunManifestPath, null, null, null, null, null, ModuleStorageScope.Local, false, null, null, null, null, null, false, []);
        error = string.Empty;

        var state = new RunParseState(startIndex, kestrunFolder, kestrunManifestPath);
        while (state.Index < args.Length)
        {
            var current = args[state.Index];
            if (TryCaptureRunScriptArguments(args, current, ref state))
            {
                break;
            }

            if (TryConsumeRunOption(args, current, ref state, out error))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    return false;
                }

                continue;
            }

            if (current.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unknown option: {current}";
                return false;
            }

            if (state.ScriptPathSet)
            {
                error = "Script arguments must be preceded by --arguments (or --).";
                return false;
            }

            state.ScriptPath = current;
            state.ScriptPathSet = true;
            state.Index += 1;
        }

        if (!state.ScriptPathSet)
        {
            // Default to ./server.ps1 when a script path is not explicitly provided.
            state.ScriptPath = DefaultScriptFileName;
        }

        parsedCommand = new ParsedCommand(CommandMode.Run, state.ScriptPath, state.ScriptArguments, state.KestrunFolder, state.KestrunManifestPath, null, null, null, null, null, ModuleStorageScope.Local, false, null, null, null, null, null, false, []);

        return true;
    }

    /// <summary>
    /// Captures script arguments when the explicit script argument separator is encountered.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="current">Current token being processed.</param>
    /// <param name="state">Mutable run command parse state.</param>
    /// <returns>True when the parser should stop consuming additional tokens.</returns>
    private static bool TryCaptureRunScriptArguments(string[] args, string current, ref RunParseState state)
    {
        if (current is not "--arguments" and not "--")
        {
            return false;
        }

        state.ScriptArguments = [.. args.Skip(state.Index + 1)];
        return true;
    }

    /// <summary>
    /// Consumes a supported run command option and updates the parse state.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="current">Current token being processed.</param>
    /// <param name="state">Mutable run command parse state.</param>
    /// <param name="error">Error message when option parsing fails.</param>
    /// <returns>True when the current token was handled as a supported option.</returns>
    private static bool TryConsumeRunOption(string[] args, string current, ref RunParseState state, out string error)
    {
        error = string.Empty;
        if (current is "--script")
        {
            return TryConsumeRunScriptOption(args, ref state, out error);
        }

        if (current is "--kestrun-folder" or "-k")
        {
            return TryConsumeRunKestrunFolderOption(args, ref state, out error);
        }

        if (current is "--kestrun-manifest" or "-m")
        {
            return TryConsumeRunKestrunManifestOption(args, ref state, out error);
        }
        // Add additional options here as else-if branches.
        return false;
    }

    /// <summary>
    /// Consumes the run command script path option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="state">Mutable run command parse state.</param>
    /// <param name="error">Error message when parsing fails.</param>
    /// <returns>True when the option was consumed.</returns>
    private static bool TryConsumeRunScriptOption(string[] args, ref RunParseState state, out string error)
    {
        error = string.Empty;
        if (state.ScriptPathSet)
        {
            error = "Script path was provided multiple times. Use either positional script path or --script once.";
            return true;
        }

        if (state.Index + 1 >= args.Length)
        {
            error = "Missing value for --script.";
            return true;
        }

        state.ScriptPath = args[state.Index + 1];
        state.ScriptPathSet = true;
        state.Index += 2;
        return true;
    }

    /// <summary>
    /// Consumes the run command Kestrun folder option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="state">Mutable run command parse state.</param>
    /// <param name="error">Error message when parsing fails.</param>
    /// <returns>True when the option was consumed.</returns>
    private static bool TryConsumeRunKestrunFolderOption(string[] args, ref RunParseState state, out string error)
    {
        if (state.Index + 1 >= args.Length)
        {
            error = "Missing value for --kestrun-folder.";
            return true;
        }

        state.KestrunFolder = args[state.Index + 1];
        state.Index += 2;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Consumes the run command Kestrun manifest option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="state">Mutable run command parse state.</param>
    /// <param name="error">Error message when parsing fails.</param>
    /// <returns>True when the option was consumed.</returns>
    private static bool TryConsumeRunKestrunManifestOption(string[] args, ref RunParseState state, out string error)
    {
        if (state.Index + 1 >= args.Length)
        {
            error = "Missing value for --kestrun-manifest.";
            return true;
        }

        state.KestrunManifestPath = args[state.Index + 1];
        state.Index += 2;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Holds mutable state while parsing run command arguments.
    /// </summary>
    /// <remarks>
    /// Initializes a new parse-state instance.
    /// </remarks>
    /// <param name="index">Current parser index.</param>
    /// <param name="kestrunFolder">Optional folder containing Kestrun.psd1.</param>
    /// <param name="kestrunManifestPath">Optional explicit path to Kestrun.psd1.</param>
    private sealed class RunParseState(int index, string? kestrunFolder, string? kestrunManifestPath)
    {
        /// <summary>
        /// Gets or sets the current parser index.
        /// </summary>
        public int Index { get; set; } = index;

        /// <summary>
        /// Gets or sets the optional folder containing Kestrun.psd1.
        /// </summary>
        public string? KestrunFolder { get; set; } = kestrunFolder;

        /// <summary>
        /// Gets or sets the optional explicit path to Kestrun.psd1.
        /// </summary>
        public string? KestrunManifestPath { get; set; } = kestrunManifestPath;

        /// <summary>
        /// Gets or sets the resolved script path token.
        /// </summary>
        public string ScriptPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether a script path was provided explicitly.
        /// </summary>
        public bool ScriptPathSet { get; set; }

        /// <summary>
        /// Gets or sets script arguments while ensuring a non-null array.
        /// </summary>
        public string[] ScriptArguments { get; set; } = [];
    }
}
