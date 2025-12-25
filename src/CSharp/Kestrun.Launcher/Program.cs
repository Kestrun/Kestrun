using System.ServiceProcess;
using Kestrun.Launcher.Cli;
using Kestrun.Launcher.Installation;
using Kestrun.Launcher.Logging;
using Kestrun.Launcher.PowerShell;
using Kestrun.Launcher.Service;
using Kestrun.Launcher.Utilities;

namespace Kestrun.Launcher;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        ParsedArgs parsedArgs;
        try
        {
            parsedArgs = Args.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            PrintUsage();
            return 1;
        }

        try
        {
            return parsedArgs.Command switch
            {
                "run" => await RunAsync(parsedArgs),
                "install" => InstallService(parsedArgs),
                "uninstall" => UninstallService(parsedArgs),
                "start" => StartService(parsedArgs),
                "stop" => StopService(parsedArgs),
                "status" => StatusService(parsedArgs),
                "service" => RunService(parsedArgs),
                _ => InvalidCommand(parsedArgs.Command)
            };
        }
        catch (PlatformNotSupportedException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unhandled error: {ex}");
            return 1;
        }
    }

    private static async Task<int> RunAsync(ParsedArgs parsedArgs)
    {
        var root = PathResolver.ResolveRoot(parsedArgs.RequireOption("root"));
        var startup = PathResolver.ResolveStartupScript(root, parsedArgs.GetOption("startup"));
        var scriptArgs = parsedArgs.GetOption("scriptArgs");

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        var logger = SimpleLogger.Create();
        var runner = new InProcPowerShellRunner(logger);
        return await runner.ExecuteAsync(root, startup, scriptArgs, cancellation.Token);
    }

    private static int InstallService(ParsedArgs parsedArgs)
    {
        PlatformGuard.EnsureWindows();
        var serviceName = parsedArgs.RequireOption("name");
        var root = PathResolver.ResolveRoot(parsedArgs.RequireOption("root"));
        var startup = PathResolver.ResolveStartupScript(root, parsedArgs.GetOption("startup"));
        var scriptArgs = parsedArgs.GetOption("scriptArgs");
        var display = parsedArgs.GetOption("display");
        var description = parsedArgs.GetOption("desc");
        var autoStart = parsedArgs.HasOption("auto");

        var logger = SimpleLogger.Create();
        return WindowsServiceInstaller.Install(serviceName, root, startup, scriptArgs, display, description, autoStart, logger);
    }

    private static int UninstallService(ParsedArgs parsedArgs)
    {
        PlatformGuard.EnsureWindows();
        var serviceName = parsedArgs.RequireOption("name");
        var logger = SimpleLogger.Create();
        return WindowsServiceInstaller.Uninstall(serviceName, logger);
    }

    private static int StartService(ParsedArgs parsedArgs)
    {
        PlatformGuard.EnsureWindows();
        var serviceName = parsedArgs.RequireOption("name");
        var logger = SimpleLogger.Create();
        WindowsServiceInstaller.Start(serviceName, logger);
        return 0;
    }

    private static int StopService(ParsedArgs parsedArgs)
    {
        PlatformGuard.EnsureWindows();
        var serviceName = parsedArgs.RequireOption("name");
        var logger = SimpleLogger.Create();
        WindowsServiceInstaller.Stop(serviceName, logger);
        return 0;
    }

    private static int StatusService(ParsedArgs parsedArgs)
    {
        PlatformGuard.EnsureWindows();
        var serviceName = parsedArgs.RequireOption("name");
        var logger = SimpleLogger.Create();
        var status = WindowsServiceInstaller.Status(serviceName, logger);
        Console.WriteLine(status);
        return 0;
    }

    private static int RunService(ParsedArgs parsedArgs)
    {
        PlatformGuard.EnsureWindows();
        var serviceName = parsedArgs.RequireOption("name");
        var root = PathResolver.ResolveRoot(parsedArgs.RequireOption("root"));
        var startup = PathResolver.ResolveStartupScript(root, parsedArgs.GetOption("startup"));
        var scriptArgs = parsedArgs.GetOption("scriptArgs");
        var logger = SimpleLogger.Create(echoToConsole: false);

        ServiceBase.Run(new KestrunWindowsService(serviceName, root, startup, scriptArgs, logger));
        return 0;
    }

    private static int InvalidCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        const string usage = """
            Kestrun Launcher

            Commands:
              run --root <folder> [--startup <file>] [--scriptArgs "<args>"]
              install --name <svcName> --root <folder> [--startup <file>] [--display "<name>"] [--desc "<text>"] [--auto] [--scriptArgs "<args>"]
              uninstall --name <svcName>
              start --name <svcName>
              stop --name <svcName>
              status --name <svcName>
              service --name <svcName> --root <folder> [--startup <file>] [--scriptArgs "<args>"]
            """;
        Console.WriteLine(usage);
    }
}
