using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utility;

public class AssemblyAutoLoaderExtraTests
{
    [Fact]
    [Trait("Category", "Utility")]
    public void Clear_DoesNotThrow_WhenNotInstalled() => AssemblyAutoLoader.Clear(clearSearchDirs: true);
}
