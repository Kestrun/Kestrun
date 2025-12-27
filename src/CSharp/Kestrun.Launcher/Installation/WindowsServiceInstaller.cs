using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using Kestrun.Launcher.Logging;

namespace Kestrun.Launcher.Installation;

internal static class WindowsServiceInstaller
{
    public static int Install(string serviceName, string root, string startupScript, string? scriptArgs, string? displayName, string? description, bool autoStart, SimpleLogger logger)
    {
        var exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Unable to locate current executable path.");
        var binPath = BuildBinPath(exePath, serviceName, root, startupScript, scriptArgs);
        var resolvedDisplayName = string.IsNullOrWhiteSpace(displayName) ? serviceName : displayName;
        var startMode = autoStart ? "auto" : "demand";

        logger.Info($"Installing service '{serviceName}' with binPath: {binPath}");
        var createResult = RunSc($"create {serviceName} binPath= \"{binPath}\" DisplayName= \"{resolvedDisplayName}\" start= {startMode}");
        LogScOutput(logger, "create", createResult);
        if (createResult.ExitCode != 0)
        {
            return createResult.ExitCode;
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            var descResult = RunSc($"description {serviceName} \"{description}\"");
            LogScOutput(logger, "description", descResult);
        }

        return 0;
    }

    public static int Uninstall(string serviceName, SimpleLogger logger)
    {
        logger.Info($"Stopping and uninstalling service '{serviceName}'.");
        TryStopService(serviceName, logger);
        var deleteResult = RunSc($"delete {serviceName}");
        LogScOutput(logger, "delete", deleteResult);
        return deleteResult.ExitCode;
    }

    public static void Start(string serviceName, SimpleLogger logger)
    {
        using var controller = new ServiceController(serviceName);
        if (controller.Status == ServiceControllerStatus.Running)
        {
            logger.Info($"Service '{serviceName}' is already running.");
            return;
        }

        controller.Start();
        controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
        logger.Info($"Service '{serviceName}' started.");
    }

    public static void Stop(string serviceName, SimpleLogger logger)
    {
        using var controller = new ServiceController(serviceName);
        if (controller.Status == ServiceControllerStatus.Stopped)
        {
            logger.Info($"Service '{serviceName}' is already stopped.");
            return;
        }

        controller.Stop();
        controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        logger.Info($"Service '{serviceName}' stopped.");
    }

    public static ServiceControllerStatus Status(string serviceName, SimpleLogger logger)
    {
        using var controller = new ServiceController(serviceName);
        logger.Info($"Service '{serviceName}' status: {controller.Status}");
        return controller.Status;
    }

    private static void TryStopService(string serviceName, SimpleLogger logger)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
            if (controller.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
            {
                return;
            }

            controller.Stop();
            controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        }
        catch (InvalidOperationException)
        {
            // Service may not exist yet.
        }
        catch (Exception ex)
        {
            logger.Warn($"Failed to stop service '{serviceName}' before uninstall: {ex.Message}");
        }
    }

    private static string BuildBinPath(string exePath, string serviceName, string root, string startupScript, string? scriptArgs)
    {
        var builder = new StringBuilder();
        builder.Append('"').Append(exePath).Append("\" service --name ").Append('"').Append(serviceName).Append('"');
        builder.Append(" --root ").Append('"').Append(root).Append('"');
        if (!string.IsNullOrWhiteSpace(startupScript))
        {
            builder.Append(" --startup ").Append('"').Append(startupScript).Append('"');
        }

        if (!string.IsNullOrWhiteSpace(scriptArgs))
        {
            builder.Append(" --scriptArgs ").Append('"').Append(EscapeQuotes(scriptArgs)).Append('"');
        }

        return builder.ToString();
    }

    private static (int ExitCode, string Output, string Error) RunSc(string arguments)
    {
        var startInfo = new ProcessStartInfo("sc.exe", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start sc.exe");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }

    private static void LogScOutput(SimpleLogger logger, string operation, (int ExitCode, string Output, string Error) result)
    {
        logger.Info($"sc.exe {operation} exited with code {result.ExitCode}. Output: {result.Output}".Trim());
        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            logger.Warn($"sc.exe {operation} error: {result.Error}");
        }
    }

    private static string EscapeQuotes(string input) => input.Replace("\"", "\\\"");
}
