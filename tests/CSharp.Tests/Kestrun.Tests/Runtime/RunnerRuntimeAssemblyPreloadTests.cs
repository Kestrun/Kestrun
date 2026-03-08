
#if NET10_0_OR_GREATER
using Kestrun;
using Kestrun.Runner;
using Xunit;
#endif 

namespace KestrunTests.Runtime;

#if NET10_0_OR_GREATER
public class RunnerRuntimeAssemblyPreloadTests
{
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
            Assert.DoesNotContain(loadedPath!, warning, StringComparison.OrdinalIgnoreCase);
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
}
#endif
