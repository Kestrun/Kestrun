using System.Text;
using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utilities;

public class AssemblyAutoLoaderMoreTests
{
    [Fact]
    [Trait("Category", "Utilities")]
    public void PreloadAll_Throws_When_NoDirectories_Provided()
    {
        // Ensure a clean state
        AssemblyAutoLoader.Clear(clearSearchDirs: true);

        var ex = Assert.Throws<ArgumentException>(() => AssemblyAutoLoader.PreloadAll());
        Assert.Contains("At least one folder is required.", ex.Message);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void PreloadAll_Ignores_NonExisting_Directories()
    {
        AssemblyAutoLoader.Clear(clearSearchDirs: true);

        var bogus = Path.Combine(Path.GetTempPath(), "kestrun-autoloader-tests", Guid.NewGuid().ToString("N"), "does-not-exist");
        // Should not throw even if the directory doesn't exist
        AssemblyAutoLoader.PreloadAll(verbose: false, bogus);

        // Clean up
        AssemblyAutoLoader.Clear(clearSearchDirs: true);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void PreloadAll_Verbose_Writes_To_Console()
    {
        AssemblyAutoLoader.Clear(clearSearchDirs: true);

        var tempDir = Path.Combine(Path.GetTempPath(), "kestrun-autoloader-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);
        try
        {
            var sw = new StringWriter(new StringBuilder());
            var orig = Console.Out;
            Console.SetOut(sw);
            try
            {
                AssemblyAutoLoader.PreloadAll(verbose: true, tempDir);
            }
            finally
            {
                Console.SetOut(orig);
            }

            var output = sw.ToString();
            Assert.Contains("Installing AssemblyResolve hook for Kestrun.Utilities", output);
            Assert.Contains("Adding search directory:", output);
        }
        finally
        {
            AssemblyAutoLoader.Clear(clearSearchDirs: true);
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void PreloadAll_Merges_Directories_And_Is_Idempotent()
    {
        AssemblyAutoLoader.Clear(clearSearchDirs: true);

        var dir1 = Path.Combine(Path.GetTempPath(), "kestrun-autoloader-tests", Guid.NewGuid().ToString("N"), "a");
        var dir2 = Path.Combine(Path.GetTempPath(), "kestrun-autoloader-tests", Guid.NewGuid().ToString("N"), "b");
        _ = Directory.CreateDirectory(dir1);
        _ = Directory.CreateDirectory(dir2);

        try
        {
            // First call with dir1
            AssemblyAutoLoader.PreloadAll(false, dir1);
            // Second call adds dir2 and repeats dir1
            AssemblyAutoLoader.PreloadAll(false, dir1, dir2);
            // Third call with reversed order should still be fine
            AssemblyAutoLoader.PreloadAll(false, dir2, dir1);
        }
        finally
        {
            AssemblyAutoLoader.Clear(clearSearchDirs: true);
            try { Directory.Delete(Path.GetDirectoryName(dir1)!, recursive: true); } catch { /* ignore */ }
            try { Directory.Delete(Path.GetDirectoryName(dir2)!, recursive: true); } catch { /* ignore */ }
        }
    }
}
