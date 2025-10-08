using Kestrun.Scheduling;
using Kestrun.Scripting;
using Kestrun.Hosting;
using Serilog;
using Serilog.Core;
using Xunit;

namespace KestrunTests.Scheduling;

public class JobFactoryTests
{
    private static Logger CreateLogger() => new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

    [Fact]
    [Trait("Category", "Scheduling")]
    public void PowerShell_Create_Throws_WhenPoolMissing()
    {
        var cfg = new JobFactory.JobConfig(
            ScriptLanguage.PowerShell,
            Code: "Write-Output 'hi'",
            Log: CreateLogger(),
            Pool: null
        );

        var ex = Assert.Throws<InvalidOperationException>(() => JobFactory.Create(cfg));
        Assert.Contains("runspace pool", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task PowerShell_Job_Executes_Successfully()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        using var pool = new KestrunRunspacePoolManager(host, 1, 1);
        var cfg = new JobFactory.JobConfig(
            ScriptLanguage.PowerShell,
            Code: "$x = 1; $x",
            Log: CreateLogger(),
            Pool: pool
        );

        var job = JobFactory.Create(cfg);
        await job(CancellationToken.None);
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task PowerShell_Job_Honors_Cancellation()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        using var pool = new KestrunRunspacePoolManager(host, 1, 1);
        var cfg = new JobFactory.JobConfig(
            ScriptLanguage.PowerShell,
            Code: "Start-Sleep -Seconds 5",
            Log: CreateLogger(),
            Pool: pool
        );

        var job = JobFactory.Create(cfg);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job(cts.Token));
    }

    [Fact]
    [Trait("Category", "Scheduling")]
    public async Task CreateAsync_Reads_Code_From_File()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        using var pool = new KestrunRunspacePoolManager(host, 1, 1);
        var cfg = new JobFactory.JobConfig(
            ScriptLanguage.PowerShell,
            Code: string.Empty,
            Log: CreateLogger(),
            Pool: pool
        );

        var tmp = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmp, "Write-Output 'abc'");
            var job = await JobFactory.CreateAsync(cfg, new FileInfo(tmp));
            await job(CancellationToken.None);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }
}
