namespace Kestrun.Tool;

internal static partial class Program
{
    private sealed class ServiceParseState
    {
        public string ServiceName { get; set; } = string.Empty;

        public string ScriptPath { get; set; } = string.Empty;

        public bool ScriptPathSet { get; set; }

        public string[] ScriptArguments { get; set; } = [];

        public string? ServiceLogPath { get; set; }

        public string? ServiceUser { get; set; }

        public string? ServicePassword { get; set; }

        public string? ServiceContentRoot { get; set; }

        public string? ServiceDeploymentRoot { get; set; }
    }

    private sealed class ServiceRegisterParseState
    {
        public string ServiceName { get; set; } = string.Empty;

        public string ServiceHostExecutablePath { get; set; } = string.Empty;

        public string RunnerExecutablePath { get; set; } = string.Empty;

        public string ScriptPath { get; set; } = string.Empty;

        public string ModuleManifestPath { get; set; } = string.Empty;

        public string[] ScriptArguments { get; set; } = [];

        public string? ServiceLogPath { get; set; }

        public string? ServiceUser { get; set; }

        public string? ServicePassword { get; set; }
    }

    /// <summary>
    /// Parses arguments for service install/remove/start/stop/query commands.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="startIndex">Index after service token.</param>
    /// <param name="kestrunFolder">Optional folder containing Kestrun.psd1.</param>
    /// <param name="kestrunManifestPath">Optional explicit path to Kestrun.psd1.</param>
    /// <param name="parsedCommand">Parsed command payload.</param>
    /// <param name="error">Error message when parsing fails.</param>
    /// <returns>True when parsing succeeds.</returns>
    private static bool TryParseServiceArguments(string[] args, int startIndex, string? kestrunFolder, string? kestrunManifestPath, out ParsedCommand parsedCommand, out string error)
    {
        parsedCommand = CreateDefaultServiceParsedCommand(kestrunFolder, kestrunManifestPath);
        if (!TryResolveServiceMode(args, startIndex, out var mode, out error))
        {
            return false;
        }

        var state = new ServiceParseState();
        if (!TryParseServiceOptionLoop(args, mode, state, startIndex + 1, ref kestrunFolder, ref kestrunManifestPath, out error))
        {
            return false;
        }

        if (!TryValidateServiceParseState(mode, state, out error))
        {
            return false;
        }

        parsedCommand = CreateServiceParsedCommand(mode, state, kestrunFolder, kestrunManifestPath);
        return true;
    }

    /// <summary>
    /// Creates the default parsed command placeholder for service command parsing.
    /// </summary>
    /// <param name="kestrunFolder">Optional folder containing Kestrun.psd1.</param>
    /// <param name="kestrunManifestPath">Optional explicit path to Kestrun.psd1.</param>
    /// <returns>Default parsed command for service mode.</returns>
    private static ParsedCommand CreateDefaultServiceParsedCommand(string? kestrunFolder, string? kestrunManifestPath)
        => new(CommandMode.ServiceInstall, string.Empty, [], kestrunFolder, kestrunManifestPath, null, null, null, null, null, ModuleStorageScope.Local, false, null, null);

    /// <summary>
    /// Validates service token bounds and resolves command mode.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="startIndex">Index after service token.</param>
    /// <param name="mode">Resolved service mode.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when a service mode is resolved.</returns>
    private static bool TryResolveServiceMode(string[] args, int startIndex, out CommandMode mode, out string error)
    {
        mode = CommandMode.Run;
        if (startIndex >= args.Length)
        {
            error = "Missing service action. Use 'service install', 'service remove', 'service start', 'service stop', or 'service query'.";
            return false;
        }

        return TryParseServiceMode(args[startIndex], out mode, out error);
    }

    /// <summary>
    /// Parses all option and positional tokens for service commands.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="startIndex">First token index after service action.</param>
    /// <param name="kestrunFolder">Optional folder override.</param>
    /// <param name="kestrunManifestPath">Optional manifest override.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when option parsing succeeds.</returns>
    private static bool TryParseServiceOptionLoop(
        string[] args,
        CommandMode mode,
        ServiceParseState state,
        int startIndex,
        ref string? kestrunFolder,
        ref string? kestrunManifestPath,
        out string error)
    {
        error = string.Empty;
        var index = startIndex;
        while (index < args.Length)
        {
            if (TryConsumeServiceOption(args, mode, state, ref index, ref kestrunFolder, ref kestrunManifestPath, out error))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            var current = args[index];
            if (mode == CommandMode.ServiceInstall && (current is "--arguments" or "--"))
            {
                state.ScriptArguments = [.. args.Skip(index + 1)];
                break;
            }

            if (!TryConsumeServicePositionalScript(current, mode, state, out error))
            {
                return false;
            }

            index += 1;
        }

        return true;
    }

