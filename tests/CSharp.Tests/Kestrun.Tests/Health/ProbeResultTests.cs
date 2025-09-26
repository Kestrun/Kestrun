using Kestrun.Health;
using Xunit;

namespace KestrunTests.Health;

public class ProbeResultTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        var data = new Dictionary<string, object> { { "key", 42 } };
        var result = new ProbeResult(ProbeStatus.Healthy, "desc", data); // No change needed, keeping for context
        Assert.Equal(ProbeStatus.Healthy, result.Status);
        Assert.Equal("desc", result.Description);
        Assert.Equal(data, result.Data);
    }

    [Fact]
    public void StatusEnum_ValuesAreCorrect()
    {
        Assert.Equal(0, (int)ProbeStatus.Healthy);
        Assert.Equal(1, (int)ProbeStatus.Degraded);
        Assert.Equal(2, (int)ProbeStatus.Unhealthy);
    }
}
