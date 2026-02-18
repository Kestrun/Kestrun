using Kestrun.Launcher;
using System.Reflection;


// Parse command-line arguments
var parsedArgs = Args.Parse([.. Environment.GetCommandLineArgs().Skip(1)]);
if (parsedArgs.Debugger)
{
    _ = ProcessHelper.WaitForDebugger();
}
// Show help
if (parsedArgs.Help)
{
    ShowHelp();
    return 0;
}

// Show version
if (parsedArgs.Version)
{
    ShowVersion();
    return 0;
}

// Validate arguments
if (!string.IsNullOrEmpty(parsedArgs.AppPath))
{
    try
    {
        parsedArgs.AppPath = Path.GetFullPath(parsedArgs.AppPath);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: Invalid app path '{parsedArgs.AppPath}': {ex.Message}");
        return 1;
    }
}

if (!string.IsNullOrEmpty(parsedArgs.KestrunModulePath))
{
    try
    {
        parsedArgs.KestrunModulePath = Path.GetFullPath(parsedArgs.KestrunModulePath);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: Invalid Kestrun module path '{parsedArgs.KestrunModulePath}': {ex.Message}");
        return 1;
    }
}

if (!parsedArgs.Validate(out var error))
{
    Console.Error.WriteLine($"Error: {error}");
    Console.Error.WriteLine();
    ShowHelp();
    return 1;
}

