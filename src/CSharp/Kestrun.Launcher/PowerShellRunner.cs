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
    public static async Task<int> RunScript(string scriptPath)
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
            
            // Set working directory to script directory
            var scriptDirectory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(scriptDirectory))
            {
                Environment.CurrentDirectory = scriptDirectory;
            }
            
            // Create runspace
            using var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
            runspace.Open();
            
            // Create PowerShell instance
            using var powershell = PowerShell.Create();
            powershell.Runspace = runspace;
            
            // Add the script
            powershell.AddScript($". '{fullPath}'");
            
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
}
