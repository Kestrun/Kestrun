using Kestrun.Health;
using Xunit;

namespace KestrunTests.Health;

public class HealthProbeRunnerTests
{
    private class DummyProbe(string name, ProbeResult result, string[]? tags = null) : IProbe
    {
        public string Name { get; } = name;
        public string[] Tags { get; } = tags ?? ["core", "test"];
        private readonly ProbeResult _result = result;

        public Task<ProbeResult> CheckAsync(CancellationToken ct = default) => Task.FromResult(_result);
    }

    [Fact]
    public async Task RunAsync_AggregatesStatusesCorrectly()
    {
        var probes = new List<IProbe>
        {
            new DummyProbe("p1", new ProbeResult(ProbeStatus.Healthy)),
            new DummyProbe("p2", new ProbeResult(ProbeStatus.Degraded)),
            new DummyProbe("p3", new ProbeResult(ProbeStatus.Unhealthy))
        };
        var report = await HealthProbeRunner.RunAsync(
            probes,
            [],
            TimeSpan.FromMilliseconds(500),
            0,
            Serilog.Log.Logger,
            CancellationToken.None);
        Assert.Equal(ProbeStatus.Unhealthy, report.Status);
        Assert.Equal(3, report.Probes.Count);
    }

    [Fact]
    public async Task RunAsync_TagFilteringWorks()
    {
        var probes = new List<IProbe>
        {
            new DummyProbe("p1", new ProbeResult(ProbeStatus.Healthy), ["core", "test"]),
            new DummyProbe("p2", new ProbeResult(ProbeStatus.Degraded), ["core", "test"]),
        };
        var report = await HealthProbeRunner.RunAsync(
            probes,
            ["core"],
            TimeSpan.FromMilliseconds(500),
            0,
            Serilog.Log.Logger,
            CancellationToken.None);
        Assert.Equal(2, report.Probes.Count);
        report = await HealthProbeRunner.RunAsync(
            probes,
            ["test"],
            TimeSpan.FromMilliseconds(500),
            0,
            Serilog.Log.Logger,
            CancellationToken.None);
        Assert.Equal(2, report.Probes.Count);
        report = await HealthProbeRunner.RunAsync(
            probes,
            ["missing"],
            TimeSpan.FromMilliseconds(500),
            0,
            Serilog.Log.Logger,
            CancellationToken.None);
        Assert.Empty(report.Probes);
    }

    [Fact]
    public async Task RunAsync_TimeoutProducesUnhealthy()
    {
        var probes = new List<IProbe>
        {
            new TimeoutProbe()
        };
        var report = await HealthProbeRunner.RunAsync(
            probes,
            [],
            TimeSpan.FromMilliseconds(10),
            0,
            Serilog.Log.Logger,
            CancellationToken.None);
        Assert.Equal(ProbeStatus.Unhealthy, report.Status);
        Assert.Contains(report.Probes, r => r.Status == ProbeStatus.Unhealthy && r.Description?.Contains("Timed out") == true);
    }

    private class TimeoutProbe : IProbe
    {
        public string Name => "timeout";
        public string[] Tags => ["core"];
        public async Task<ProbeResult> CheckAsync(CancellationToken ct = default)
        {
            await Task.Delay(100, ct);
            return new ProbeResult(ProbeStatus.Healthy);
        }
    }
}
