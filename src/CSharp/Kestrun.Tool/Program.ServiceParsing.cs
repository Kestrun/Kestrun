namespace Kestrun.Tool;

internal static partial class Program
{
    private sealed class ServiceParseState
    {
        public string ServiceName { get; set; } = string.Empty;

        public bool ServiceNameSet { get; set; }

        public string ScriptPath { get; set; } = string.Empty;

        public bool ScriptPathSet { get; set; }

        public string[] ScriptArguments { get; set; } = [];

        public string? ServiceLogPath { get; set; }

        public string? ServiceUser { get; set; }

        public string? ServicePassword { get; set; }

        public string? ServiceContentRoot { get; set; }

        public string? ServiceDeploymentRoot { get; set; }

        public string? ServiceContentRootChecksum { get; set; }

        public string? ServiceContentRootChecksumAlgorithm { get; set; }

        public string? ServiceContentRootBearerToken { get; set; }

        public bool ServiceContentRootIgnoreCertificate { get; set; }

        public List<string> ServiceContentRootHeaders { get; } = [];
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
        => new(CommandMode.ServiceInstall, string.Empty, false, [], kestrunFolder, kestrunManifestPath, null, false, null, null, null, null, ModuleStorageScope.Local, false, null, null, null, null, null, false, []);

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
                if (!string.IsNullOrEmpty(error))
                {
                    return false;
                }

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
            state.ScriptPathSet,
            state.ScriptArguments,
            kestrunFolder,
            kestrunManifestPath,
            state.ServiceName,
            state.ServiceNameSet,
            state.ServiceLogPath,
            state.ServiceUser,
            state.ServicePassword,
            null,
            ModuleStorageScope.Local,
            false,
            state.ServiceContentRoot,
            state.ServiceDeploymentRoot,
            state.ServiceContentRootChecksum,
            state.ServiceContentRootChecksumAlgorithm,
            state.ServiceContentRootBearerToken,
            state.ServiceContentRootIgnoreCertificate,
            [.. state.ServiceContentRootHeaders]);

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

