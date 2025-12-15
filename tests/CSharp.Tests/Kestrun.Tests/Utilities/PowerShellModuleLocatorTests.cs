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

    [Fact]
    [Trait("Category", "Utilities")]
    public void FindFileUpwards_FindsFileMultipleLevelsUp()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            // Create directory structure: root/a/b/c
            var c = Directory.CreateDirectory(Path.Combine(root.FullName, "a", "b", "c"));
            // Create file at root/a/file.txt
            var file = Path.Combine(root.FullName, "a", "file.txt");
            File.WriteAllText(file, "data");

            var method = typeof(PowerShellModuleLocator).GetMethod("FindFileUpwards", BindingFlags.NonPublic | BindingFlags.Static)!;
            var found = (string?)method.Invoke(null, [c.FullName, Path.Combine("..", "..", "file.txt")]);
            Assert.Equal(Path.GetFullPath(file), Path.GetFullPath(found!));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void FindFileUpwards_ReturnsNullWhenFileNotFound()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "a", "b"));

            var method = typeof(PowerShellModuleLocator).GetMethod("FindFileUpwards", BindingFlags.NonPublic | BindingFlags.Static)!;
            var found = (string?)method.Invoke(null, [nested.FullName, "nonexistent.txt"]);
            Assert.Null(found);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void FindFileUpwards_WithSingleDirectory_FindsFileInRoot()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var file = Path.Combine(root.FullName, "test.txt");
            File.WriteAllText(file, "data");

            var method = typeof(PowerShellModuleLocator).GetMethod("FindFileUpwards", BindingFlags.NonPublic | BindingFlags.Static)!;
            var found = (string?)method.Invoke(null, [root.FullName, "test.txt"]);
            Assert.Equal(Path.GetFullPath(file), Path.GetFullPath(found!));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void FindFileUpwards_WithRelativePath_ResolvesProperly()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var level1 = Directory.CreateDirectory(Path.Combine(root.FullName, "level1"));
            var level2 = Directory.CreateDirectory(Path.Combine(level1.FullName, "level2"));
            var file = Path.Combine(level1.FullName, "target.txt");
            File.WriteAllText(file, "data");

            var method = typeof(PowerShellModuleLocator).GetMethod("FindFileUpwards", BindingFlags.NonPublic | BindingFlags.Static)!;
            var found = (string?)method.Invoke(null, [level2.FullName, "target.txt"]);
            Assert.NotNull(found);
            Assert.True(File.Exists(found));
        }
        finally
        {
            root.Delete(true);
        }
    }
}
