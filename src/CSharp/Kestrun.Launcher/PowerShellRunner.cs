using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;

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

            // Resolve and import the Kestrun module before creating the runspace
            var modulePath = ResolveKestrunModulePath(kestrunModulePath);
            if (!string.IsNullOrEmpty(modulePath))
            {
                initialSessionState.ImportPSModule([modulePath]);
                Console.WriteLine($"Kestrun module imported from: {modulePath}");
            }
            else if (!string.IsNullOrEmpty(kestrunModulePath))
            {
                try
                {
                    Console.WriteLine($"Warning: Kestrun module path not found: {Path.GetFullPath(kestrunModulePath)}");
                }
                catch
                {
                    Console.WriteLine($"Warning: Kestrun module path not found: {kestrunModulePath}");
                }
            }
            else
            {
                Console.WriteLine("Warning: Kestrun module not imported. Continuing without it.");
            }

            // Keep the current working directory (user's path) rather than changing to script directory
            // This allows scripts to access files relative to where the user ran the command

            // Create runspace
            using var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
            runspace.Open();

            // Create PowerShell instance
            using var powershell = PowerShell.Create();
            powershell.Runspace = runspace;

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
    private static string? ResolveKestrunModulePath(string? customModulePath)
    {
        if (!string.IsNullOrEmpty(customModulePath))
        {
            try
            {
                var candidate = Path.GetFullPath(customModulePath);
                return File.Exists(candidate) ? candidate : null;
            }
            catch
            {
                return null;
            }
        }

        return FindDefaultKestrunModule();
    }

    /// <summary>
    /// Locates the Kestrun module path.
    /// It first attempts to find the module in the development environment by searching upwards from the current directory
    /// If not found, it will then check the production environment using PowerShell.
    /// </summary>
    /// <returns>The full path to the Kestrun module if found, otherwise null.</returns>
    public static string? FindDefaultKestrunModule()
    {
        // 1. Try development search
        var asm = Assembly.GetExecutingAssembly();
        var dllPath = asm.Location;
        // Get full InformationalVersion
        var fullVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // Strip build metadata if present (everything after and including '+')
        var semver = fullVersion?.Split('+')[0];

        var devPath = FindFileUpwards(Path.GetDirectoryName(dllPath)!, Path.Combine("src", "PowerShell", "Kestrun", "Kestrun.psd1"));

        if (devPath != null)
        {
            Console.WriteLine("🌿 Development module found.");
            return devPath;
        }
        if (semver == null)
        {
            Console.Error.WriteLine("🚫 Unable to determine assembly version for Kestrun module lookup.");
            return null;
        }
        Console.WriteLine($"🔍 Searching for Kestrun PowerShell module version: {semver}");


        Console.Error.WriteLine("🚫 Kestrun.psm1 not found in any known location.");
        return null;
    }

    /// <summary>
    /// Finds a file upwards from the current directory.
    /// </summary>
    /// <param name="startDir">The starting directory to search from.</param>
    /// <param name="relativeTarget">The relative path of the target file.</param>
    /// <returns>The full path to the file if found, otherwise null.</returns>
    private static string? FindFileUpwards(string startDir, string relativeTarget)
    {
        var current = startDir;

        while (current != null)
        {
            var candidate = Path.Combine(current, relativeTarget);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }
}
