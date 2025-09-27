using Kestrun.Health;
using Xunit;

namespace KestrunTests.Health;

public class DiskSpaceProbeTests
{
    private static (double freePercent, DriveInfo drive) GetDriveInfo()
    {
        var path = AppContext.BaseDirectory;
        var drive = new DriveInfo(Path.GetPathRoot(path)!);
        var freePercent = (double)drive.AvailableFreeSpace / drive.TotalSize * 100.0;
        return (freePercent, drive);
    }

    [Fact]
    public async Task DiskSpaceProbe_DynamicThresholds_ClassifyStatuses()
    {
        var (freePercent, _) = GetDriveInfo();
        // Healthy thresholds: both below actual free percent
        var hCritical = Math.Max(0.1, freePercent / 10);
        var hWarn = Math.Max(hCritical + 0.1, hCritical * 1.5);
        if (hWarn >= freePercent)
        {
            hWarn = Math.Max(0.1, freePercent - 0.5);
        }

        var healthy = await new DiskSpaceProbe("disk-h", ["live"], AppContext.BaseDirectory, hCritical, hWarn).CheckAsync();
        Assert.Equal(ProbeStatus.Healthy, healthy.Status);

        // Degraded: critical below free < warn > free
        var dCritical = Math.Max(0.1, freePercent - 5);
        if (dCritical <= 0)
        {
            dCritical = 0.1;
        }

        var dWarn = Math.Min(100, freePercent + 1);
        if (!(dCritical < freePercent && dWarn > freePercent))
        {
            // If we cannot satisfy invariant (e.g., extremely low or high free%), just assert healthy case executed.
            return;
        }
        var degraded = await new DiskSpaceProbe("disk-d", ["live"], AppContext.BaseDirectory, dCritical, dWarn).CheckAsync();
        Assert.Equal(ProbeStatus.Degraded, degraded.Status);

        // Unhealthy: critical above freePercent
        var uCritical = Math.Min(99, freePercent + 1);
        var uWarn = Math.Min(100, uCritical + 1);
        if (uCritical > 98)
        {
            return; // avoid invalid thresholds near 100
        }

        var unhealthy = await new DiskSpaceProbe("disk-u", ["live"], AppContext.BaseDirectory, uCritical, uWarn).CheckAsync();
        Assert.Equal(ProbeStatus.Unhealthy, unhealthy.Status);
    }
}
