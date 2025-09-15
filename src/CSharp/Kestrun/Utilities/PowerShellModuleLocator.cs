using System.Diagnostics;
using System.Reflection;
using Serilog;
namespace Kestrun.Utilities;

/// <summary>
/// Utility class to locate the Kestrun PowerShell module.
/// It searches for the module in both development and production environments.
/// </summary>
public static class PowerShellModuleLocator
{
    /// <summary>
    /// Retrieves the PowerShell module paths using pwsh.
    /// This method executes a PowerShell command to get the PSModulePath environment variable,
    /// splits it by the path separator, and returns the individual paths as an array.
    /// </summary>
    /// <returns>Array of PowerShell module paths.</returns>
    private static string[] GetPSModulePathsViaPwsh()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = "-NoProfile -Command \"$env:PSModulePath -split [IO.Path]::PathSeparator\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                Log.Error("❌ Failed to start pwsh process.");
                return [];
            }

            var output = proc.StandardOutput.ReadToEnd();
            var error = proc.StandardError.ReadToEnd();

            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                Log.Error("❌ pwsh exited with code {ExitCode}. Error:\n{Error}", proc.ExitCode, error);
                return [];
            }

            return output
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        catch (Exception ex)
        {
            Log.Error("⚠️ Exception during pwsh invocation: {Message}", ex.Message);
            return [];
        }
    }

    /// <summary>
    /// Locates the Kestrun module path.
    /// It first attempts to find the module in the development environment by searching upwards from the current directory
    /// If not found, it will then check the production environment using PowerShell.
    /// </summary>
    /// <returns>The full path to the Kestrun module if found, otherwise null.</returns>
    public static string? LocateKestrunModule()
    {
        // 1. Try development search
        var asm = Assembly.GetExecutingAssembly();
        var dllPath = asm.Location;
        // Get full InformationalVersion
        var fullVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // Strip build metadata if present (everything after and including '+')
        var semver = fullVersion?.Split('+')[0];

        var devPath = FindFileUpwards(Path.GetDirectoryName(dllPath)!, Path.Combine("src", "PowerShell", "Kestrun", "Kestrun.psm1"));

        if (devPath != null)
        {
            Log.Information("🌿 Development module found.");
            return devPath;
        }
        if (semver == null)
        {
            Log.Error("🚫 Unable to determine assembly version for Kestrun module lookup.");
            return null;
        }
        Log.Information("🔍 Searching for Kestrun PowerShell module version: {Semver}", semver);
        // 2. Production mode - ask pwsh
        Log.Information("🛰  Switching to production lookup via pwsh...");
        foreach (var path in GetPSModulePathsViaPwsh())
        {
            var full = Path.Combine(path, "Kestrun", semver, "Kestrun.psm1");
            if (File.Exists(full))
            {
                Console.WriteLine($"✅ Found production module: {full}");
                return full;
            }
        }

        Log.Error("🚫 Kestrun.psm1 not found in any known location.");
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