        return current switch
        {
            "--script" => TryConsumeServiceScriptOption(args, mode, state, ref index, out error),
            "--name" or "-n" => TryConsumeServiceNameOption(args, state, ref index, out error),
            "--kestrun-folder" or "-k" => TryConsumeKestrunFolderOption(args, ref kestrunFolder, ref index, out error),
            "--kestrun-manifest" or "-m" => TryConsumeKestrunManifestOption(args, ref kestrunManifestPath, ref index, out error),
            "--service-log-path" => TryConsumeServiceLogPathOption(args, state, ref index, out error),
            "--service-user" => TryConsumeServiceUserOption(args, mode, state, ref index, out error),
            "--service-password" => TryConsumeServicePasswordOption(args, mode, state, ref index, out error),
            "--deployment-root" => TryConsumeServiceDeploymentRootOption(args, mode, state, ref index, out error),
            "--content-root" => TryConsumeServiceContentRootOption(args, mode, state, ref index, out error),
            "--content-root-checksum" => TryConsumeServiceContentRootChecksumOption(args, mode, state, ref index, out error),
            "--content-root-checksum-algorithm" => TryConsumeServiceContentRootChecksumAlgorithmOption(args, mode, state, ref index, out error),
            "--content-root-bearer-token" => TryConsumeServiceContentRootBearerTokenOption(args, mode, state, ref index, out error),
            "--content-root-ignore-certificate" => TryConsumeServiceContentRootIgnoreCertificateOption(mode, state, ref index, out error),
            "--content-root-header" => TryConsumeServiceContentRootHeaderOption(args, mode, state, ref index, out error),
            _ => false,
        };
    }

    /// <summary>
    /// Consumes and validates the service script option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the option token is handled.</returns>
    private static bool TryConsumeServiceScriptOption(string[] args, CommandMode mode, ServiceParseState state, ref int index, out string error)
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

    /// <summary>
    /// Consumes and applies the service name option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the option token is handled.</returns>
    private static bool TryConsumeServiceNameOption(string[] args, ServiceParseState state, ref int index, out string error)
    {
        if (!TryConsumeOptionValue(args, ref index, "--name", out var value, out error))
        {
            return true;
        }

        state.ServiceName = value;
        state.ServiceNameSet = true;
        return true;
    }

    /// <summary>
    /// Consumes and applies the Kestrun folder option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="kestrunFolder">Optional folder override.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the option token is handled.</returns>
    private static bool TryConsumeKestrunFolderOption(string[] args, ref string? kestrunFolder, ref int index, out string error)
    {
        if (!TryConsumeOptionValue(args, ref index, "--kestrun-folder", out var value, out error))
        {
            return true;
        }

        kestrunFolder = value;
        return true;
    }

    /// <summary>
    /// Consumes and applies the Kestrun manifest option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="kestrunManifestPath">Optional manifest override.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the option token is handled.</returns>
    private static bool TryConsumeKestrunManifestOption(string[] args, ref string? kestrunManifestPath, ref int index, out string error)
    {
        if (!TryConsumeOptionValue(args, ref index, "--kestrun-manifest", out var value, out error))
        {
            return true;
        }

        kestrunManifestPath = value;
        return true;
    }

    /// <summary>
    /// Consumes and applies the service log path option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the option token is handled.</returns>
    private static bool TryConsumeServiceLogPathOption(string[] args, ServiceParseState state, ref int index, out string error)
    {
        if (!TryConsumeOptionValue(args, ref index, "--service-log-path", out var value, out error))
        {
            return true;
        }

        state.ServiceLogPath = value;
        return true;
    }

    /// <summary>
    /// Consumes and validates the service-user option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the option token is handled.</returns>
    private static bool TryConsumeServiceUserOption(string[] args, CommandMode mode, ServiceParseState state, ref int index, out string error)
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

    /// <summary>
    /// Consumes and validates the service-password option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the option token is handled.</returns>
    private static bool TryConsumeServicePasswordOption(string[] args, CommandMode mode, ServiceParseState state, ref int index, out string error)
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

    /// <summary>
    /// Consumes and validates the deployment-root option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the option token is handled.</returns>
    private static bool TryConsumeServiceDeploymentRootOption(string[] args, CommandMode mode, ServiceParseState state, ref int index, out string error)
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

    /// <summary>
    /// Consumes and validates the content-root option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the option token is handled.</returns>
    private static bool TryConsumeServiceContentRootOption(string[] args, CommandMode mode, ServiceParseState state, ref int index, out string error)
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

    /// <summary>
    /// Consumes and validates the content-root checksum option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the option token is handled.</returns>
    private static bool TryConsumeServiceContentRootChecksumOption(string[] args, CommandMode mode, ServiceParseState state, ref int index, out string error)
    {
        if (mode is CommandMode.ServiceRemove or CommandMode.ServiceStart or CommandMode.ServiceStop or CommandMode.ServiceQuery)
        {
            error = "Service remove/start/stop/query does not accept --content-root-checksum.";
            return true;
        }

        if (!TryConsumeOptionValue(args, ref index, "--content-root-checksum", out var value, out error))
        {
            return true;
        }

        state.ServiceContentRootChecksum = value;
        return true;
    }

    /// <summary>
    /// Consumes and validates the content-root checksum algorithm option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the option token is handled.</returns>
    private static bool TryConsumeServiceContentRootChecksumAlgorithmOption(string[] args, CommandMode mode, ServiceParseState state, ref int index, out string error)
    {
        if (mode is CommandMode.ServiceRemove or CommandMode.ServiceStart or CommandMode.ServiceStop or CommandMode.ServiceQuery)
        {
            error = "Service remove/start/stop/query does not accept --content-root-checksum-algorithm.";
            return true;
        }

        if (!TryConsumeOptionValue(args, ref index, "--content-root-checksum-algorithm", out var value, out error))
        {
            return true;
        }

        state.ServiceContentRootChecksumAlgorithm = value;
        return true;
    }

    /// <summary>
    /// Consumes and validates the content-root bearer token option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the option token is handled.</returns>
    private static bool TryConsumeServiceContentRootBearerTokenOption(string[] args, CommandMode mode, ServiceParseState state, ref int index, out string error)
    {
        if (mode is CommandMode.ServiceRemove or CommandMode.ServiceStart or CommandMode.ServiceStop or CommandMode.ServiceQuery)
        {
            error = "Service remove/start/stop/query does not accept --content-root-bearer-token.";
            return true;
        }

        if (!TryConsumeOptionValue(args, ref index, "--content-root-bearer-token", out var value, out error))
        {
            return true;
        }

        state.ServiceContentRootBearerToken = value;
        return true;
    }

    /// <summary>
    /// Consumes and validates the content-root certificate-ignore option.
    /// </summary>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the option token is handled.</returns>
    private static bool TryConsumeServiceContentRootIgnoreCertificateOption(CommandMode mode, ServiceParseState state, ref int index, out string error)
    {
        if (mode is CommandMode.ServiceRemove or CommandMode.ServiceStart or CommandMode.ServiceStop or CommandMode.ServiceQuery)
        {
            error = "Service remove/start/stop/query does not accept --content-root-ignore-certificate.";
            return true;
        }

        state.ServiceContentRootIgnoreCertificate = true;
        index += 1;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Consumes and validates the content-root custom header option.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="index">Current parser index.</param>
    /// <param name="error">Error text when parsing fails.</param>
    /// <returns>True when the option token is handled.</returns>
    private static bool TryConsumeServiceContentRootHeaderOption(string[] args, CommandMode mode, ServiceParseState state, ref int index, out string error)
    {
        if (mode is CommandMode.ServiceRemove or CommandMode.ServiceStart or CommandMode.ServiceStop or CommandMode.ServiceQuery)
        {
            error = "Service remove/start/stop/query does not accept --content-root-header.";
            return true;
        }

        if (!TryConsumeOptionValue(args, ref index, "--content-root-header", out var value, out error))
        {
            return true;
        }

        state.ServiceContentRootHeaders.Add(value);
        return true;
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
        if (!TryValidateServiceName(mode, state, out error))
        {
            return false;
        }

        ApplyDefaultServiceInstallScript(mode, state);

        return TryValidateServiceCredentialOptions(mode, state, out error) && TryValidateServiceContentRootDependentOptions(mode, state, out error);
    }

    /// <summary>
    /// Validates that the service name option was provided.
    /// </summary>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="error">Error text when validation fails.</param>
    /// <returns>True when the service name is valid.</returns>
    private static bool TryValidateServiceName(CommandMode mode, ServiceParseState state, out string error)
    {
        if (mode != CommandMode.ServiceInstall || string.IsNullOrWhiteSpace(state.ServiceContentRoot))
        {
            if (string.IsNullOrWhiteSpace(state.ServiceName))
            {
                error = "Service name is required. Use --name <value>.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        if (state.ServiceNameSet)
        {
            error = "--name is not supported when --content-root is used. Define Name in Service.psd1.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Applies the default script path for service install when script is omitted.
    /// </summary>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    private static void ApplyDefaultServiceInstallScript(CommandMode mode, ServiceParseState state)
    {
        if (mode == CommandMode.ServiceInstall
            && string.IsNullOrWhiteSpace(state.ServiceContentRoot)
            && !state.ScriptPathSet)
        {
            state.ScriptPath = DefaultScriptFileName;
        }
    }

    /// <summary>
    /// Validates credential-related service install options.
    /// </summary>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="error">Error text when validation fails.</param>
    /// <returns>True when credential options are valid.</returns>
    private static bool TryValidateServiceCredentialOptions(CommandMode mode, ServiceParseState state, out string error)
    {
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

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Validates content-root dependent options for service install mode.
    /// </summary>
    /// <param name="mode">Current service mode.</param>
    /// <param name="state">Mutable service parse state.</param>
    /// <param name="error">Error text when validation fails.</param>
    /// <returns>True when content-root dependent options are valid.</returns>
    private static bool TryValidateServiceContentRootDependentOptions(CommandMode mode, ServiceParseState state, out string error)
    {
        if (mode != CommandMode.ServiceInstall)
        {
            error = string.Empty;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(state.ServiceContentRoot) && state.ScriptPathSet)
        {
            error = "--script (or positional script path) is not supported when --content-root is used. Define Script in Service.psd1.";
            return false;
        }

        var hasChecksum = !string.IsNullOrWhiteSpace(state.ServiceContentRootChecksum);
        var hasChecksumAlgorithm = !string.IsNullOrWhiteSpace(state.ServiceContentRootChecksumAlgorithm);
        if (hasChecksumAlgorithm && !hasChecksum)
        {
            error = "--content-root-checksum-algorithm requires --content-root-checksum.";
            return false;
        }

        if (hasChecksum && string.IsNullOrWhiteSpace(state.ServiceContentRoot))
        {
            error = "--content-root-checksum requires --content-root.";
            return false;
        }

        var hasBearerToken = !string.IsNullOrWhiteSpace(state.ServiceContentRootBearerToken);
        if (hasBearerToken && string.IsNullOrWhiteSpace(state.ServiceContentRoot))
        {
            error = "--content-root-bearer-token requires --content-root.";
            return false;
        }

        if (state.ServiceContentRootIgnoreCertificate && string.IsNullOrWhiteSpace(state.ServiceContentRoot))
        {
            error = "--content-root-ignore-certificate requires --content-root.";
            return false;
        }

        if (state.ServiceContentRootHeaders.Count > 0 && string.IsNullOrWhiteSpace(state.ServiceContentRoot))
        {
            error = "--content-root-header requires --content-root.";
            return false;
        }

        error = string.Empty;
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
    /// <param name="error">Parse error when an unsupported option or missing option value is encountered.</param>
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
    /// <param name="error">Parse error when an unsupported option or missing option value is encountered.</param>
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
            error = IsServiceRegisterOptionWithValue(current)
                ? $"Missing value for {current}."
                : $"Unknown service register option: {current}";
            return false;
        }

        return TryApplyServiceRegisterOptionValue(current, value, state, out error);
    }

    /// <summary>
    /// Applies a consumed service-register option value to parse state.
    /// </summary>
    /// <param name="option">Service-register option token.</param>
    /// <param name="value">Consumed option value.</param>
    /// <param name="state">Mutable service-register parse state.</param>
    /// <param name="error">Error text when the option token is unsupported.</param>
    /// <returns>True when the option value is applied.</returns>
    private static bool TryApplyServiceRegisterOptionValue(string option, string value, ServiceRegisterParseState state, out string? error)
    {
        error = null;

        switch (option)
        {
            case "--name":
                state.ServiceName = value;
                return true;
            case "--service-host-exe":
                state.ServiceHostExecutablePath = value;
                return true;
            case "--runner-exe":
                state.RunnerExecutablePath = value;
                return true;
            case "--exe":
                // Backward compatibility for older elevated registration invocations.
                state.ServiceHostExecutablePath = value;
                return true;
            case "--script":
                state.ScriptPath = value;
                return true;
            case "--kestrun-manifest":
            case "-m":
                state.ModuleManifestPath = value;
                return true;
            case "--service-log-path":
                state.ServiceLogPath = value;
                return true;
            case "--service-user":
                state.ServiceUser = value;
                return true;
            case "--service-password":
                state.ServicePassword = value;
                return true;
            default:
                error = $"Unknown service register option: {option}";
                return false;
        }
    }

    /// <summary>
    /// Determines whether a token is a supported service-register option that requires a value.
    /// </summary>
    /// <param name="option">Option token to evaluate.</param>
    /// <returns>True when the token is a recognized option that requires a value.</returns>
    private static bool IsServiceRegisterOptionWithValue(string option)
        => option is "--name"
            or "--service-host-exe"
            or "--runner-exe"
            or "--exe"
            or "--script"
            or "--kestrun-manifest"
            or "-m"
            or "--service-log-path"
            or "--service-user"
            or "--service-password";

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

        if (!IsServiceRegisterOptionWithValue(option))
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
