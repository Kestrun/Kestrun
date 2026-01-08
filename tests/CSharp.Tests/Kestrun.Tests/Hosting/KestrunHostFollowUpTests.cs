using Kestrun;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;
using Xunit;
using System.Reflection;

namespace KestrunTests.Hosting;

[Collection("SharedStateSerial")]
public class KestrunHostFollowUpTests
{
    private const string EntryScriptPath = "tests.ps1";

    private sealed class SwitchReset(string name, bool prev) : IDisposable
    {
        private readonly string _name = name;
        private readonly bool _prev = prev;
        public void Dispose() => AppContext.SetSwitch(_name, _prev);
    }

    private static string LocateDevModule()
    {
        var current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            var candidate = Path.Combine(current, "src", "PowerShell", "Kestrun", "Kestrun.psm1");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            current = Path.GetDirectoryName(current);
        }
        throw new FileNotFoundException("Unable to locate dev Kestrun.psm1 in repo");
    }

    private static IDisposable SetPreviewSwitch(bool value)
    {
        _ = AppContext.TryGetSwitch("System.Runtime.EnablePreviewFeatures", out var prev);
        AppContext.SetSwitch("System.Runtime.EnablePreviewFeatures", value);
        return new SwitchReset("System.Runtime.EnablePreviewFeatures", prev);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task StopAllAsync_With_Canceled_Token_Completes()
    {
        Reset();
        _ = KestrunHostManager.Create("s1", EntryScriptPath, () => NewHost("s1"));
        _ = KestrunHostManager.Create("s2", EntryScriptPath, () => NewHost("s2"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await KestrunHostManager.StopAllAsync(cts.Token);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Destroy_Default_Reassigns_To_Existing_Instance()
    {
        Reset();
        _ = KestrunHostManager.Create("aa", EntryScriptPath, () => NewHost("aa"), setAsDefault: true);
        _ = KestrunHostManager.Create("bb", EntryScriptPath, () => NewHost("bb"));
        _ = KestrunHostManager.Create("cc", EntryScriptPath, () => NewHost("cc"));

        KestrunHostManager.Destroy("aa");

        var names = KestrunHostManager.InstanceNames;
        Assert.Contains("bb", names);
        Assert.Contains("cc", names);
        var def = KestrunHostManager.Default;
        // Default should be reassigned to one of the remaining instances
        Assert.NotNull(def);
        Assert.Contains(def.Options.ApplicationName, names);
        Assert.NotEqual("aa", def.Options.ApplicationName);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Create_WithLogger_Throws_When_KestrunRoot_Unset()
    {
        // Save current private _kestrunRoot and then set it to null via reflection
        var type = typeof(KestrunHostManager);
        var field = type.GetField("_kestrunRoot", BindingFlags.NonPublic | BindingFlags.Static)!;
        var original = (string?)field.GetValue(null);

        try
        {
            KestrunHostManager.DestroyAll();
            field.SetValue(null, null);

            var module = LocateDevModule();
            _ = Assert.Throws<InvalidOperationException>(() => KestrunHostManager.Create("noRoot", Log.Logger, EntryScriptPath, [module]));
        }
        finally
        {
            // restore
            field.SetValue(null, original);
        }
    }
    private static KestrunHost NewHost(string name)
    {
        var module = LocateDevModule();
        var root = Directory.GetCurrentDirectory();
        return new KestrunHost(name, Log.Logger, root, [module]);
    }

    private static void Reset()
    {
        KestrunHostManager.DestroyAll();
        KestrunHostManager.KestrunRoot = Directory.GetCurrentDirectory();
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ConfigureListener_Downgrades_Http3_When_PreviewDisabled()
    {
        Reset();
        using var preview = SetPreviewSwitch(false);
        var host = NewHost("cfg");
        _ = host.ConfigureListener(12345, ipAddress: null, x509Certificate: null, protocols: HttpProtocols.Http1AndHttp2AndHttp3, useConnectionLogging: false);
        Assert.NotEmpty(host.Options.Listeners);
        var last = host.Options.Listeners[^1];
        Assert.Equal(HttpProtocols.Http1AndHttp2, last.Protocols);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ConfigureListener_Keeps_Http3_When_PreviewEnabled()
    {
        Reset();
        using var preview = SetPreviewSwitch(true);
        var host = NewHost("cfg2");
        _ = host.ConfigureListener(12346, ipAddress: null, x509Certificate: null, protocols: HttpProtocols.Http1AndHttp2AndHttp3, useConnectionLogging: true);
        Assert.NotEmpty(host.Options.Listeners);
        var last = host.Options.Listeners[^1];
        Assert.Equal(HttpProtocols.Http1AndHttp2AndHttp3, last.Protocols);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task StopAsync_On_Disposed_Host_Does_Not_Throw()
    {
        Reset();
        var name = "disp";
        _ = KestrunHostManager.Create(name, EntryScriptPath, () => NewHost(name));
        var host = KestrunHostManager.Get(name)!;
        host.Dispose();

        // Host remains in manager; StopAsync should be a no-op and not throw
        await KestrunHostManager.StopAsync(name);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Duplicate_Create_Throws()
    {
        Reset();
        _ = KestrunHostManager.Create("dup", EntryScriptPath, () => NewHost("dup"));
        _ = Assert.Throws<InvalidOperationException>(() => KestrunHostManager.Create("dup", EntryScriptPath, () => NewHost("dup")));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Stop_Synchronous_On_Unknown_Name_NoThrow()
    {
        Reset();
        // Should not throw and should be a no-op
        KestrunHostManager.Stop("unknown");
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void SetDefault_On_Missing_Name_Throws()
    {
        Reset();
        _ = Assert.Throws<InvalidOperationException>(() => KestrunHostManager.SetDefault("missing"));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void SetDefault_Changes_Default_Instance()
    {
        Reset();
        _ = KestrunHostManager.Create("a", EntryScriptPath, () => NewHost("a"));
        _ = KestrunHostManager.Create("b", EntryScriptPath, () => NewHost("b"));
        KestrunHostManager.SetDefault("b");
        var def = KestrunHostManager.Default;
        Assert.NotNull(def);
        Assert.Equal("b", def.Options.ApplicationName);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Contains_Reflects_Lifecycle()
    {
        Reset();
        Assert.False(KestrunHostManager.Contains("c1"));
        _ = KestrunHostManager.Create("c1", EntryScriptPath, () => NewHost("c1"));
        Assert.True(KestrunHostManager.Contains("c1"));
        KestrunHostManager.Destroy("c1");
        Assert.False(KestrunHostManager.Contains("c1"));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void TryGet_Returns_Host_When_Exists_And_Null_When_Missing()
    {
        Reset();
        _ = KestrunHostManager.Create("t1", EntryScriptPath, () => NewHost("t1"));
        var found = KestrunHostManager.TryGet("t1", out var host1);
        Assert.True(found);
        Assert.NotNull(host1);

        var notFound = KestrunHostManager.TryGet("missing", out var host2);
        Assert.False(notFound);
        Assert.Null(host2);
    }
}
