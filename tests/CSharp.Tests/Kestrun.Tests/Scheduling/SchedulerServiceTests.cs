using System.Collections;
using Kestrun.Scheduling;
using Kestrun.Scripting;
using Serilog;
using Serilog.Core;
using Xunit;
using System.Management.Automation;
using JobInfo = Kestrun.Scheduling.JobInfo;

namespace KestrunTests.Scheduling;

[Collection("SchedulerTests")]
public class SchedulerServiceTests
{
    private static Logger CreateLogger() => new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task Schedule_Interval_Runs_And_Updates_Timestamps()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        var ran = 0;
        svc.Schedule("tick", TimeSpan.FromMilliseconds(100), async ct =>
        {
            _ = Interlocked.Increment(ref ran);
            await Task.CompletedTask;
        }, runImmediately: false);
        // Previous fixed delay (800ms) was occasionally insufficient on slower / contended CI
        // (notably net8 arm64) leading to rare flakes where only 1 run occurred. Poll up to 3s
        // for at least 2 executions to make the test robust while still validating interval logic.
        var start = DateTime.UtcNow;
        while (ran < 2 && DateTime.UtcNow - start < TimeSpan.FromSeconds(3))
        {
            await Task.Delay(50);
        }

        var snap = svc.GetSnapshot();
        var job = Assert.Single(snap, j => j.Name == "tick");
        Assert.True(ran >= 2, $"Expected at least 2 runs, observed {ran}");
        _ = Assert.NotNull(job.LastRunAt);
        Assert.True(job.NextRunAt > job.LastRunAt, $"Expected NextRunAt ({job.NextRunAt:o}) to be after LastRunAt ({job.LastRunAt:o})");
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task Pause_And_Resume_Work()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        var ran = 0;
        var interval = TimeSpan.FromMilliseconds(100);
        svc.Schedule("p", interval, async ct => { _ = Interlocked.Increment(ref ran); await Task.CompletedTask; });
        // wait for at least one run
        var start = DateTime.UtcNow;
        while (ran == 0 && (DateTime.UtcNow - start) < TimeSpan.FromSeconds(2))
        {
            await Task.Delay(10);
        }
        Assert.True(ran > 0);

        // Pause and allow any in-flight execution to drain before taking baseline
        Assert.True(svc.Pause("p"));

        // Allow any in-flight execution already scheduled (pending Task.Delay) to complete.
        await Task.Delay(interval + TimeSpan.FromMilliseconds(50));

        // Stabilization phase: wait until the counter stops changing for at least one full interval
        // (up to a max timeout). This avoids flakiness from the scheduler potentially catching up
        // one additional anchored slot right after pause.
        var stabilizeStart = DateTime.UtcNow;
        var lastObserved = ran;
        var lastChangeAt = DateTime.UtcNow;
        var maxStabilize = TimeSpan.FromSeconds(4); // generous for slow CI
        while (DateTime.UtcNow - stabilizeStart < maxStabilize)
        {
            await Task.Delay(25);
            var current = ran;
            if (current != lastObserved)
            {
                lastObserved = current;
                lastChangeAt = DateTime.UtcNow;
            }
            else if (DateTime.UtcNow - lastChangeAt >= interval)
            {
                // no change for at least one interval => stabilized
                break;
            }
        }
        var pausedBaseline = lastObserved;

        // Resume and ensure at least one further increment occurs.
        Assert.True(svc.Resume("p"));
        var resumeStart = DateTime.UtcNow;
        var resumed = false;
        while (!resumed && DateTime.UtcNow - resumeStart < TimeSpan.FromSeconds(5))
        {
            if (ran > pausedBaseline) { resumed = true; break; }
            await Task.Delay(25);
        }
        Assert.True(resumed, "Job did not resume and increment after pause within timeout");
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task Cancel_Removes_And_Stops()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        var ran = 0;
        svc.Schedule("c", TimeSpan.FromMilliseconds(100), async ct => { _ = Interlocked.Increment(ref ran); await Task.CompletedTask; });
        // Wait for at least one run or timeout
        var start = DateTime.UtcNow;
        while (ran == 0 && DateTime.UtcNow - start < TimeSpan.FromSeconds(3))
        {
            await Task.Delay(25);
        }
        Assert.True(ran > 0, "Scheduled job 'c' never executed before cancel attempt");