    /// <summary>
    /// Creates the final parsed command from service parse state.
    /// </summary>
    /// <param name="mode">Resolved service mode.</param>
    /// <param name="state">Completed parse state.</param>
    /// <param name="kestrunFolder">Optional folder containing Kestrun.psd1.</param>
    /// <param name="kestrunManifestPath">Optional explicit path to Kestrun.psd1.</param>
    /// <returns>Parsed command payload.</returns>
    private static ParsedCommand CreateServiceParsedCommand(CommandMode mode, ServiceParseState state, string? kestrunFolder, string? kestrunManifestPath)
        => new(
            mode,
            state.ScriptPath,
            state.ScriptArguments,
            kestrunFolder,
            kestrunManifestPath,
            state.ServiceName,
            state.ServiceLogPath,
            state.ServiceUser,
            state.ServicePassword,
            null,
            ModuleStorageScope.Local,
            false,
            state.ServiceContentRoot,
            state.ServiceDeploymentRoot);

    /// <summary>
    /// Parses the service action token into a concrete command mode.
    /// </summary>
    /// <param name="action">Service action token.</param>
    /// <param name="mode">Parsed service mode.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the action token is valid.</returns>
    private static bool TryParseServiceMode(string action, out CommandMode mode, out string error)
    {
        mode = action.ToLowerInvariant() switch
        {
            "install" => CommandMode.ServiceInstall,
            "remove" => CommandMode.ServiceRemove,
            "start" => CommandMode.ServiceStart,
            "stop" => CommandMode.ServiceStop,
            "query" => CommandMode.ServiceQuery,
            _ => CommandMode.Run,
        };

        if (mode != CommandMode.Run)
        {
            error = string.Empty;
            return true;
        }

        error = $"Unknown service action: {action}. Use 'service install', 'service remove', 'service start', 'service stop', or 'service query'.";
        return false;
    }

