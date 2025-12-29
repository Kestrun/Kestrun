namespace Kestrun.Launcher;

/// <summary>
/// Command-line arguments for kestrun-launcher
/// </summary>
public class Args
{
    public string? AppPath { get; set; }
    public LauncherCommand Command { get; set; } = LauncherCommand.Run;
    public string? ServiceName { get; set; }
    public string? KestrunModulePath { get; set; }
    public bool Help { get; set; }
    public bool Version { get; set; }

    /// <summary>
    /// Parse command-line arguments
    /// </summary>
    public static Args Parse(string[] args)
    {
        var parsed = new Args();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg.ToLowerInvariant())
            {
                case "-h":
                case "--help":
                    parsed.Help = true;
                    break;

                case "-v":
                case "--version":
                    parsed.Version = true;
                    break;

                case "run":
                    parsed.Command = LauncherCommand.Run;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                    {
                        parsed.AppPath = args[++i];
                    }
                    break;

                case "install":
                    parsed.Command = LauncherCommand.Install;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                    {
                        parsed.AppPath = args[++i];
                    }
                    break;

                case "uninstall":
                    parsed.Command = LauncherCommand.Uninstall;
                    break;

                case "start":
                    parsed.Command = LauncherCommand.Start;
                    break;

                case "stop":
                    parsed.Command = LauncherCommand.Stop;
                    break;

                case "--service-name":
                case "-n":
                    if (i + 1 < args.Length)
                    {
                        parsed.ServiceName = args[++i];
                    }
                    break;

                case "--path":
                case "-p":
                    if (i + 1 < args.Length)
                    {
                        parsed.AppPath = args[++i];
                    }
                    break;

                case "--kestrun-module":
                case "-k":
                    if (i + 1 < args.Length)
                    {
                        parsed.KestrunModulePath = args[++i];
                    }
                    break;

                default:
                    // If no command specified and this is the first positional arg, treat as path
                    if (string.IsNullOrEmpty(parsed.AppPath) && !arg.StartsWith('-'))
                    {
                        parsed.AppPath = arg;
                    }
                    break;
            }
        }

        return parsed;
    }

    /// <summary>
    /// Validate the parsed arguments
    /// </summary>
    public bool Validate(out string? error)
    {
        error = null;

        if (Help || Version)
        {
            return true;
        }

        // Service operations need a service name
        if (Command is LauncherCommand.Install or
            LauncherCommand.Uninstall or
            LauncherCommand.Start or
            LauncherCommand.Stop)
        {
            if (string.IsNullOrEmpty(ServiceName))
            {
                error = $"Command '{Command}' requires a service name (--service-name or -n)";
                return false;
            }
        }

        // Install and Run need an app path
        if (Command is LauncherCommand.Install or LauncherCommand.Run)
        {
            if (string.IsNullOrEmpty(AppPath))
            {
                error = $"Command '{Command}' requires an app path";
                return false;
            }

            if (!Directory.Exists(AppPath) && !File.Exists(AppPath))
            {
                error = $"App path does not exist: {AppPath}";
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Available launcher commands
/// </summary>
public enum LauncherCommand
{
    Run,
    Install,
    Uninstall,
    Start,
    Stop
}
