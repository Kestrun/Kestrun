using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace Kestrun.Launcher;

/// <summary>
/// Runs PowerShell scripts in-process
/// </summary>
public static class PowerShellRunner
{
    /// <summary>
    /// Execute a PowerShell script file
    /// </summary>
    /// <param name="scriptPath">Path to the PowerShell script</param>
    /// <param name="kestrunModulePath">Optional path to Kestrun module. If not specified, will auto-detect from default location.</param>
    public static async Task<int> RunScript(string scriptPath, string? kestrunModulePath = null)
    {
        var fullPath = Path.GetFullPath(scriptPath);

        if (!File.Exists(fullPath))
        {
            Console.Error.WriteLine($"Script not found: {fullPath}");
            return 1;
        }

        Console.WriteLine($"Executing PowerShell script: {fullPath}");

        try
        {
            // Create initial session state
            var initialSessionState = InitialSessionState.CreateDefault();

            // Set execution policy to allow scripts
            initialSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;

            // Keep the current working directory (user's path) rather than changing to script directory
            // This allows scripts to access files relative to where the user ran the command

            // Create runspace
            using var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
            runspace.Open();

            // Create PowerShell instance
            using var powershell = PowerShell.Create();
            powershell.Runspace = runspace;

            // Import Kestrun module
            var moduleImported = await ImportKestrunModule(powershell, kestrunModulePath);
            if (!moduleImported)
            {
                Console.WriteLine("Warning: Kestrun module not imported. Continuing without it.");
            }

            // Add the script
            _ = powershell.AddScript($". '{fullPath}'");

            // Set up output handling
            powershell.Streams.Error.DataAdded += (sender, args) =>
            {
                if (sender is PSDataCollection<ErrorRecord> errors && args.Index < errors.Count)
                {
                    var error = errors[args.Index];
                    Console.Error.WriteLine($"ERROR: {error}");
                }
            };

            powershell.Streams.Warning.DataAdded += (sender, args) =>
            {
                if (sender is PSDataCollection<WarningRecord> warnings && args.Index < warnings.Count)
                {
                    var warning = warnings[args.Index];
                    Console.WriteLine($"WARNING: {warning}");
                }
            };

            powershell.Streams.Verbose.DataAdded += (sender, args) =>
            {
                if (sender is PSDataCollection<VerboseRecord> verboses && args.Index < verboses.Count)
                {
                    var verbose = verboses[args.Index];
                    Console.WriteLine($"VERBOSE: {verbose}");
                }
            };

            powershell.Streams.Debug.DataAdded += (sender, args) =>
            {
                if (sender is PSDataCollection<DebugRecord> debugs && args.Index < debugs.Count)
                {
                    var debug = debugs[args.Index];
                    Console.WriteLine($"DEBUG: {debug}");
                }
            };

            // Execute the script
            var results = await Task.Run(() => powershell.Invoke());

            // Output results
            foreach (var result in results)
            {
                Console.WriteLine(result);
            }

            // Check for errors
            if (powershell.HadErrors)
            {
                Console.Error.WriteLine("Script execution completed with errors.");
                return 1;
            }

            Console.WriteLine("Script execution completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to execute script: {ex.Message}");
            if (Environment.GetEnvironmentVariable("DEBUG") == "1")
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    /// <summary>
    /// Import the Kestrun PowerShell module
    /// </summary>
    private static async Task<bool> ImportKestrunModule(PowerShell powershell, string? customModulePath)
    {
        try
        {
            var modulePath = customModulePath;

            // If no custom path specified, try to find the default module
            if (string.IsNullOrEmpty(modulePath))
            {
                modulePath = FindDefaultKestrunModule();
            }

            if (string.IsNullOrEmpty(modulePath))
            {
                return false;
            }

            // Import the module
            powershell.Commands.Clear();
            _ = powershell.AddCommand("Import-Module")
                         .AddParameter("Name", modulePath)
                         .AddParameter("Force", true);

            var results = await Task.Run(() => powershell.Invoke());

            if (powershell.HadErrors)
            {
                Console.WriteLine($"Warning: Failed to import Kestrun module from: {modulePath}");
                foreach (var error in powershell.Streams.Error)
                {
                    Console.WriteLine($"  {error}");
                }
                return false;
            }

            Console.WriteLine($"Kestrun module imported from: {modulePath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Exception importing Kestrun module: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Find the default Kestrun module path
    /// </summary>
    private static string? FindDefaultKestrunModule()
    {
        // Try to find the module relative to the launcher executable
        var launcherDir = AppContext.BaseDirectory;

        // Common relative paths from the launcher to the Kestrun module
        var relativePaths = new[]
        {
            // When built from source: bin/Debug/netX.0 -> src/PowerShell/Kestrun
            "../../../src/PowerShell/Kestrun/Kestrun.psd1",
            "../../../../src/PowerShell/Kestrun/Kestrun.psd1",
            "../../../../../src/PowerShell/Kestrun/Kestrun.psd1",
            // When installed as a tool
            "../../PowerShell/Kestrun/Kestrun.psd1",
            "../PowerShell/Kestrun/Kestrun.psd1"
        };

        foreach (var relativePath in relativePaths)
        {
            var fullPath = Path.GetFullPath(Path.Combine(launcherDir, relativePath));
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // Try looking in PSModulePath
        var psModulePath = Environment.GetEnvironmentVariable("PSModulePath");
        if (!string.IsNullOrEmpty(psModulePath))
        {
            var paths = psModulePath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var path in paths)
            {
                var moduleManifest = Path.Combine(path, "Kestrun", "Kestrun.psd1");
                if (File.Exists(moduleManifest))
                {
                    return moduleManifest;
                }
            }
        }

        return null;
    }
}
