using Kestrun.Hosting;
using Xunit;

namespace KestrunTests.Hosting;

public class KestrunHostRuntimeTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void Uptime_ReturnsNull_WhenStartTimeNotSet()
    {
        var runtime = new KestrunHostRuntime();
        Assert.Null(runtime.Uptime);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Uptime_UsesStopTime_WhenStopped()
    {
        var runtime = new KestrunHostRuntime
        {
            StartTime = new DateTime(2020, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            StopTime = new DateTime(2020, 01, 01, 0, 0, 10, DateTimeKind.Utc)
        };

        Assert.Equal(TimeSpan.FromSeconds(10), runtime.Uptime);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Uptime_UsesUtcNow_WhenRunning()
    {
        var runtime = new KestrunHostRuntime
        {
            StartTime = DateTime.UtcNow - TimeSpan.FromSeconds(5)
        };

        var uptime = runtime.Uptime;
        _ = Assert.NotNull(uptime);
        Assert.InRange(uptime.Value.TotalSeconds, 4, 10);
    }
}
