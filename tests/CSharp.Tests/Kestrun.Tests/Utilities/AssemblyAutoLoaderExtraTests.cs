using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utilities;

[Collection("SharedStateSerial")]
public class AssemblyAutoLoaderExtraTests
{
    [Fact]
    [Trait("Category", "Utilities")]
    public void Clear_DoesNotThrow_WhenNotInstalled() => AssemblyAutoLoader.Clear(clearSearchDirs: true);
}