// Execute command
try
{
    return parsedArgs.Command switch
    {
        LauncherCommand.Run => await RunApp(parsedArgs),
        LauncherCommand.Install => await InstallService(parsedArgs),
        LauncherCommand.Uninstall => await UninstallService(parsedArgs),
        LauncherCommand.Start => await StartService(parsedArgs),
        LauncherCommand.Stop => await StopService(parsedArgs),
        _ => throw new InvalidOperationException($"Unknown command: {parsedArgs.Command}")
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    if (Environment.GetEnvironmentVariable("DEBUG") == "1")
    {
        Console.Error.WriteLine(ex.StackTrace);
    }
    return 1;
}

static void ShowHelp()
{
    Console.WriteLine("kestrun-launcher - Manage Kestrun applications");
    Console.WriteLine();
    Console.WriteLine("USAGE:");
    Console.WriteLine("  kestrun-launcher [command] [options]");
    Console.WriteLine();
    Console.WriteLine("COMMANDS:");
    Console.WriteLine("  run [path]              Run a Kestrun app from the specified folder/script");
    Console.WriteLine("  install [path]          Install Kestrun app as a Windows service");
    Console.WriteLine("  uninstall               Uninstall a Windows service");
    Console.WriteLine("  start                   Start a Windows service");
    Console.WriteLine("  stop                    Stop a Windows service");
    Console.WriteLine();
    Console.WriteLine("OPTIONS:");
    Console.WriteLine("  -p, --path <path>       Path to the Kestrun app folder or script");
    Console.WriteLine("  -n, --service-name <name>  Service name (required for service commands)");
    Console.WriteLine("  -k, --kestrun-module <path>  Path to Kestrun module (optional, auto-detected by default)");
    Console.WriteLine("  -h, --help              Show this help message");
    Console.WriteLine("  -v, --version           Show version information");
    Console.WriteLine();
    Console.WriteLine("EXAMPLES:");
    Console.WriteLine("  kestrun-launcher run ./my-app");
    Console.WriteLine("  kestrun-launcher install ./my-app -n MyKestrunService");
    Console.WriteLine("  kestrun-launcher start -n MyKestrunService");
    Console.WriteLine("  kestrun-launcher stop -n MyKestrunService");
    Console.WriteLine("  kestrun-launcher uninstall -n MyKestrunService");
}

static void ShowVersion()
{
    var assembly = Assembly.GetExecutingAssembly();
    var version = assembly.GetName().Version;
    var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    Console.WriteLine($"kestrun-launcher version {informationalVersion ?? version?.ToString() ?? "unknown"}");
}

static async Task<int> RunApp(Args args)
{
    Console.WriteLine($"Running Kestrun app from: {args.AppPath}");

    // Resolve the app path
    var appPath = Path.GetFullPath(args.AppPath!);

    // Check if it's a PowerShell script or a directory
    if (File.Exists(appPath) && Path.GetExtension(appPath).Equals(".ps1", StringComparison.OrdinalIgnoreCase))
    {
        // Run PowerShell script
        return await PowerShellRunner.RunScript(appPath, args.KestrunModulePath);
    }
    else if (Directory.Exists(appPath))
    {
        // Look for a startup script in the directory
        var startupScript = FindStartupScript(appPath);
        if (startupScript != null)
        {
            return await PowerShellRunner.RunScript(startupScript, args.KestrunModulePath);
        }
        else
        {
            Console.Error.WriteLine($"No startup script found in: {appPath}");
            Console.Error.WriteLine("Expected: serve.ps1, server.ps1, start.ps1, main.ps1, or app.ps1");
            return 1;
        }
    }
    else
    {
        Console.Error.WriteLine($"Invalid app path: {appPath}");
        return 1;
    }
}

static string? FindStartupScript(string directory)
{
    var candidates = new[] { "serve.ps1", "server.ps1", "start.ps1", "main.ps1", "app.ps1" };

    foreach (var candidate in candidates)
    {
        var scriptPath = Path.Combine(directory, candidate);
        if (File.Exists(scriptPath))
        {
            return scriptPath;
        }
    }

    return null;
}

static async Task<int> InstallService(Args args)
{
    Console.WriteLine($"Installing service '{args.ServiceName}' for app: {args.AppPath}");

    var appPath = Path.GetFullPath(args.AppPath!);
    var modulePath = string.IsNullOrEmpty(args.KestrunModulePath) ? null : Path.GetFullPath(args.KestrunModulePath);
    var launcher = Assembly.GetExecutingAssembly().Location;
    var serviceName = args.ServiceName!;

    // Build the command line arguments for the service
    var cmdArgs = $"run \\\"{appPath}\\\"";
    if (!string.IsNullOrEmpty(modulePath))
    {
        cmdArgs += $" -k \\\"{modulePath}\\\"";
    }


    // Use sc.exe to create the service
    var arguments = $"create \"{serviceName}\" binPath= \"\\\"{launcher}\\\" {cmdArgs}\" start= auto";

    var result = await ServiceController.ExecuteCommand("sc.exe", arguments);

    if (result == 0)
    {
        Console.WriteLine($"Service '{serviceName}' installed successfully.");
        Console.WriteLine($"Use 'kestrun-launcher start -n {serviceName}' to start the service.");
    }

    return result;
}

static async Task<int> UninstallService(Args args)
{
    Console.WriteLine($"Uninstalling service '{args.ServiceName}'...");

    var serviceName = args.ServiceName;

    // Stop the service first if it's running
    _ = await ServiceController.ExecuteCommand("sc.exe", $"stop \"{serviceName}\"");

    // Delete the service
    var result = await ServiceController.ExecuteCommand("sc.exe", $"delete \"{serviceName}\"");

    if (result == 0)
    {
        Console.WriteLine($"Service '{serviceName}' uninstalled successfully.");
    }

    return result;
}

static async Task<int> StartService(Args args)
{
    Console.WriteLine($"Starting service '{args.ServiceName}'...");

    var serviceName = args.ServiceName;
    var result = await ServiceController.ExecuteCommand("sc.exe", $"start \"{serviceName}\"");

    if (result == 0)
    {
        Console.WriteLine($"Service '{serviceName}' started successfully.");
    }

    return result;
}

static async Task<int> StopService(Args args)
{
    Console.WriteLine($"Stopping service '{args.ServiceName}'...");

    var serviceName = args.ServiceName;
    var result = await ServiceController.ExecuteCommand("sc.exe", $"stop \"{serviceName}\"");

    if (result == 0)
    {
        Console.WriteLine($"Service '{serviceName}' stopped successfully.");
    }

    return result;
}
