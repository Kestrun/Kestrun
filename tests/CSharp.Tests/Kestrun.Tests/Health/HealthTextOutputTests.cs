using Kestrun.Health;
using Kestrun.Hosting;
using Kestrun.Models;
using Kestrun.Scripting;
using Xunit;

namespace KestrunTests.Health;

public class HealthTextOutputTests
{
    private sealed class StaticProbe(string name, ProbeResult result, string[]? tags = null) : IProbe
    {
        public string Name { get; } = name;
        public string[] Tags { get; } = tags ?? [];
        private readonly ProbeResult _result = result;
        public Serilog.ILogger Logger { get; init; } = Serilog.Log.ForContext("HealthProbe", name).ForContext("Probe", name);
        public Task<ProbeResult> CheckAsync(CancellationToken ct = default) => Task.FromResult(_result);
    }

    [Fact]
    public async Task TextFormatter_EmitsExpectedMarkers()
    {
        var probes = new List<IProbe>
        {
            new StaticProbe("alpha", new ProbeResult(ProbeStatus.Healthy, "ok", new Dictionary<string, object>{{"latencyMs", 12}})),
            new StaticProbe("beta", new ProbeResult(ProbeStatus.Degraded, "warn", new Dictionary<string, object>{{"queueDepth", 5}}))
        };
        var report = await HealthProbeRunner.RunAsync(
            probes,
            [],
            TimeSpan.FromSeconds(1),
            0,
            Serilog.Log.Logger,
            CancellationToken.None);

        var text = HealthReportTextFormatter.Format(report);

        Assert.Contains("Status: ", text);
        Assert.Contains("Probes:", text);
        Assert.Contains("name=alpha", text);
        Assert.Contains("name=beta", text);
        Assert.Contains("latencyMs=12", text);
        Assert.Contains("queueDepth=5", text);
        // Ensure degraded probe shows status token
        Assert.Contains("status=degraded", text);
    }
}