    /// <summary>
    /// Attempts to consume one named option in service argument parsing.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="index">Current argument index.</param>
    /// <param name="kestrunFolder">Optional folder override.</param>
    /// <param name="kestrunManifestPath">Optional manifest override.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when an option was consumed or handled.</returns>
    private static bool TryConsumeServiceOption(
        string[] args,
        CommandMode mode,
        ServiceParseState state,
        ref int index,
        ref string? kestrunFolder,
        ref string? kestrunManifestPath,
        out string error)
    {
        error = string.Empty;
        var current = args[index];

        if (current is "--script")
        {
            if (mode is CommandMode.ServiceRemove or CommandMode.ServiceStart or CommandMode.ServiceStop or CommandMode.ServiceQuery)
            {
                error = "Service remove/start/stop/query does not accept --script.";
                return true;
            }

            if (!TryConsumeOptionValue(args, ref index, "--script", out var value, out error))
            {
                return true;
            }

            if (state.ScriptPathSet)
            {
                error = "Script path was provided multiple times. Use either positional script path or --script once.";
                return true;
            }

            state.ScriptPath = value;
            state.ScriptPathSet = true;
            return true;
        }

        if (current is "--name" or "-n")
        {
            if (!TryConsumeOptionValue(args, ref index, "--name", out var value, out error))
            {
                return true;
            }

            state.ServiceName = value;
            return true;
        }

        if (current is "--kestrun-folder" or "-k")
        {
            if (!TryConsumeOptionValue(args, ref index, "--kestrun-folder", out var value, out error))
            {
                return true;
            }

            kestrunFolder = value;
            return true;
        }

        if (current is "--kestrun-manifest" or "-m")
        {
            if (!TryConsumeOptionValue(args, ref index, "--kestrun-manifest", out var value, out error))
            {
                return true;
            }

            kestrunManifestPath = value;
            return true;
        }

        if (current is "--service-log-path")
        {
            if (!TryConsumeOptionValue(args, ref index, "--service-log-path", out var value, out error))
            {
                return true;
            }

            state.ServiceLogPath = value;
            return true;
        }

        if (current is "--service-user")
        {
            if (mode is CommandMode.ServiceRemove or CommandMode.ServiceStart or CommandMode.ServiceStop or CommandMode.ServiceQuery)
            {
                error = "Service remove/start/stop/query does not accept --service-user.";
                return true;
            }

            if (!TryConsumeOptionValue(args, ref index, "--service-user", out var value, out error))
            {
                return true;
            }

            state.ServiceUser = value;
            return true;
        }

        if (current is "--service-password")
        {
            if (mode is CommandMode.ServiceRemove or CommandMode.ServiceStart or CommandMode.ServiceStop or CommandMode.ServiceQuery)
            {
                error = "Service remove/start/stop/query does not accept --service-password.";
                return true;
            }

            if (!TryConsumeOptionValue(args, ref index, "--service-password", out var value, out error))
            {
                return true;
            }

            state.ServicePassword = value;
            return true;
        }

        if (current is "--deployment-root")
        {
            if (mode is CommandMode.ServiceStart or CommandMode.ServiceStop or CommandMode.ServiceQuery)
            {
                error = "Service start/stop/query does not accept --deployment-root.";
                return true;
            }

            if (!TryConsumeOptionValue(args, ref index, "--deployment-root", out var value, out error))
            {
                return true;
            }

            state.ServiceDeploymentRoot = value;
            return true;
        }

        if (current is "--content-root")
        {
            if (mode is CommandMode.ServiceRemove or CommandMode.ServiceStart or CommandMode.ServiceStop or CommandMode.ServiceQuery)
            {
                error = "Service remove/start/stop/query does not accept --content-root.";
                return true;
            }

            if (!TryConsumeOptionValue(args, ref index, "--content-root", out var value, out error))
            {
                return true;
            }

            state.ServiceContentRoot = value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Consumes a single option value and advances the argument index.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="optionName">Option name for error reporting.</param>
    /// <param name="value">Parsed option value.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the option value was consumed.</returns>
    private static bool TryConsumeOptionValue(string[] args, ref int index, string optionName, out string value, out string error)
    {
        value = string.Empty;
        error = string.Empty;

        if (index + 1 >= args.Length)
        {
            error = $"Missing value for {optionName}.";
            return false;
        }

        value = args[index + 1];
        index += 2;
        return true;
    }

    /// <summary>
    /// Consumes the positional script path for service install mode.
    /// </summary>
    /// <param name="current">Current argument token.</param>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when parsing can continue.</returns>
    private static bool TryConsumeServicePositionalScript(string current, CommandMode mode, ServiceParseState state, out string error)
    {
        error = string.Empty;

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

        if (state.ScriptPathSet)
        {
            error = "Service install script arguments must be preceded by --arguments (or --).";
            return false;
        }

        state.ScriptPath = current;
        state.ScriptPathSet = true;
        return true;
    }

    /// <summary>
    /// Validates parsed service arguments and applies install defaults.
    /// </summary>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="error">Error text when validation fails.</param>
    /// <returns>True when validation succeeds.</returns>
    private static bool TryValidateServiceParseState(CommandMode mode, ServiceParseState state, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(state.ServiceName))
        {
            error = "Service name is required. Use --name <value>.";
            return false;
        }

        if (mode == CommandMode.ServiceInstall && !state.ScriptPathSet)
        {
            state.ScriptPath = DefaultScriptFileName;
        }

        if (mode != CommandMode.ServiceInstall && (!string.IsNullOrWhiteSpace(state.ServiceUser) || !string.IsNullOrWhiteSpace(state.ServicePassword)))
        {
            error = "Service user credentials are only supported for service install.";
            return false;
        }

        if (mode == CommandMode.ServiceInstall && string.IsNullOrWhiteSpace(state.ServiceUser) && !string.IsNullOrWhiteSpace(state.ServicePassword))
        {
            error = "--service-password requires --service-user.";
            return false;
        }

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

        var state = new ServiceRegisterParseState();
        if (!TryParseServiceRegisterOptionLoop(args, state, out error))
        {
            return false;
        }
        // Service registration mode is recognized. Validate required options and build immutable options.
        return TryBuildServiceRegisterOptions(state, out options, out error);
    }

    /// <summary>
    /// Parses service-register option tokens into a mutable parse state.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="state">Mutable parse state.</param>
    /// <param name="error">Parse error when an unknown option is encountered.</param>
    /// <returns>True when parsing succeeds.</returns>
    private static bool TryParseServiceRegisterOptionLoop(string[] args, ServiceRegisterParseState state, out string? error)
    {
        error = null;
        var index = 1;

        while (index < args.Length)
        {
            if (!TryConsumeServiceRegisterOption(args, state, ref index, out error))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Consumes one internal service-register option from the current parser index.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="state">Mutable parse state.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="error">Parse error when an unknown option is encountered.</param>
    /// <returns>True when parsing can continue.</returns>
    private static bool TryConsumeServiceRegisterOption(string[] args, ServiceRegisterParseState state, ref int index, out string? error)
    {
        error = null;
        var current = args[index];

        if (current is "--arguments" or "--")
        {
            state.ScriptArguments = [.. args.Skip(index + 1)];
            index = args.Length;
            return true;
        }

        if (!TryConsumeServiceRegisterOptionValue(args, ref index, current, out var value))
        {
            error = $"Unknown service register option: {current}";
            return false;
        }

        if (current is "--name")
        {
            state.ServiceName = value;
            return true;
        }

        if (current is "--service-host-exe")
        {
            state.ServiceHostExecutablePath = value;
            return true;
        }

        if (current is "--runner-exe")
        {
            state.RunnerExecutablePath = value;
            return true;
        }

        // Backward compatibility for older elevated registration invocations.
        if (current is "--exe")
        {
            state.ServiceHostExecutablePath = value;
            return true;
        }

        if (current is "--script")
        {
            state.ScriptPath = value;
            return true;
        }

        if (current is "--kestrun-manifest" or "-m")
        {
            state.ModuleManifestPath = value;
            return true;
        }

        if (current is "--service-log-path")
        {
            state.ServiceLogPath = value;
            return true;
        }

        if (current is "--service-user")
        {
            state.ServiceUser = value;
            return true;
        }

        if (current is "--service-password")
        {
            state.ServicePassword = value;
            return true;
        }

        error = $"Unknown service register option: {current}";
        return false;
    }

    /// <summary>
    /// Attempts to consume a single service-register option value.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="option">Current option token.</param>
    /// <param name="value">Consumed option value when available.</param>
    /// <returns>True when an option value pair is consumed.</returns>
    private static bool TryConsumeServiceRegisterOptionValue(string[] args, ref int index, string option, out string value)
    {
        value = string.Empty;

        if (option is not "--name"
            and not "--service-host-exe"
            and not "--runner-exe"
            and not "--exe"
            and not "--script"
            and not "--kestrun-manifest"
            and not "-m"
            and not "--service-log-path"
            and not "--service-user"
            and not "--service-password")
        {
            return false;
        }

        if (index + 1 >= args.Length)
        {
            return false;
        }

        value = args[index + 1];
        index += 2;
        return true;
    }

    /// <summary>
    /// Validates parsed service-register values and creates immutable registration options.
    /// </summary>
    /// <param name="state">Completed parse state.</param>
    /// <param name="options">Parsed registration options.</param>
    /// <param name="error">Validation error text.</param>
    /// <returns>True when validation succeeds.</returns>
    private static bool TryBuildServiceRegisterOptions(
        ServiceRegisterParseState state,
        out ServiceRegisterOptions? options,
        out string? error)
    {
        options = null;
        error = null;

        if (string.IsNullOrWhiteSpace(state.ServiceName))
        {
            error = "Missing --name for internal service registration mode.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(state.ServiceHostExecutablePath))
        {
            error = "Missing --service-host-exe for internal service registration mode.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(state.RunnerExecutablePath))
        {
            state.RunnerExecutablePath = state.ServiceHostExecutablePath;
        }

        if (string.IsNullOrWhiteSpace(state.ScriptPath))
        {
            error = "Missing --script for internal service registration mode.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(state.ModuleManifestPath))
        {
            error = "Missing --kestrun-manifest for internal service registration mode.";
            return false;
        }

        options = new ServiceRegisterOptions(
            state.ServiceName,
            state.ServiceHostExecutablePath,
            state.RunnerExecutablePath,
            state.ScriptPath,
            state.ModuleManifestPath,
            state.ScriptArguments,
            state.ServiceLogPath,
            state.ServiceUser,
            state.ServicePassword);
        return true;
    }
}