        // Ensure job appears in snapshot (defensive; should always be true once schedule returns)
        var present = svc.GetSnapshot().Any(j => j.Name == "c");
        Assert.True(present, "Job 'c' missing from snapshot before cancel (unexpected)");

        var cancelled = false;
        for (var i = 0; i < 5 && !cancelled; i++)
        {
            cancelled = svc.Cancel("c");
            if (!cancelled)
            {
                await Task.Delay(50); // tiny backoff in pathological race (should be rare)
            }
        }
        Assert.True(cancelled, "Failed to cancel job 'c' after retries");
        await Task.Delay(250);
        var afterCancel = ran;
        await Task.Delay(250);
        Assert.Equal(afterCancel, ran);
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public void GetReport_And_Hashtable_Shape()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        svc.Schedule("r1", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask);
        svc.Schedule("r2", "*/5 * * * * *", async ct => await Task.CompletedTask);

        var rpt = svc.GetReport(TimeZoneInfo.Utc);
        Assert.True(rpt.GeneratedAt <= DateTimeOffset.UtcNow);
        Assert.Contains(rpt.Jobs, j => j.Name == "r1");
        Assert.Contains(rpt.Jobs, j => j.Name == "r2");

        var ht = svc.GetReportHashtable(TimeZoneInfo.Utc);
        Assert.True(ht.ContainsKey("GeneratedAt"));
        Assert.True(ht.ContainsKey("Jobs"));
        var jobsObj = ht["Jobs"];
        Assert.NotNull(jobsObj);
        var jobs = Assert.IsType<Hashtable[]>(jobsObj);
        Assert.Contains(jobs, o => o["Name"]?.ToString() == "r1");
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task Schedule_PowerShell_Code_And_File()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        svc.Schedule("ps-inline", TimeSpan.FromMilliseconds(100), "$null | Out-Null", ScriptLanguage.PowerShell);

