using System.Text;

namespace Kestrun.Launcher.Logging;

internal sealed class SimpleLogger
{
    private readonly string _filePath;
    private readonly bool _echoToConsole;
    private readonly object _sync = new();

    private SimpleLogger(string filePath, bool echoToConsole)
    {
        _filePath = filePath;
        _echoToConsole = echoToConsole;
    }

    public static SimpleLogger Create(string? logFilePath = null, bool echoToConsole = true)
    {
        var resolvedPath = logFilePath ?? Path.Combine(AppContext.BaseDirectory, "kestrun-launcher.log");
        return new SimpleLogger(resolvedPath, echoToConsole);
    }

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(string message, Exception? exception = null)
    {
        var builder = new StringBuilder(message);
        if (exception is not null)
        {
            builder.Append(": ").Append(exception);
        }

        Write("ERROR", builder.ToString());
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:O} [{level}] {message}";
        lock (_sync)
        {
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.AppendAllText(_filePath, line + Environment.NewLine);
            }
            catch
            {
                // Ignore logging failures
            }
        }

        if (_echoToConsole)
        {
            try
            {
                Console.WriteLine(line);
            }
            catch
            {
                // Ignore console failures (e.g., when running as a Windows service)
            }
        }
    }
}
