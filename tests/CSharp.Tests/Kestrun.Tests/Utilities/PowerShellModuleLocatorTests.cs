using Kestrun.Utilities;
using System.Reflection;
using Xunit;

namespace KestrunTests.Utilities;

public class PowerShellModuleLocatorTests
{
    [Fact]
    [Trait("Category", "Utilities")]
    public void FindFileUpwards_FindsFile()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "a", "b"));
            var targetDir = Directory.CreateDirectory(Path.Combine(root.FullName, "a"));
            var file = Path.Combine(targetDir.FullName, "test.txt");
            File.WriteAllText(file, "data");

            var method = typeof(PowerShellModuleLocator).GetMethod("FindFileUpwards", BindingFlags.NonPublic | BindingFlags.Static)!;
            var found = (string?)method.Invoke(null, [nested.FullName, Path.Combine("..", "test.txt")]);
            Assert.Equal(Path.GetFullPath(file), Path.GetFullPath(found!));
        }
        finally
        {
            root.Delete(true);
        }
    }
}