        var tmp = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmp, "$x=1; $x | Out-Null");
            svc.Schedule("ps-file", TimeSpan.FromMilliseconds(100), new FileInfo(tmp), ScriptLanguage.PowerShell);
            var start = DateTime.UtcNow;
            var seenInline = false; var seenFile = false;
            // runspace cold start on CI (especially net8) can exceed 250ms; allow up to 5s
            while (!(seenInline && seenFile) && DateTime.UtcNow - start < TimeSpan.FromSeconds(5))
            {
                var snap = svc.GetSnapshot();
                if (!seenInline)
                {
                    seenInline = snap.Any(j => j.Name == "ps-inline");
                }

                if (!seenFile)
                {
                    seenFile = snap.Any(j => j.Name == "ps-file");
                }

                if (!(seenInline && seenFile))
                {
                    await Task.Delay(50);
                }
            }
            Assert.True(seenInline, "ps-inline job not visible in snapshot within timeout");
            Assert.True(seenFile, "ps-file job not visible in snapshot within timeout");
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task Schedule_RunImmediately_CSharp_Interval_Fires()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        var ran = 0;
        svc.Schedule("immediate-int", TimeSpan.FromMinutes(5), async ct => { _ = Interlocked.Increment(ref ran); await Task.CompletedTask; }, runImmediately: true);

        var start = DateTime.UtcNow;
        while (ran == 0 && DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
        {
            await Task.Delay(25);
        }
        Assert.True(ran > 0);
        var job = Assert.Single(svc.GetSnapshot(), j => j.Name == "immediate-int");
        _ = Assert.NotNull(job.LastRunAt);
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task Schedule_Cron_CSharp_RunImmediately()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        var ran = 0;
        svc.Schedule("cron-cs", "* * * * * *", async ct => { _ = Interlocked.Increment(ref ran); await Task.CompletedTask; }, runImmediately: true);

        var start = DateTime.UtcNow;
        while (ran == 0 && DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
        {
            await Task.Delay(25);
        }
        Assert.True(ran > 0);
        var job = Assert.Single(svc.GetSnapshot(), j => j.Name == "cron-cs");
        _ = Assert.NotNull(job.LastRunAt);
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task Schedule_Cron_PowerShell_ScriptBlock()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        // Script block as PowerShell job
        var ranFlag = new ManualResetEventSlim();
        var script = ScriptBlock.Create("$global:__psCronRuns = ($global:__psCronRuns + 1); if(-not $global:__psCronRuns){$global:__psCronRuns=1}");
        svc.Schedule("ps-cron", "* * * * * *", script, runImmediately: true);

        // Poll snapshot for LastRunAt (runImmediately path)
        var start = DateTime.UtcNow;
        // Allow a little more time on slower CI / Linux environments; was 2s and proved flaky on .NET 9
        while (DateTime.UtcNow - start < TimeSpan.FromSeconds(3))
        {
            var snap = svc.GetSnapshot();
            if (snap.Any(j => j.Name == "ps-cron" && j.LastRunAt != null))
            {
                ranFlag.Set();
                break;
            }
            await Task.Delay(50);
        }
        Assert.True(ranFlag.IsSet, "PowerShell cron job did not run immediately");
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task Schedule_Cron_PowerShell_Code_String()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        svc.Schedule("ps-code-cron", "* * * * * *", "$null | Out-Null", ScriptLanguage.PowerShell, runImmediately: true);

        var start = DateTime.UtcNow;
        var seen = false;
        while (!seen && DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
        {
            seen = svc.GetSnapshot().Any(j => j.Name == "ps-code-cron" && j.LastRunAt != null);
            if (!seen)
            {
                await Task.Delay(50);
            }
        }
        Assert.True(seen);
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task Schedule_File_Cron_PowerShell()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        var tmp = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmp, "$x=42 # cron file");
            svc.Schedule("ps-file-cron", "* * * * * *", new FileInfo(tmp), ScriptLanguage.PowerShell, runImmediately: true);

            var start = DateTime.UtcNow;
            var seen = false;
            while (!seen && DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
            {
                seen = svc.GetSnapshot().Any(j => j.Name == "ps-file-cron" && j.LastRunAt != null);
                if (!seen)
                {
                    await Task.Delay(50);
                }
            }
            Assert.True(seen);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task ScheduleAsync_File_Interval_And_Cron()
    {
        // Allow two concurrent runspaces to avoid contention causing the second immediate
        // job to queue behind the first on slow CI (net8 arm64), which occasionally exceeded
        // the previous 10s deadline.
        using var pool = new KestrunRunspacePoolManager(1, 2);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        // Warm-up: a tiny immediate PowerShell interval job to initialize the runspace before timing.
        svc.Schedule("warm", TimeSpan.FromMinutes(5), "$null | Out-Null", ScriptLanguage.PowerShell, runImmediately: true);
        var warmStart = DateTime.UtcNow;
        var warmed = false;
        while (!warmed && DateTime.UtcNow - warmStart < TimeSpan.FromSeconds(5))
        {
            warmed = svc.GetSnapshot().Any(j => j.Name == "warm" && j.LastRunAt != null);
            if (!warmed) { await Task.Delay(50); }
        }

        var tmp1 = Path.GetTempFileName();
        var tmp2 = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmp1, "$null | Out-Null # interval async");
            await File.WriteAllTextAsync(tmp2, "$null | Out-Null # cron async");
            await svc.ScheduleAsync("ps-file-int-async", TimeSpan.FromMinutes(10), new FileInfo(tmp1), ScriptLanguage.PowerShell, runImmediately: true);
            await svc.ScheduleAsync("ps-file-cron-async", "* * * * * *", new FileInfo(tmp2), ScriptLanguage.PowerShell, runImmediately: true);

            var start = DateTime.UtcNow;
            var seen1 = false; var seen2 = false;
            // Extended to 20s total (rare path: slow single-core / throttled runners + initial module load)
            while (!(seen1 && seen2) && DateTime.UtcNow - start < TimeSpan.FromSeconds(20))
            {
                var snap = svc.GetSnapshot();
                if (!seen1)
                {
                    seen1 = snap.Any(j => j.Name == "ps-file-int-async" && j.LastRunAt != null);
                }
                if (!seen2)
                {
                    seen2 = snap.Any(j => j.Name == "ps-file-cron-async" && j.LastRunAt != null);
                }
                if (!(seen1 && seen2))
                {
                    await Task.Delay(100); // slightly larger interval reduces snapshot churn
                }
            }
            Assert.True(seen1, "Interval file job did not execute its immediate run within 20s");
            Assert.True(seen2, "Cron file job did not execute its immediate run within 20s");
        }
        finally
        {
            try { File.Delete(tmp1); } catch { }
            try { File.Delete(tmp2); } catch { }
        }
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public void CancelAll_Removes_All_Tasks()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        svc.Schedule("a1", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask);
        svc.Schedule("a2", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask);
        Assert.Equal(2, svc.GetSnapshot().Count);
        svc.CancelAll();
        Assert.Empty(svc.GetSnapshot());
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public void Duplicate_Name_Throws()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        svc.Schedule("dup", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask);
        var ex = Assert.Throws<InvalidOperationException>(() => svc.Schedule("dup", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask));
        Assert.NotNull(ex);
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public void Invalid_Name_Operations_Throw()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        _ = Assert.Throws<ArgumentException>(() => svc.Cancel(" "));
        _ = Assert.Throws<ArgumentException>(() => svc.Pause(""));
        _ = Assert.Throws<ArgumentException>(() => svc.Resume(null!));
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public void Snapshot_Filtering_And_Hashtable_Output()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        svc.Schedule("job-alpha", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask);
        svc.Schedule("job-beta", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask);
        svc.Schedule("other", TimeSpan.FromMinutes(1), async ct => await Task.CompletedTask);

        var filtered = svc.GetSnapshot(TimeZoneInfo.Utc, asHashtable: false, "job-*");
        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, o => Assert.StartsWith("job-", ((JobInfo)o).Name));

        var ht = svc.GetSnapshot(TimeZoneInfo.Utc, asHashtable: true, "other");
        var entry = Assert.Single(ht);
        var h = Assert.IsType<Hashtable>(entry);
        Assert.Equal("other", h["Name"]);
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task Timestamp_Invariant_NextRunAt_After_LastRunAt()
    {
        using var pool = new KestrunRunspacePoolManager(1, 1);
        var log = CreateLogger();
        using var svc = new SchedulerService(pool, log, TimeZoneInfo.Utc);

        // Interval job (immediate) + cron job (immediate) to exercise both paths.
        var intervalRuns = 0;
        svc.Schedule("int", TimeSpan.FromMilliseconds(120), async ct => { _ = Interlocked.Increment(ref intervalRuns); await Task.CompletedTask; }, runImmediately: true);
        svc.Schedule("cron", "* * * * * *", async ct => await Task.CompletedTask, runImmediately: true);

        var start = DateTime.UtcNow;
        // Wait until at least 2 interval executions (immediate + one scheduled) or timeout.
        while (intervalRuns < 2 && DateTime.UtcNow - start < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(50);
        }

        // Take several snapshots over a short window to look for transient ordering issues.
        for (var i = 0; i < 5; i++)
        {
            var snap = svc.GetSnapshot();
            foreach (var job in snap)
            {
                if (job.LastRunAt is not null)
                {
                    Assert.True(job.NextRunAt >= job.LastRunAt, $"Invariant violated for {job.Name}: NextRunAt {job.NextRunAt:o} < LastRunAt {job.LastRunAt:o}");
                }
            }
            await Task.Delay(50);
        }
    }
}
