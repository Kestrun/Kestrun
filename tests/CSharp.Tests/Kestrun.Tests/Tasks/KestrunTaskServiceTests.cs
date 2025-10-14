using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.Scripting;
using Kestrun.Tasks;
using Serilog;
using Serilog.Core;
using Xunit;
using System.Threading.Tasks;

namespace KestrunTests.Tasks;

public class KestrunTaskServiceTests
{
    private static Logger CreateLogger() => new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

    [Fact]
    [Trait("Category", "Tasks")]
    public async Task Create_Start_Progress_Complete_Result_CSharp()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        using var pool = new KestrunRunspacePoolManager(host, 1, 1);
        var log = CreateLogger();
        var svc = new KestrunTaskService(pool, log);

        var code = """
for (int i=1;i<=5;i++)
{
    TaskProgress.StatusMessage=$"Step {i}/5";
    TaskProgress.PercentComplete = (i-1)*20;
    await Task.Delay(50);
}
TaskProgress.Complete("Completed");
return "done";
""";
        var lang = new LanguageOptions { Language = ScriptLanguage.CSharp, Code = code };

        var id = svc.Create(id: null, scriptCode: lang, autoStart: false, name: null, description: null);
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.True(svc.Start(id));

        // Wait until completed or 2s timeout
        var start = DateTime.UtcNow;
        while (svc.GetState(id) != TaskState.Completed && DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
        {
            await Task.Delay(20);
        }

        Assert.Equal(TaskState.Completed, svc.GetState(id));
        var result = svc.GetResult(id);
        Assert.Equal("done", result as string);

        var kr = svc.Get(id);
        Assert.NotNull(kr);
        Assert.Equal(100, kr!.Progress!.PercentComplete);
        Assert.Equal("Completed", kr.Progress.StatusMessage);

        Assert.True(svc.Remove(id));
    }

    [Fact]
    [Trait("Category", "Tasks")]
    public async Task Create_Start_Cancel_Stops_PowerShell()
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        using var pool = new KestrunRunspacePoolManager(host, 1, 1);
        var log = CreateLogger();
        var svc = new KestrunTaskService(pool, log);

        // Long-running PowerShell loop that will be interrupted by Cancel()
        var code = "$i=0; while($true){ Start-Sleep -Milliseconds 200; $i++ }";
        var lang = new LanguageOptions { Language = ScriptLanguage.PowerShell, Code = code };

        var id = svc.Create(id: null, scriptCode: lang, autoStart: false, name: null, description: null);
        Assert.True(svc.Start(id));

        // Wait for running
        var start = DateTime.UtcNow;
        while (svc.GetState(id) == TaskState.NotStarted && DateTime.UtcNow - start < TimeSpan.FromSeconds(1))
        {
            await Task.Delay(10);
        }

        _ = svc.Cancel(id);

        // Wait for stopped
        start = DateTime.UtcNow;
        while (svc.GetState(id) != TaskState.Stopped && DateTime.UtcNow - start < TimeSpan.FromSeconds(3))
        {
            await Task.Delay(20);
        }
        Assert.Equal(TaskState.Stopped, svc.GetState(id));
        var kr = svc.Get(id);
        Assert.NotNull(kr);
        Assert.Equal(100, kr!.Progress!.PercentComplete);
        Assert.Equal("Cancelled", kr.Progress.StatusMessage);
    }
}
