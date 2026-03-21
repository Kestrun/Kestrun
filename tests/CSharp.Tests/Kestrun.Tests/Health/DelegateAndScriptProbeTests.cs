using System.Collections;
using System.Management.Automation;
using System.Reflection;
using Kestrun.Health;
using Kestrun.Hosting;
using Kestrun.Languages;
using Kestrun.Scripting;
using Xunit;

namespace Kestrun.Tests.Health;

public class DelegateAndScriptProbeTests
{
    private static readonly Type PowerShellScriptProbeType = typeof(ProbeResult).Assembly.GetType("Kestrun.Health.PowerShellScriptProbe", throwOnError: true)!;
    private static readonly Type ScriptProbeFactoryType = typeof(ProbeResult).Assembly.GetType("Kestrun.Health.ScriptProbeFactory", throwOnError: true)!;

    [Fact]
    public async Task DelegateProbe_CheckAsync_ReturnsCallbackResult()
    {
        var probe = new DelegateProbe(
            "demo",
            [" core ", "CORE", "network"],
            _ => Task.FromResult(new ProbeResult(ProbeStatus.Healthy, "ok")));

        var result = await probe.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProbeStatus.Healthy, result.Status);
        Assert.Equal("ok", result.Description);
        Assert.Equal(["core", "network"], probe.Tags);
    }

    [Fact]
    public async Task DelegateProbe_CheckAsync_WhenCancelled_ReturnsDegradedResult()
    {
        var probe = new DelegateProbe(
            "demo",
            null,
            static ct => Task.FromCanceled<ProbeResult>(ct));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await probe.CheckAsync(cts.Token);

        Assert.Equal(ProbeStatus.Degraded, result.Status);
        Assert.Equal("Canceled", result.Description);
    }

    [Fact]
    public async Task DelegateProbe_CheckAsync_WhenCallbackThrows_ReturnsUnhealthyResult()
    {
        var probe = new DelegateProbe(
            "demo",
            null,
            static _ => throw new InvalidOperationException("boom"));

        var result = await probe.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProbeStatus.Unhealthy, result.Status);
        Assert.Contains("boom", result.Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ok", ProbeStatus.Healthy, true)]
    [InlineData("warning", ProbeStatus.Degraded, true)]
    [InlineData("failed", ProbeStatus.Unhealthy, true)]
    [InlineData("unknown", ProbeStatus.Unhealthy, false)]
    public void PowerShellScriptProbe_TryParseStatus_MapsValues(string value, ProbeStatus expectedStatus, bool expectedSuccess)
    {
        var (success, parsedStatus) = InvokeTryParseStatus(value);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedStatus, parsedStatus);
    }

    [Fact]
    public void PowerShellScriptProbe_TryConvert_UnwrapsProbeResult()
    {
        var original = new ProbeResult(ProbeStatus.Healthy, "direct");
        var (success, result) = InvokeTryConvert(new PSObject(original));

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(original, result);
    }

    [Fact]
    public void PowerShellScriptProbe_TryConvert_ConvertsRawStatusString()
    {
        var (success, result) = InvokeTryConvert(new PSObject("warn"));

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(ProbeStatus.Degraded, result!.Status);
        Assert.Equal("warn", result.Description);
    }

    [Fact]
    public void PowerShellScriptProbe_TryConvert_ConvertsPropertyBasedPayloadAndNormalizesData()
    {
        var payload = new PSObject();
        payload.Properties.Add(new PSNoteProperty("status", "healthy"));
        payload.Properties.Add(new PSNoteProperty("description", "all good"));
        payload.Properties.Add(new PSNoteProperty("data", new Hashtable
        {
            ["count"] = 3,
            [" "] = 7,
            ["nested"] = new ArrayList { 1, new PSObject("two") },
        }));

        var (success, result) = InvokeTryConvert(payload);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(ProbeStatus.Healthy, result!.Status);
        Assert.Equal("all good", result.Description);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data!["count"]);
        Assert.False(result.Data.ContainsKey(" "));
        var nested = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.Data["nested"]);
        Assert.Equal(2, nested.Count);
    }

    [Fact]
    public void PowerShellScriptProbe_Constructor_RejectsWhitespaceScript()
    {
        using var host = new KestrunHost("Tests", Serilog.Log.Logger);

        var exception = Assert.Throws<TargetInvocationException>(() => CreatePowerShellScriptProbe(host, "   ", () => null!));
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void ScriptProbeFactory_Create_RejectsUnsupportedLanguages()
    {
        using var host = new KestrunHost("Tests", Serilog.Log.Logger);

        var native = Assert.Throws<TargetInvocationException>(() => InvokeScriptProbeFactoryCreate(host, ScriptLanguage.Native, "return 1"));
        Assert.IsType<NotSupportedException>(native.InnerException);

        var python = Assert.Throws<TargetInvocationException>(() => InvokeScriptProbeFactoryCreate(host, ScriptLanguage.Python, "return 1"));
        Assert.IsType<NotImplementedException>(python.InnerException);
    }

    [Fact]
    public void ScriptProbeFactory_Create_ValidatesCodeAndRunspaceAccessor()
    {
        using var host = new KestrunHost("Tests", Serilog.Log.Logger);

        var badCode = Assert.Throws<TargetInvocationException>(() => InvokeScriptProbeFactoryCreate(host, ScriptLanguage.CSharp, "   "));
        Assert.IsType<ArgumentException>(badCode.InnerException);

        var badAccessor = Assert.Throws<TargetInvocationException>(() => InvokeScriptProbeFactoryCreate(host, ScriptLanguage.PowerShell, "Get-Date", runspaceAccessor: null));
        Assert.IsType<ArgumentNullException>(badAccessor.InnerException);
    }

    private static object CreatePowerShellScriptProbe(KestrunHost host, string script, Func<KestrunRunspacePoolManager> poolAccessor)
    {
        var ctor = PowerShellScriptProbeType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Single();

        var instance = ctor.Invoke([
            host,
            "probe",
            null,
            script,
            poolAccessor,
            null,
        ]);

        Assert.NotNull(instance);
        return instance;
    }

    private static object InvokeScriptProbeFactoryCreate(
        KestrunHost host,
        ScriptLanguage language,
        string code,
        Func<KestrunRunspacePoolManager>? runspaceAccessor = null)
    {
        var createMethod = ScriptProbeFactoryType.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(createMethod);

        var result = createMethod.Invoke(null, [
            host,
            "probe",
            null,
            language,
            code,
            runspaceAccessor,
            null,
            null,
            null,
        ]);

        Assert.NotNull(result);
        return result;
    }

    private static (bool Success, ProbeStatus Status) InvokeTryParseStatus(string value)
    {
        var method = PowerShellScriptProbeType.GetMethod("TryParseStatus", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var values = new object?[] { value, null };
        var success = method.Invoke(null, values);

        Assert.NotNull(success);
        _ = Assert.IsType<bool>(success);
        var status = Assert.IsType<ProbeStatus>(values[1]);

        return ((bool)success, status);
    }

    private static (bool Success, ProbeResult? Result) InvokeTryConvert(PSObject input)
    {
        var method = PowerShellScriptProbeType.GetMethod("TryConvert", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var values = new object?[] { input, null };
        var success = method.Invoke(null, values);

        Assert.NotNull(success);
        _ = Assert.IsType<bool>(success);

        return ((bool)success, values[1] as ProbeResult);
    }
}
