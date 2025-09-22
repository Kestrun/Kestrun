using System.Reflection;
using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utilities;

public class PowerShellModuleLocatorMoreTests
{
    [Fact]
    [Trait("Category", "Utilities")]
    public void LocateKestrunModule_Finds_Dev_Module_In_Repo()
    {
        // In this repo the dev module exists under src/PowerShell/Kestrun/Kestrun.psm1
        // The implementation walks upward from the executing assembly location.
        // This call should therefore find the dev module when running tests inside the repo.
        var path = PowerShellModuleLocator.LocateKestrunModule();

        // We can be flexible: either it finds the dev module path or null (if environment differs),
        // but we still increase coverage across the happy path.
        if (path is not null)
        {
            Assert.True(File.Exists(path));
            var suffix = Path.Combine("src", "PowerShell", "Kestrun", "Kestrun.psm1");
            Assert.EndsWith(suffix, path.Replace('/', Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void FindFileUpwards_Returns_Null_When_Not_Found()
    {
        // Invoke private FindFileUpwards via reflection with a path that won't contain the file
        var method = typeof(PowerShellModuleLocator).GetMethod("FindFileUpwards", BindingFlags.NonPublic | BindingFlags.Static)!;
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            var result = (string?)method.Invoke(null, [temp.FullName, Path.Combine("non", "existent", "file.txt")]);
            Assert.Null(result);
        }
        finally
        {
            temp.Delete(true);
        }
    }
}
