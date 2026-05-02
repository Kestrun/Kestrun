namespace Kestrun.Mcp.ServerHost;

/// <summary>
/// Parsed command-line options for the Kestrun MCP server.
/// </summary>
internal sealed record KestrunMcpCommandLine(
    string ScriptPath,
    string ModuleManifestPath,
    string? HostName,
    bool DiscoverPowerShellHome,
    bool AllowInvokeRoute,
    IReadOnlyList<string> AllowedInvokePaths)
{
    /// <summary>
    /// Parses command-line arguments into server options.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The parsed options, or null when parsing fails.</returns>
    public static KestrunMcpCommandLine? Parse(string[] args)
    {
        string? scriptPath = null;
        string? manifestPath = null;
        string? hostName = null;
        var discoverPowerShellHome = false;
        var allowInvokeRoute = false;
        var allowedInvokePaths = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--script":
                    scriptPath = ReadValue(args, ref index, "--script");
                    break;
                case "--kestrun-manifest":
                    manifestPath = ReadValue(args, ref index, "--kestrun-manifest");
                    break;
                case "--host-name":
                    hostName = ReadValue(args, ref index, "--host-name");
                    break;
                case "--discover-pshome":
                    discoverPowerShellHome = true;
                    break;
                case "--allow-invoke":
                    allowInvokeRoute = true;
                    allowedInvokePaths.Add(ReadValue(args, ref index, "--allow-invoke"));
                    break;
                case "--help":
                case "-h":
                    PrintUsage();
                    return null;
                default:
                    throw new InvalidOperationException($"Unknown argument '{args[index]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            PrintUsage("Missing required --script argument.");
            return null;
        }

        manifestPath ??= ResolveDefaultManifestPath();
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            throw new InvalidOperationException("Unable to resolve a default Kestrun module manifest path. Provide --kestrun-manifest.");
        }

        return new KestrunMcpCommandLine(
            Path.GetFullPath(scriptPath),
            Path.GetFullPath(manifestPath),
            hostName,
            discoverPowerShellHome,
            allowInvokeRoute,
            allowInvokeRoute ? allowedInvokePaths : []);
    }

    /// <summary>
    /// Reads a required option value.
    /// </summary>
    /// <param name="args">All arguments.</param>
    /// <param name="index">The current argument index.</param>
    /// <param name="optionName">The option name.</param>
    /// <returns>The required value.</returns>
    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new InvalidOperationException($"Missing value for {optionName}.");
        }

        index++;
        return args[index];
    }

    /// <summary>
    /// Resolves a default Kestrun module manifest path.
    /// </summary>
    /// <returns>The manifest path when found; otherwise null.</returns>
    private static string? ResolveDefaultManifestPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Modules", "Kestrun", "Kestrun.psd1"),
            Path.Combine(AppContext.BaseDirectory, "Kestrun.psd1"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "PowerShell", "Kestrun", "Kestrun.psd1"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Prints usage help to stderr.
    /// </summary>
    /// <param name="error">Optional parse error.</param>
    private static void PrintUsage(string? error = null)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            Console.Error.WriteLine(error);
        }

        Console.Error.WriteLine("Usage: Kestrun.Mcp --script <path> [--kestrun-manifest <path>] [--host-name <name>] [--discover-pshome] [--allow-invoke <glob> ...]");
    }
}
