
#if NET10_0_OR_GREATER
using Kestrun;
using Kestrun.Runner;
using System.Reflection;
using Xunit;
#endif

namespace KestrunTests.Runtime;

#if NET10_0_OR_GREATER
public class RunnerRuntimeAssemblyPreloadTests
{
    [Fact]
    public void EnsureKestrunAssemblyPreloaded_WhenAlreadyLoadedFromDifferentPathAndCompatible_DoesNotOverwriteResolverModuleLibPath()
    {
        var loadedAssembly = typeof(KestrunHostManager).Assembly;
        var loadedPath = loadedAssembly.Location;
        Assert.False(string.IsNullOrWhiteSpace(loadedPath));

        var tempRoot = Path.Combine(Path.GetTempPath(), "kestrun-runner-tests", Guid.NewGuid().ToString("N"));
        var moduleRoot = Path.Combine(tempRoot, "Kestrun");
        var libDirectory = Path.Combine(moduleRoot, "lib", "net10.0");
        var manifestPath = Path.Combine(moduleRoot, "Kestrun.psd1");
        var copiedAssemblyPath = Path.Combine(libDirectory, "Kestrun.dll");

        Directory.CreateDirectory(libDirectory);
        File.WriteAllText(manifestPath, "@{}\n");
        File.Copy(loadedPath!, copiedAssemblyPath, overwrite: true);

        var originalModuleLibPath = GetResolverModuleLibPath();
        var sentinelPath = Path.Combine(tempRoot, "sentinel", "lib", "net10.0");

        SetResolverModuleLibPath(sentinelPath);
        try
        {
            RunnerRuntime.EnsureKestrunAssemblyPreloaded(manifestPath);
            Assert.Equal(sentinelPath, GetResolverModuleLibPath());
        }
        finally
        {
            SetResolverModuleLibPath(originalModuleLibPath);
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for temporary test files.
            }
        }
    }

    [Fact]
    public void EnsureKestrunAssemblyPreloaded_WhenAlreadyLoadedFromDifferentPathAndCompatible_WarnsAndContinues()
    {
        var loadedAssembly = typeof(KestrunHostManager).Assembly;
        Assert.Equal("Kestrun", loadedAssembly.GetName().Name);

        var loadedPath = loadedAssembly.Location;
        Assert.False(string.IsNullOrWhiteSpace(loadedPath));

        var tempRoot = Path.Combine(Path.GetTempPath(), "kestrun-runner-tests", Guid.NewGuid().ToString("N"));
        var moduleRoot = Path.Combine(tempRoot, "Kestrun");
        var libDirectory = Path.Combine(moduleRoot, "lib", "net10.0");
        var manifestPath = Path.Combine(moduleRoot, "Kestrun.psd1");
        var copiedAssemblyPath = Path.Combine(libDirectory, "Kestrun.dll");

        Directory.CreateDirectory(libDirectory);
        File.WriteAllText(manifestPath, "@{}\n");
        File.Copy(loadedPath!, copiedAssemblyPath, overwrite: true);

        try
        {
            string? warning = null;

            RunnerRuntime.EnsureKestrunAssemblyPreloaded(
                manifestPath,
                message => warning = message);

            Assert.False(string.IsNullOrWhiteSpace(warning));
            Assert.Contains("already loaded from a different location", warning, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(copiedAssemblyPath, warning, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for temporary test files.
            }
        }
    }

    private static string? GetResolverModuleLibPath()
    {
        var field = typeof(RunnerRuntime).GetField("s_kestrunModuleLibPath", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return (string?)field.GetValue(null);
    }

    private static void SetResolverModuleLibPath(string? value)
    {
        var field = typeof(RunnerRuntime).GetField("s_kestrunModuleLibPath", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        field.SetValue(null, value);
    }
}
#endif
