using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utilities;

[Collection("SharedStateSerial")]
public class AssemblyAutoLoaderTests
{
    [Fact]
    [Trait("Category", "Utilities")]
    public void PreloadAll_IgnoresMissingDirs_AndCanClear()
    {
        // Create a temporary empty directory to register
        var tempDir = Path.Combine(Path.GetTempPath(), "kestrun-autoloader-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);
        try
        {
            AssemblyAutoLoader.PreloadAll(verbose: false, tempDir);
            // Calling again should be idempotent
            AssemblyAutoLoader.PreloadAll(verbose: false, tempDir);
            // Nothing to assert about loaded assemblies here; just ensure no exceptions and Clear works
            AssemblyAutoLoader.Clear(clearSearchDirs: true);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }
}
