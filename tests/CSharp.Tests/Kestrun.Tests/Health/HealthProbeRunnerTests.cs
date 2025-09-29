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
        public Serilog.ILogger Logger { get; init; } = Serilog.Log.ForContext("HealthProbe", name).ForContext("Probe", name);

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
    public async Task RunAsync_TimeoutProducesDegraded()
    {
        var probes = new List<IProbe>
        {
            new TimeoutProbe()
        };
        var report = await HealthProbeRunner.RunAsync(
            probes,
            [],
            // Use a slightly larger timeout window to avoid timing granularity issues on CI runners.
            TimeSpan.FromMilliseconds(100),
            0,
            Serilog.Log.Logger,
            CancellationToken.None);
        // Design decision: individual probe timeouts are considered Degraded (transient) not outright Unhealthy.
        Assert.Equal(ProbeStatus.Degraded, report.Status);
        Assert.Contains(report.Probes, r => r.Status == ProbeStatus.Degraded && r.Description?.Contains("Timed out") == true);
    }

    private class TimeoutProbe : IProbe
    {
        public string Name => "timeout";
        public string[] Tags => ["core"];
        public Serilog.ILogger Logger { get; init; } = Serilog.Log.ForContext("HealthProbe", "timeout").ForContext("Probe", "timeout");
        public async Task<ProbeResult> CheckAsync(CancellationToken ct = default)
        {
            // Intentionally wait forever (until cancelled) to deterministically trigger the per-probe timeout.
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            // We should never get here because the token is expected to cancel first.
            return new ProbeResult(ProbeStatus.Healthy, "Completed unexpectedly");
        }
    }

    private sealed class StaticProbe(string name, ProbeResult result, string[]? tags = null, TimeSpan? delay = null, Exception? ex = null) : IProbe
    {
        public string Name { get; } = name;
        public string[] Tags { get; } = tags ?? [];
        private readonly ProbeResult _result = result;
        private readonly TimeSpan _delay = delay ?? TimeSpan.Zero;
        private readonly Exception? _throw = ex;
        public Serilog.ILogger Logger { get; init; } = Serilog.Log.ForContext("HealthProbe", name).ForContext("Probe", name);

        public async Task<ProbeResult> CheckAsync(CancellationToken ct = default)
        {
            if (_throw is not null)
            {
                throw _throw;
            }
            if (_delay > TimeSpan.Zero)
            {
                await Task.Delay(_delay, ct);
            }
            return _result;
        }
    }

    [Fact]
    public async Task EmptyProbeList_ReturnsHealthyWithEmptySummary()
    {
        var report = await HealthProbeRunner.RunAsync(
            [],
            [],
            TimeSpan.FromSeconds(1),
            0,
            Serilog.Log.Logger,
            CancellationToken.None);
        Assert.Equal(ProbeStatus.Healthy, report.Status);
        Assert.Empty(report.Probes);
        Assert.Equal(0, report.Summary.Total);
        Assert.Empty(report.AppliedTags);
    }

    [Fact]
    public async Task TagNormalization_TrimsAndDeduplicates()
    {
        var probes = new List<IProbe>
        {
            new StaticProbe("p1", new ProbeResult(ProbeStatus.Healthy), ["Live"]),
            new StaticProbe("p2", new ProbeResult(ProbeStatus.Healthy), ["ready"])
        };
        var tags = new List<string> { " live ", "LIVE", "Ready" };
        var report = await HealthProbeRunner.RunAsync(
            probes,
            tags,
            TimeSpan.FromSeconds(1),
            0,
            Serilog.Log.Logger,
            CancellationToken.None);
        Assert.Equal(2, report.Probes.Count); // both matched after normalization
        Assert.All(report.AppliedTags, t => Assert.Equal(t, t.Trim()));
        Assert.Equal(2, report.AppliedTags.Count); // live, ready
    }

    [Fact]
    public async Task SummaryCounts_AreAccurate()
    {
        var probes = new List<IProbe>
        {
            new StaticProbe("h", new ProbeResult(ProbeStatus.Healthy)),
            new StaticProbe("d", new ProbeResult(ProbeStatus.Degraded)),
            new StaticProbe("u", new ProbeResult(ProbeStatus.Unhealthy))
        };
        var report = await HealthProbeRunner.RunAsync(
            probes,
            [],
            TimeSpan.FromSeconds(1),
            0,
            Serilog.Log.Logger,
            CancellationToken.None);
        Assert.Equal(3, report.Summary.Total);
        Assert.Equal(1, report.Summary.Healthy);
        Assert.Equal(1, report.Summary.Degraded);
        Assert.Equal(1, report.Summary.Unhealthy);
    }

    [Fact]
    public async Task AppliedTags_AreReturnedNormalized()
    {
        var probes = new List<IProbe>
        {
            new StaticProbe("p1", new ProbeResult(ProbeStatus.Healthy), ["X"]),
        };
        var report = await HealthProbeRunner.RunAsync(
            probes,
            [" x ", "X"],
            TimeSpan.FromSeconds(1),
            0,
            Serilog.Log.Logger,
            CancellationToken.None);
        _ = Assert.Single(report.Probes);
        _ = Assert.Single(report.AppliedTags);
        Assert.Equal("x", report.AppliedTags[0]);
    }

    [Fact]
    public async Task TimeoutDisabled_AllowsLongRunningProbe()
    {
        var probes = new List<IProbe>
        {
            new StaticProbe("slow", new ProbeResult(ProbeStatus.Healthy), delay: TimeSpan.FromMilliseconds(150))
        };
        var report = await HealthProbeRunner.RunAsync(
            probes,
            [],
            TimeSpan.Zero, // disable per-probe timeout
            0,
            Serilog.Log.Logger,
            CancellationToken.None);
        Assert.Equal(ProbeStatus.Healthy, report.Status);
        _ = Assert.Single(report.Probes);
        Assert.True(report.Probes[0].Duration >= TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task ExceptionProbe_IsCapturedWithErrorAndUnhealthy()
    {
        var probes = new List<IProbe>
        {
            new StaticProbe("boom", new ProbeResult(ProbeStatus.Healthy), ex: new InvalidOperationException("bad"))
        };
        var report = await HealthProbeRunner.RunAsync(
            probes,
            [],
            TimeSpan.FromSeconds(1),
            0,
            Serilog.Log.Logger,
            CancellationToken.None);
        Assert.Equal(ProbeStatus.Unhealthy, report.Status);
        var entry = Assert.Single(report.Probes);
        Assert.Equal("boom", entry.Name);
        Assert.Equal(ProbeStatus.Unhealthy, entry.Status);
        Assert.NotNull(entry.Error);
        Assert.Contains("bad", entry.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Exception:", entry.Description);
    }

    [Fact]
    public async Task MaxDegreeOfParallelism_ThrottlesExecutionTime()
    {
        // Deterministic test: use a probe that records concurrent executions instead of timing assumptions.
        var tracker = new ConcurrencyTracker();
        var probes = new List<IProbe>();
        for (var i = 0; i < 8; i++)
        {
            probes.Add(new CountingProbe($"p{i}", tracker, delay: TimeSpan.FromMilliseconds(40)));
        }

        // Run with no throttle (expect observed concurrency >= 6, likely 8 on thread pool)
        _ = await HealthProbeRunner.RunAsync(
            probes,
            [],
            TimeSpan.FromSeconds(2),
            0,
            Serilog.Log.Logger,
            CancellationToken.None);
        var unboundedMax = tracker.MaxObservedConcurrency;

        // Reset tracker and run throttled at 2
        tracker.Reset();
        _ = await HealthProbeRunner.RunAsync(
            probes,
            [],
            TimeSpan.FromSeconds(2),
            2,
            Serilog.Log.Logger,
            CancellationToken.None);
        var throttledMax = tracker.MaxObservedConcurrency;

        Assert.True(unboundedMax >= 4, $"Expected unbounded concurrency >=4 but was {unboundedMax}");
        Assert.InRange(throttledMax, 1, 2); // should never exceed throttle
        Assert.True(unboundedMax > throttledMax, $"Expected throttled max ({throttledMax}) to be less than unbounded ({unboundedMax})");
    }
}

internal sealed class ConcurrencyTracker
{
    private int _current;
    private int _max;
    public int MaxObservedConcurrency => Volatile.Read(ref _max);
    public void Enter()
    {
        var cur = Interlocked.Increment(ref _current);
        var prevMax = Volatile.Read(ref _max);
        if (cur > prevMax)
        {
            _ = Interlocked.CompareExchange(ref _max, cur, prevMax);
        }
    }
    public void Exit() => Interlocked.Decrement(ref _current);
    public void Reset()
    {
        Volatile.Write(ref _current, 0);
        Volatile.Write(ref _max, 0);
    }
}

internal sealed class CountingProbe(string name, ConcurrencyTracker tracker, TimeSpan? delay = null, string[]? tags = null) : IProbe
{
    private readonly ConcurrencyTracker _tracker = tracker;
    private readonly TimeSpan _delay = delay ?? TimeSpan.FromMilliseconds(20);

    public string Name { get; } = name;
    public string[] Tags { get; } = tags ?? [];
    public Serilog.ILogger Logger { get; init; } = Serilog.Log.ForContext("HealthProbe", name).ForContext("Probe", name);
    public async Task<ProbeResult> CheckAsync(CancellationToken ct = default)
    {
        _tracker.Enter();
        try
        {
            await Task.Delay(_delay, ct);
            return new ProbeResult(ProbeStatus.Healthy);
        }
        finally
        {
            _tracker.Exit();
        }
    }
}
