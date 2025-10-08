using Kestrun.Scheduling;
using Kestrun.Scripting;
using Kestrun.Hosting;
using Serilog;
using Xunit;

namespace KestrunTests.Scheduling;

// Disable parallel execution for all scheduler tests to reduce timing contention on CI runners.
[CollectionDefinition("SchedulerTests", DisableParallelization = true)]
public sealed class SchedulerTestCollection : ICollectionFixture<SchedulerWarmupFixture> { }

/// <summary>
/// One-time warmup to JIT key paths and (optionally) initialize a PowerShell runspace before tests.
/// Keeps work minimal to avoid masking real issues while smoothing first-test latency variance.
/// </summary>
public sealed class SchedulerWarmupFixture : IDisposable
{
    public SchedulerWarmupFixture()
    {
        try
        {
            using var host = new KestrunHost("Tests", Log.Logger);
            using var pool = new KestrunRunspacePoolManager(host, 1, 1);
            var log = new LoggerConfiguration().MinimumLevel.Warning().CreateLogger();
            using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

            var ran = 0;
            svc.Schedule("__warm", TimeSpan.FromMinutes(10), async ct => { _ = Interlocked.Increment(ref ran); await Task.CompletedTask; }, runImmediately: true);
            var start = DateTime.UtcNow;
            while (ran == 0 && DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
            {
                Thread.Sleep(25);
            }
        }
        catch { }
    }

    public void Dispose() { }
}
