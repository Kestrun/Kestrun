using System.Globalization;
using Serilog;
using Serilog.Events;

namespace Kestrun.Health;

/// <summary>
/// Probe that reports free disk space for a target drive / mount point.
/// </summary>
/// <remarks>
/// By default it inspects the drive containing the current process executable (AppContext.BaseDirectory).
/// Status mapping (unless overridden):
///  Healthy: free percent greater-or-equal to warn threshold (default 10%)
///  Degraded: free percent between critical and warn thresholds (default 5% - 10%)
///  Unhealthy: free percent below critical threshold (default under 5%)
/// On error (e.g., drive missing) the probe returns Unhealthy with the exception message.
/// </remarks>
public sealed class DiskSpaceProbe : IProbe
{
    private readonly string _path;
    private readonly double _criticalPercent;
    private readonly double _warnPercent;
    private readonly Serilog.ILogger _logger;

    /// <summary>
    /// Creates a new <see cref="DiskSpaceProbe"/>.
    /// </summary>
    /// <param name="name">Probe name (e.g., "disk").</param>
    /// <param name="tags">Probe tags (e.g., ["live"], ["ready"]).</param>
    /// <param name="path">Directory path whose containing drive should be measured. Defaults to AppContext.BaseDirectory.</param>
    /// <param name="criticalPercent">Below this free percentage the probe is Unhealthy. Default 5.</param>
    /// <param name="warnPercent">Below this free percentage (but above critical) the probe is Degraded. Default 10.</param>
    /// <param name="logger">Optional logger; if null a context logger is created.</param>
    /// <exception cref="ArgumentException">Thrown when thresholds are invalid.</exception>
    public DiskSpaceProbe(
        string name,
        string[] tags,
        string? path = null,
        double criticalPercent = 5,
        double warnPercent = 10,
        Serilog.ILogger? logger = null)
    {
        if (criticalPercent <= 0 || warnPercent <= 0 || warnPercent <= criticalPercent || warnPercent > 100)
        {
            throw new ArgumentException("Invalid threshold configuration. Must satisfy: 0 < critical < warn <= 100.");
        }

        Name = name;
        Tags = tags;
        _path = string.IsNullOrWhiteSpace(path) ? AppContext.BaseDirectory : path!;
        _criticalPercent = criticalPercent;
        _warnPercent = warnPercent;
        _logger = logger ?? Log.ForContext("HealthProbe", name).ForContext("Probe", name);
        Logger = _logger; // expose via interface
    }

    /// <summary>
    /// Probe name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Probe tags used for filtering.
    /// </summary>
    public string[] Tags { get; }

    /// <inheritdoc />
    public Serilog.ILogger Logger { get; init; }

    /// <summary>
    /// Executes the disk space check.
    /// </summary>
    public Task<ProbeResult> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            // Resolve drive info
            var drive = ResolveDrive(_path);
            if (drive is null)
            {
                if (Logger.IsEnabled(LogEventLevel.Debug))
                {
                    Logger.Debug("DiskSpaceProbe {Probe} drive not found for path {Path}", Name, _path);
                }
                return Task.FromResult(new ProbeResult(ProbeStatus.Unhealthy, $"Drive not found for path '{_path}'."));
            }

            if (!drive.IsReady)
            {
                return Task.FromResult(new ProbeResult(ProbeStatus.Unhealthy, $"Drive '{drive.Name}' is not ready."));
            }

            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("DiskSpaceProbe {Probe} checking drive {Drive}", Name, drive.Name);
                Logger.Debug("DiskSpaceProbe {Probe} drive is ready {Drive}", Name, drive.Name);
            }

            var total = drive.TotalSize; // bytes
            var free = drive.AvailableFreeSpace; // bytes (user-available)
            if (total <= 0)
            {
                return Task.FromResult(new ProbeResult(ProbeStatus.Unhealthy, $"Drive '{drive.Name}' total size reported as 0."));
            }

            var freePercent = (double)free / total * 100.0;
            var status = freePercent < _criticalPercent
                ? ProbeStatus.Unhealthy
                : freePercent < _warnPercent
                    ? ProbeStatus.Degraded
                    : ProbeStatus.Healthy;

            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("DiskSpaceProbe {Probe} free percent={Percent:F1}", Name, freePercent);
            }

            var data = new Dictionary<string, object>
            {
                ["path"] = _path,
                ["driveName"] = drive.Name,
                ["totalBytes"] = total,
                ["freeBytes"] = free,
                ["freePercent"] = Math.Round(freePercent, 2),
                ["criticalPercent"] = _criticalPercent,
                ["warnPercent"] = _warnPercent
            };

            var desc = $"Free {FormatBytes(free)} of {FormatBytes(total)} ({freePercent:F2}% free)";

            return Task.FromResult(new ProbeResult(status, desc, data));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Task.FromResult(new ProbeResult(ProbeStatus.Degraded, "Canceled", new Dictionary<string, object> { ["path"] = _path }));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "DiskSpaceProbe {Probe} failed", Name);
            _logger.Warning(ex, "Disk space probe failed for path {Path}", _path);
            return Task.FromResult(new ProbeResult(ProbeStatus.Unhealthy, ex.Message));
        }
    }

    private static DriveInfo? ResolveDrive(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }
            var root = Path.GetPathRoot(path);
            return string.IsNullOrEmpty(root) ? null : new DriveInfo(root);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB", "PB"]; // improbable > PB
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return string.Create(CultureInfo.InvariantCulture, $"{len:0.##} {sizes[order]}");
    }
}
