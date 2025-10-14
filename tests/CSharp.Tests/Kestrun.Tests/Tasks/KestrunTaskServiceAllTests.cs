using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.Scripting;
using Kestrun.Tasks;
using Serilog;
using Serilog.Core;
using Xunit;

namespace KestrunTests.Tasks;

public class KestrunTaskServiceAllTests
{
    private static Logger CreateLogger() => new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

    private static (KestrunTaskService Svc, KestrunHost Host, KestrunRunspacePoolManager Pool) CreateService()
    {
        var host = new KestrunHost("Tests", Log.Logger);
        var pool = new KestrunRunspacePoolManager(host, 1, 1);
        var svc = new KestrunTaskService(pool, CreateLogger());
        return (svc, host, pool);
    }

    [Fact]
    [Trait("Category", "Tasks")]
    public async Task Create_WithAutoStart_Completes_And_ReturnsResult_CSharp()
    {
        var (svc, host, pool) = CreateService();
        using (host)
        using (pool)
        {

            var code = """
        await Task.Delay(25);
        return 123;
        """;
            var lang = new LanguageOptions { Language = ScriptLanguage.CSharp, Code = code };

            var id = svc.Create(id: null, scriptCode: lang, autoStart: true, name: "Auto", description: "desc");
            Assert.False(string.IsNullOrWhiteSpace(id));

            var start = DateTime.UtcNow;
            while (svc.GetState(id) != TaskState.Completed && DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
            {
                await Task.Delay(10);
            }
            Assert.Equal(TaskState.Completed, svc.GetState(id));

            Assert.Equal(123, (int)(svc.GetResult(id) ?? -1));
            var kr = svc.Get(id);
            Assert.NotNull(kr);
            Assert.Equal("Auto", kr!.Name);
            Assert.Equal("desc", kr.Description);
            Assert.Equal(100, kr.Progress!.PercentComplete);
            Assert.Equal("Completed", kr.Progress.StatusMessage);
        }
    }

    [Fact]
    [Trait("Category", "Tasks")]
    public void Create_WithNullScript_Throws_ArgumentNull()
    {
        var (svc, host, pool) = CreateService();
        using (host) using (pool)
        {
            _ = Assert.Throws<ArgumentNullException>(() => svc.Create(null, null!, false, null, null));
        }
    }

    [Fact]
    [Trait("Category", "Tasks")]
    public void Create_DuplicateId_Throws_InvalidOperation()
    {
        var (svc, host, pool) = CreateService();
        using (host) using (pool)
        {
            var lang = new LanguageOptions { Language = ScriptLanguage.CSharp, Code = "return 1;" };
            var id = svc.Create("abc", lang, false, null, null);
            Assert.Equal("abc", id);
            _ = Assert.Throws<InvalidOperationException>(() => svc.Create("abc", lang, false, null, null));
        }
    }

    [Fact]
    [Trait("Category", "Tasks")]
    public void Start_ReturnsFalse_ForMissing_And_SecondStart()
    {
        var (svc, host, pool) = CreateService();
        using (host) using (pool)
        {
            Assert.False(svc.Start("does-not-exist"));
            var lang = new LanguageOptions { Language = ScriptLanguage.CSharp, Code = "return 1;" };
            var id = svc.Create(null, lang, false, null, null);
            Assert.True(svc.Start(id));
            Assert.False(svc.Start(id));
        }
    }

    [Fact]
    [Trait("Category", "Tasks")]
    public async Task StartAsync_WaitsUntil_Completed_PowerShell()
    {
        var (svc, host, pool) = CreateService();
        using (host)
        using (pool)
        {

            var code = "Start-Sleep -Milliseconds 50; 'ok'";
            var lang = new LanguageOptions { Language = ScriptLanguage.PowerShell, Code = code };
            var id = svc.Create(null, lang, false, null, null);

            var started = await svc.StartAsync(id);
            Assert.True(started);

            Assert.Equal(TaskState.Completed, svc.GetState(id));
            var result = svc.GetResult(id) as object[];
            Assert.NotNull(result);
            _ = Assert.Single(result!);
            Assert.Equal("ok", result![0] as string);
        }
    }

    [Fact]
    [Trait("Category", "Tasks")]
    public void Get_GetState_GetResult_ReturnNull_WhenMissing()
    {
        var (svc, host, pool) = CreateService();
        using (host) using (pool)
        {
            Assert.Null(svc.Get("missing"));
            Assert.Null(svc.GetState("missing"));
            Assert.Null(svc.GetResult("missing"));
        }
    }

    [Fact]
    [Trait("Category", "Tasks")]
    public async Task Cancel_Stops_RunningTask_And_ReturnsFalse_AfterTerminal()
    {
        var (svc, host, pool) = CreateService();
        using (host)
        using (pool)
        {

            Assert.False(svc.Cancel("missing"));

            var code = "$i=0; while($true){ Start-Sleep -Milliseconds 200; $i++ }";
            var lang = new LanguageOptions { Language = ScriptLanguage.PowerShell, Code = code };
            var id = svc.Create(null, lang, false, null, null);
            Assert.True(svc.Start(id));

            // Wait until Running
            var start = DateTime.UtcNow;
            while (svc.GetState(id) == TaskState.NotStarted && DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
            {
                await Task.Delay(20);
            }

            Assert.True(svc.Cancel(id));

            // Wait until Stopped
            start = DateTime.UtcNow;
            while (svc.GetState(id) != TaskState.Stopped && DateTime.UtcNow - start < TimeSpan.FromSeconds(3))
            {
                await Task.Delay(20);
            }
            Assert.Equal(TaskState.Stopped, svc.GetState(id));
            var kr = svc.Get(id)!;
            Assert.Equal(100, kr.Progress!.PercentComplete);
            Assert.Equal("Cancelled", kr.Progress.StatusMessage);
            Assert.False(svc.Cancel(id));
        }
    }

    [Fact]
    [Trait("Category", "Tasks")]
    public async Task Remove_Behavior_For_NotFound_NotTerminal_And_Terminal()
    {
        var (svc, host, pool) = CreateService();
        using (host)
        using (pool)
        {

            Assert.False(svc.Remove("missing"));

            var code = "$i=0; while($true){ Start-Sleep -Milliseconds 200; $i++ }";
            var lang = new LanguageOptions { Language = ScriptLanguage.PowerShell, Code = code };
            var id = svc.Create(null, lang, false, null, null);
            Assert.True(svc.Start(id));

            // CI (Linux) sometimes cancels before the worker has flipped state to Running.
            // Wait briefly for transition out of NotStarted to avoid a race where cancellation
            // happens pre-execution and state never updates to Stopped within the timeout.
            var spin = DateTime.UtcNow;
            while (svc.GetState(id) == TaskState.NotStarted && DateTime.UtcNow - spin < TimeSpan.FromSeconds(2))
            {
                await Task.Delay(25);
            }

            // Not terminal yet → cannot remove
            Assert.False(svc.Remove(id));

            // Cancel to reach terminal
            _ = svc.Cancel(id);
            var start = DateTime.UtcNow;
            while (svc.GetState(id) != TaskState.Stopped && DateTime.UtcNow - start < TimeSpan.FromSeconds(6))
            {
                await Task.Delay(20);
            }
            // If still not stopped, surface state for debugging
            var finalState = svc.GetState(id);
            Assert.True(finalState == TaskState.Stopped, $"Expected Stopped, got {finalState}");
            Assert.True(svc.Remove(id));
            Assert.Null(svc.Get(id));
        }
    }

    [Fact]
    [Trait("Category", "Tasks")]
    public async Task GetResult_IsNull_BeforeCompletion_Then_NonNull()
    {
        var (svc, host, pool) = CreateService();
        using (host)
        using (pool)
        {
            var code = "await Task.Delay(100); return 42;";
            var lang = new LanguageOptions { Language = ScriptLanguage.CSharp, Code = code };
            var id = svc.Create(null, lang, false, null, null);
            Assert.True(svc.Start(id));
            // Immediately after start, result should be null
            Assert.Null(svc.GetResult(id));
            var start = DateTime.UtcNow;
            while (svc.GetState(id) != TaskState.Completed && DateTime.UtcNow - start < TimeSpan.FromSeconds(3))
            {
                await Task.Delay(25);
            }
            Assert.Equal(TaskState.Completed, svc.GetState(id));
            Assert.Equal(42, svc.GetResult(id));
        }
    }

    [Fact]
    [Trait("Category", "Tasks")]
    public async Task StartAsync_ReturnsFalse_On_AlreadyStarted_Task()
    {
        var (svc, host, pool) = CreateService();
        using (host)
        using (pool)
        {
            var code = "await Task.Delay(50); return 'x';";
            var lang = new LanguageOptions { Language = ScriptLanguage.CSharp, Code = code };
            var id = svc.Create(null, lang, false, null, null);
            Assert.True(svc.Start(id));
            // Second async start attempt should return false
            var second = await svc.StartAsync(id);
            Assert.False(second);
        }
    }

    [Fact]
    [Trait("Category", "Tasks")]
    public async Task Remove_Fails_With_Running_Child_Then_Succeeds_When_Child_Terminal()
    {
        var (svc, host, pool) = CreateService();
        using (host)
        using (pool)
        {
            // Create parent (quick completes) and child (long running)
            var parentLang = new LanguageOptions { Language = ScriptLanguage.CSharp, Code = "await Task.Delay(20); return 1;" };
            var childLang = new LanguageOptions { Language = ScriptLanguage.CSharp, Code = "await Task.Delay(300); return 2;" };
            var parentId = svc.Create(null, parentLang, true, "parent", null);
            var childId = svc.Create(null, childLang, false, "child", null);

            // Link child manually to parent; service doesn't expose a parent-child API yet
            var parentTaskField = typeof(KestrunTaskService).GetField("_tasks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(parentTaskField);
            var dict = parentTaskField!.GetValue(svc) as System.Collections.IDictionary;
            Assert.NotNull(dict);
            var parentTask = (KestrunTask?)dict![parentId];
            var childTask = (KestrunTask?)dict![childId];
            Assert.NotNull(parentTask);
            Assert.NotNull(childTask);
            childTask!.Parent = parentTask!;
            parentTask!.Children.Add(childTask);

            Assert.True(svc.Start(childId));

            // Wait for child to actually transition to Running (CI can be slower scheduling the work)
            var childSpin = DateTime.UtcNow;
            while (svc.GetState(childId) == TaskState.NotStarted && DateTime.UtcNow - childSpin < TimeSpan.FromSeconds(3))
            {
                await Task.Delay(20);
            }

            // Wait parent to complete
            var start = DateTime.UtcNow;
            while (svc.GetState(parentId) != TaskState.Completed && DateTime.UtcNow - start < TimeSpan.FromSeconds(3))
            {
                await Task.Delay(10);
            }
            Assert.Equal(TaskState.Completed, svc.GetState(parentId));

            // Parent removal should fail because child still running
            Assert.False(svc.Remove(parentId));

            // Cancel child to reach terminal (ensure we saw Running first where possible)
            Assert.True(svc.Cancel(childId));
            start = DateTime.UtcNow;
            while (svc.GetState(childId) != TaskState.Stopped && DateTime.UtcNow - start < TimeSpan.FromSeconds(8))
            {
                await Task.Delay(25);
            }
            var childFinal = svc.GetState(childId);
            Assert.True(childFinal == TaskState.Stopped, $"Expected Stopped after cancellation, got {childFinal}");

            // Now removal should succeed and cascade
            Assert.True(svc.Remove(parentId));
            Assert.Null(svc.Get(parentId));
            Assert.Null(svc.Get(childId));
        }
    }

    [Fact]
    [Trait("Category", "Tasks")]
    public async Task VBNet_Task_Completes_And_Returns_Result()
    {
        var (svc, host, pool) = CreateService();
        using (host)
        using (pool)
        {
            // NOTE: Do not include "Imports" statements inside the snippet. The VB wrapper already
            // adds Imports at the file level; putting them inside the generated function causes
            // compilation errors. Provide only executable code lines here.
            var vbCode = @"Await Task.Delay(50)
Return 777";
            var lang = new LanguageOptions { Language = ScriptLanguage.VBNet, Code = vbCode };
            var id = svc.Create(null, lang, false, null, null);
            Assert.True(svc.Start(id));
            var start = DateTime.UtcNow;
            while (svc.GetState(id) != TaskState.Completed && DateTime.UtcNow - start < TimeSpan.FromSeconds(3))
            {
                await Task.Delay(25);
            }
            Assert.Equal(TaskState.Completed, svc.GetState(id));
            Assert.Equal(777, svc.GetResult(id));
        }
    }

    [Fact]
    [Trait("Category", "Tasks")]
    public void List_Reflects_Created_And_Removed_Tasks()
    {
        var (svc, host, pool) = CreateService();
        using (host) using (pool)
        {
            var lang = new LanguageOptions { Language = ScriptLanguage.CSharp, Code = "return 1;" };
            var id1 = svc.Create(null, lang, false, null, null);
            var id2 = svc.Create(null, lang, false, null, null);

            var list = svc.List();
            Assert.Contains(list, t => t.Id == id1);
            Assert.Contains(list, t => t.Id == id2);

            // Remove one (mark completed via StartAsync quick code)
            // Since our quick code completes instantly, start then remove
            Assert.True(svc.Start(id1));
            // Busy-wait briefly for completion
            var start = DateTime.UtcNow;
            while (svc.GetState(id1) != TaskState.Completed && DateTime.UtcNow - start < TimeSpan.FromSeconds(1))
            {
                Thread.Sleep(10);
            }
            Assert.True(svc.Remove(id1));
            list = svc.List();
            Assert.DoesNotContain(list, t => t.Id == id1);
            Assert.Contains(list, t => t.Id == id2);
        }
    }

    [Fact]
    [Trait("Category", "Tasks")]
    public void SetTaskName_And_Description_Work_And_Validate()
    {
        var (svc, host, pool) = CreateService();
        using (host) using (pool)
        {
            var lang = new LanguageOptions { Language = ScriptLanguage.CSharp, Code = "return 1;" };
            var id = svc.Create(null, lang, false, null, null);

            Assert.True(svc.SetTaskName(id, "NewName"));
            Assert.True(svc.SetTaskDescription(id, "NewDesc"));
            var kr = svc.Get(id)!;
            Assert.Equal("NewName", kr.Name);
            Assert.Equal("NewDesc", kr.Description);

            // Validation
            _ = Assert.Throws<ArgumentNullException>(() => svc.SetTaskName(id, " "));
            _ = Assert.Throws<ArgumentNullException>(() => svc.SetTaskDescription(id, "\t"));

            // Missing id returns false
            Assert.False(svc.SetTaskName("missing", "x"));
            Assert.False(svc.SetTaskDescription("missing", "y"));
        }
    }
}
