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
    [Trait("Category", "Health")]
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

    [Fact]
    [Trait("Category", "Health")]
    public void Constructor_WithValidParameters_CreatesProbe()
    {
        // Arrange & Act
        var probe = new DiskSpaceProbe("disk-check", ["ready"], AppContext.BaseDirectory, 5.0, 10.0);

        // Assert
        Assert.Equal("disk-check", probe.Name);
        _ = Assert.Single(probe.Tags);
        Assert.Equal("ready", probe.Tags[0]);
        Assert.NotNull(probe.Logger);
    }

    [Fact]
    [Trait("Category", "Health")]
    public void Constructor_WithInvalidThresholds_ThrowsArgumentException()
    {
        // Critical equals warn
        var ex1 = Assert.Throws<ArgumentException>(() =>
            new DiskSpaceProbe("test", [], criticalPercent: 10.0, warnPercent: 10.0));
        Assert.Contains("Invalid threshold", ex1.Message);

        // Critical greater than warn
        var ex2 = Assert.Throws<ArgumentException>(() =>
            new DiskSpaceProbe("test", [], criticalPercent: 15.0, warnPercent: 10.0));
        Assert.Contains("Invalid threshold", ex2.Message);

        // Warn over 100
        var ex3 = Assert.Throws<ArgumentException>(() =>
            new DiskSpaceProbe("test", [], criticalPercent: 5.0, warnPercent: 110.0));
        Assert.Contains("Invalid threshold", ex3.Message);

        // Critical zero or negative
        var ex4 = Assert.Throws<ArgumentException>(() =>
            new DiskSpaceProbe("test", [], criticalPercent: 0.0, warnPercent: 10.0));
        Assert.Contains("Invalid threshold", ex4.Message);
    }

    [Fact]
    [Trait("Category", "Health")]
    public async Task CheckAsync_WithValidDrive_ReturnsDataDictionary()
    {
        // Arrange
        var probe = new DiskSpaceProbe("disk-check", [], AppContext.BaseDirectory, 0.1, 1.0);

        // Act
        var result = await probe.CheckAsync();

        // Assert
        Assert.NotNull(result.Data);
        Assert.True(result.Data.ContainsKey("path"));
        Assert.True(result.Data.ContainsKey("driveName"));
        Assert.True(result.Data.ContainsKey("totalBytes"));
        Assert.True(result.Data.ContainsKey("freeBytes"));
        Assert.True(result.Data.ContainsKey("freePercent"));
        Assert.True(result.Data.ContainsKey("criticalPercent"));
        Assert.True(result.Data.ContainsKey("warnPercent"));
    }

    [Fact]
    [Trait("Category", "Health")]
    public async Task CheckAsync_WithValidDrive_IncludesDescription()
    {
        // Arrange
        var probe = new DiskSpaceProbe("disk-check", [], AppContext.BaseDirectory);

        // Act
        var result = await probe.CheckAsync();

        // Assert
        Assert.NotNull(result.Description);
        Assert.Contains("Free", result.Description);
        Assert.Contains("free)", result.Description); // percentage
    }

    [Fact]
    [Trait("Category", "Health")]
    public async Task CheckAsync_WithInvalidPath_ReturnsUnhealthyStatus()
    {
        // Arrange - using a truly invalid path (UNC path without server, which has no drive root)
        // This ensures GetPathRoot returns null or empty, causing ResolveDrive to return null
        var probe = new DiskSpaceProbe("disk-check", [], "\\invalid");

        // Act
        var result = await probe.CheckAsync();

        // Assert
        Assert.Equal(ProbeStatus.Unhealthy, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Description));
    }

    [Fact]
    [Trait("Category", "Health")]
    public async Task CheckAsync_DataContainsCorrectTypes()
    {
        // Arrange
        var probe = new DiskSpaceProbe("disk-check", [], AppContext.BaseDirectory);

        // Act
        var result = await probe.CheckAsync();

        // Assert
        if (result.Data is not null)
        {
            _ = Assert.IsType<string>(result.Data["path"]);
            _ = Assert.IsType<string>(result.Data["driveName"]);
            _ = Assert.IsType<long>(result.Data["totalBytes"]);
            _ = Assert.IsType<long>(result.Data["freeBytes"]);
            Assert.True(result.Data["freePercent"] is double);
            Assert.True(result.Data["criticalPercent"] is double);
            Assert.True(result.Data["warnPercent"] is double);
        }
    }

    [Fact]
    [Trait("Category", "Health")]
    public async Task CheckAsync_FreePercentIsRounded()
    {
        // Arrange
        var probe = new DiskSpaceProbe("disk-check", [], AppContext.BaseDirectory);

        // Act
        var result = await probe.CheckAsync();

        // Assert
        if (result.Data is not null && result.Data.TryGetValue("freePercent", out var freePercentObj))
        {
            var freePercent = (double)freePercentObj;
            // Check that it's rounded to 2 decimal places
            var rounded = Math.Round(freePercent, 2);
            Assert.Equal(rounded, freePercent);
        }
    }

    [Fact]
    [Trait("Category", "Health")]
    public async Task CheckAsync_ThresholdValuesMatchConstructor()
    {
        // Arrange
        var probe = new DiskSpaceProbe("disk-check", [], AppContext.BaseDirectory, 3.0, 7.0);

        // Act
        var result = await probe.CheckAsync();

        // Assert
        Assert.NotNull(result.Data);
        Assert.Equal(3.0, (double)result.Data["criticalPercent"]);
        Assert.Equal(7.0, (double)result.Data["warnPercent"]);
    }
}
