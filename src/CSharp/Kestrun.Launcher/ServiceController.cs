using System.Diagnostics;

namespace Kestrun.Launcher;

/// <summary>
/// Helper for managing Windows services via sc.exe
/// </summary>
public static class ServiceController
{
    /// <summary>
    /// Execute a command using sc.exe or another executable
    /// </summary>
    public static async Task<int> ExecuteCommand(string executable, string arguments)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };

            // Capture output
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    _ = outputBuilder.AppendLine(args.Data);
                    Console.WriteLine(args.Data);
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    _ = errorBuilder.AppendLine(args.Data);
                    Console.Error.WriteLine(args.Data);
                }
            };

            _ = process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to execute {executable}: {ex.Message}");
            return 1;
        }
    }
}
