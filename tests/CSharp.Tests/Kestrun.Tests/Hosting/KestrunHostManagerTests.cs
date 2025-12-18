using Kestrun;
using Kestrun.Hosting;
using Serilog;
using Xunit;

namespace KestrunTests.Hosting;

[Collection("SharedStateSerial")]
public class KestrunHostManagerTests
{
    private static string GetSafeStartDirectory()
    {
        try
        {
            return Directory.GetCurrentDirectory();
        }
        catch (Exception ex) when (ex is IOException
                                   || ex is UnauthorizedAccessException
                                   || ex is DirectoryNotFoundException
                                   || ex is FileNotFoundException)
        {
            return AppContext.BaseDirectory;
        }
    }

    private static string LocateDevModule()
    {
        // Walk upwards to find src/PowerShell/Kestrun/Kestrun.psm1 from current directory
        var current = GetSafeStartDirectory();
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

    private static KestrunHost NewHost(string name)
    {
        var module = LocateDevModule();
        // Provide kestrunRoot to keep constructor quiet and avoid chdir
        var root = GetSafeStartDirectory();
        return new KestrunHost(name, Log.Logger, root, [module]);
    }

    private static void Reset()
    {
        // Destroy all instances and reset default
        KestrunHostManager.DestroyAll();
        // Ensure root is set to current directory so Create won't throw
        KestrunHostManager.KestrunRoot = GetSafeStartDirectory();
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void KestrunRoot_Set_Validates_And_Sets_Cwd_When_Different()
    {
        var cwd = GetSafeStartDirectory();
        try
        {
            // Setting to current dir should be a no-op
            KestrunHostManager.KestrunRoot = cwd;
            Assert.Equal(cwd, KestrunHostManager.KestrunRoot);

            // Setting empty throws
            var ex = Assert.Throws<ArgumentException>(() => KestrunHostManager.KestrunRoot = " ");
            Assert.Contains("cannot be null or empty", ex.Message);
        }
        finally
        {
            Directory.SetCurrentDirectory(cwd);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Create_WithFactory_And_Default_Selection_Works()
    {
        Reset();
        var h1 = KestrunHostManager.Create("h1", () => NewHost("h1"), setAsDefault: true);
        Assert.Same(h1, KestrunHostManager.Default);
        Assert.Contains("h1", KestrunHostManager.InstanceNames);
        Assert.True(KestrunHostManager.Contains("h1"));
        Assert.Same(h1, KestrunHostManager.Get("h1"));

        // Duplicate name throws
        _ = Assert.Throws<InvalidOperationException>(() => KestrunHostManager.Create("h1", () => NewHost("h1")));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Create_WithLogger_Requires_Root_And_Allows_SetDefault()
    {
        Reset();
        var module = LocateDevModule();
        // Create overload with logger and module paths
        var h2 = KestrunHostManager.Create("h2", Log.Logger, [module], setAsDefault: true);
        Assert.Same(h2, KestrunHostManager.Default);
        Assert.Equal(h2, KestrunHostManager.Get("h2"));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void SetDefault_And_TryGet_Get_Behavior()
    {
        Reset();
        _ = KestrunHostManager.Create("a", () => NewHost("a"));
        _ = KestrunHostManager.Create("b", () => NewHost("b"));

        KestrunHostManager.SetDefault("b");
        Assert.Equal("b", KestrunHostManager.Default?.Options.ApplicationName);

        Assert.True(KestrunHostManager.TryGet("a", out var gotA));
        Assert.NotNull(gotA);

        Assert.Null(KestrunHostManager.Get("missing"));
        Assert.False(KestrunHostManager.TryGet("missing", out _));

        _ = Assert.Throws<InvalidOperationException>(() => KestrunHostManager.SetDefault("missing"));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task IsRunning_And_Stop_On_Missing_Do_Not_Throw()
    {
        Reset();
        _ = KestrunHostManager.Create("srv", () => NewHost("srv"));
        Assert.False(KestrunHostManager.IsRunning("srv"));

        // Stop on missing name should be a no-op
        KestrunHostManager.Stop("nope");

        // StartAsync/StopAsync on missing names throw
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => KestrunHostManager.StartAsync("nope"));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => KestrunHostManager.StopAsync("nope"));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task StopAllAsync_Does_Not_Throw_For_Unstarted_Hosts()
    {
        Reset();
        _ = KestrunHostManager.Create("x", () => NewHost("x"));
        _ = KestrunHostManager.Create("y", () => NewHost("y"));
        await KestrunHostManager.StopAllAsync();
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Destroy_And_DestroyAll_Update_Default()
    {
        Reset();
        _ = KestrunHostManager.Create("first", () => NewHost("first"), setAsDefault: true);
        _ = KestrunHostManager.Create("second", () => NewHost("second"));

        // Destroy default should reassign default to remaining or null
        KestrunHostManager.Destroy("first");
        Assert.NotNull(KestrunHostManager.Default);

        // Destroy all clears everything
        KestrunHostManager.DestroyAll();
        Assert.Null(KestrunHostManager.Default);
        Assert.Empty(KestrunHostManager.InstanceNames);
    }
}
