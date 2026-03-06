using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Text;

namespace Kestrun.ServiceHost;

internal static class Program
{
    private sealed record ParsedOptions(
        string ServiceName,
        string RunnerExecutablePath,
        string ScriptPath,
        string ModuleManifestPath,
        string[] ScriptArguments,
        string? ServiceLogPath);

    private static int Main(string[] args)
    {
        if (!TryParseArguments(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 2;
        }
        // If running on Windows and not in an interactive session, run as a Windows Service.
        if (OperatingSystem.IsWindows() && !Environment.UserInteractive)
        {
            return RunWindowsService(options!);
        }
        // For non-Windows or interactive sessions, run as a foreground daemon.
        return RunForegroundDaemon(options!);
    }

    [SupportedOSPlatform("windows")]
    private static int RunWindowsService(ParsedOptions options)
    {
        ServiceBase.Run(new KestrunWindowsService(options));
        return 0;
    }

    private static bool TryParseArguments(string[] args, out ParsedOptions? options, out string error)
    {
        options = null;
        error = string.Empty;

        var serviceName = string.Empty;
        var runnerExecutablePath = string.Empty;
        var scriptPath = string.Empty;
        var moduleManifestPath = string.Empty;
        var scriptArguments = Array.Empty<string>();
        string? serviceLogPath = null;

        var index = 0;
        while (index < args.Length)
        {
            var current = args[index];
            if (current is "--name" && index + 1 < args.Length)
            {
                serviceName = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--runner-exe" && index + 1 < args.Length)
            {
                runnerExecutablePath = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--script" && index + 1 < args.Length)
            {
                scriptPath = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--kestrun-manifest" && index + 1 < args.Length)
            {
                moduleManifestPath = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--service-log-path" && index + 1 < args.Length)
            {
                serviceLogPath = args[index + 1];
                index += 2;
                continue;
            }

            if (current is "--arguments" or "--")
            {
                scriptArguments = [.. args.Skip(index + 1)];
                break;
            }

            error = $"Unknown option: {current}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            error = "Missing --name.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(runnerExecutablePath))
        {
            error = "Missing --runner-exe.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            error = "Missing --script.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(moduleManifestPath))
        {
            error = "Missing --kestrun-manifest.";
            return false;
        }

        options = new ParsedOptions(
            serviceName,
            Path.GetFullPath(runnerExecutablePath),
            Path.GetFullPath(scriptPath),
            Path.GetFullPath(moduleManifestPath),
            scriptArguments,
            serviceLogPath);
        return true;
    }

    private static void PrintUsage() => Console.WriteLine("Usage: kestrun-service-host --name <service> --runner-exe <path> --script <path> --kestrun-manifest <path> [--service-log-path <path>] [--arguments ...]");

    private static int RunForegroundDaemon(ParsedOptions options)
    {
        var logPath = ResolveBootstrapLogPath(options.ServiceLogPath);
        var host = new RunnerProcessHost(options, logPath);

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (!shutdown.IsCancellationRequested)
            {
                shutdown.Cancel();
            }
        };

        host.WriteBootstrapLog($"Daemon '{options.ServiceName}' starting.");

        var startCode = host.Start();
        if (startCode != 0)
        {
            return startCode;
        }

        while (!shutdown.IsCancellationRequested)
        {
            if (host.HasExited)
            {
                var exitCode = host.ExitCode;
                host.WriteBootstrapLog($"Runner process exited with code {exitCode}.");
                return exitCode;
            }

            Thread.Sleep(250);
        }

        host.WriteBootstrapLog($"Daemon '{options.ServiceName}' stopping.");
        host.Stop();
        host.WriteBootstrapLog($"Daemon '{options.ServiceName}' stopped.");
        return 0;
    }

    [SupportedOSPlatform("windows")]
    private sealed class KestrunWindowsService : ServiceBase
    {
        private readonly RunnerProcessHost _host;

        public KestrunWindowsService(ParsedOptions options)
        {
            ServiceName = options.ServiceName;
            CanStop = true;
            AutoLog = true;
            _host = new RunnerProcessHost(options, ResolveBootstrapLogPath(options.ServiceLogPath));
        }

        protected override void OnStart(string[] args)
        {
            _host.WriteBootstrapLog($"Service '{ServiceName}' starting.");
            var exitCode = _host.Start();
            if (exitCode != 0)
            {
                ExitCode = exitCode;
                Stop();
                return;
            }

            _host.RegisterOnExit(code =>
            {
                _host.WriteBootstrapLog($"Runner process exited with code {code}.");
                ExitCode = code;
                Stop();
            });
        }

        protected override void OnStop()
        {
            _host.WriteBootstrapLog($"Service '{ServiceName}' stopping.");
            _host.Stop();
            _host.WriteBootstrapLog($"Service '{ServiceName}' stopped.");
        }
    }

    private sealed class RunnerProcessHost
    {
        private readonly ParsedOptions _options;
        private readonly string _bootstrapLogDirectory;
        private readonly string _bootstrapLogPath;
        private Process? _runnerProcess;

        public RunnerProcessHost(ParsedOptions options, string bootstrapLogPath)
        {
            _options = options;
            _bootstrapLogPath = bootstrapLogPath;
            _bootstrapLogDirectory = Path.GetDirectoryName(_bootstrapLogPath) ?? Path.GetTempPath();
        }

        public bool HasExited => _runnerProcess is not null && _runnerProcess.HasExited;

        public int ExitCode => _runnerProcess?.HasExited == true ? _runnerProcess.ExitCode : 0;

        public int Start()
        {
            if (!File.Exists(_options.RunnerExecutablePath))
            {
                WriteBootstrapLog($"Runner executable not found: {_options.RunnerExecutablePath}");
                return 2;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _options.RunnerExecutablePath,
                Arguments = string.Join(" ", BuildRunnerArguments(_options.ScriptPath, _options.ModuleManifestPath, _options.ScriptArguments).Select(EscapeWindowsCommandLineArgument)),
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(_options.ScriptPath) ?? Environment.CurrentDirectory,
            };

            try
            {
                _runnerProcess = Process.Start(startInfo);
                if (_runnerProcess is null)
                {
                    WriteBootstrapLog("Failed to start runner process.");
                    return 1;
                }

                return 0;
            }
            catch (Exception ex)
            {
                WriteBootstrapLog($"Failed to start runner process: {ex}");
                return 1;
            }
        }

        public void RegisterOnExit(Action<int> onExit)
        {
            if (_runnerProcess is null)
            {
                return;
            }

            _runnerProcess.EnableRaisingEvents = true;
            _runnerProcess.Exited += (_, _) => onExit(_runnerProcess.ExitCode);
        }

        public void Stop()
        {
            try
            {
                if (_runnerProcess is { HasExited: false })
                {
                    _runnerProcess.Kill(entireProcessTree: true);
                    _ = _runnerProcess.WaitForExit(15000);
                }
            }
            catch (Win32Exception ex)
            {
                WriteBootstrapLog($"Failed to stop runner process: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                WriteBootstrapLog($"Failed to stop runner process: {ex.Message}");
            }
        }

        public void WriteBootstrapLog(string message)
        {
            try
            {
                _ = Directory.CreateDirectory(_bootstrapLogDirectory);
                var line = $"{DateTime.UtcNow:O} {message}{Environment.NewLine}";
                File.AppendAllText(_bootstrapLogPath, line, Encoding.UTF8);
            }
            catch
            {
                // Best-effort logging only.
            }
        }
    }

    private static IReadOnlyList<string> BuildRunnerArguments(string scriptPath, string moduleManifestPath, IReadOnlyList<string> scriptArguments)
    {
        var arguments = new List<string>(8 + scriptArguments.Count)
        {
            "--kestrun-manifest",
            moduleManifestPath,
            "run",
            scriptPath,
        };

        if (scriptArguments.Count > 0)
        {
            arguments.Add("--arguments");
            arguments.AddRange(scriptArguments);
        }

        return arguments;
    }

    private static string EscapeWindowsCommandLineArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        var needsQuotes = value.Any(char.IsWhiteSpace) || value.Contains('"');
        if (!needsQuotes)
        {
            return value;
        }

        var escaped = value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static string ResolveBootstrapLogPath(string? configuredPath)
    {
        var defaultDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Kestrun",
            "logs");
        var defaultPath = Path.Combine(defaultDirectory, "script-runner-service.log");

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return defaultPath;
        }

        var fullPath = Path.GetFullPath(configuredPath);
        return Directory.Exists(fullPath)
            || configuredPath.EndsWith('\\')
            || configuredPath.EndsWith('/')
            ? Path.Combine(fullPath, "script-runner-service.log")
            : fullPath;
    }
}
