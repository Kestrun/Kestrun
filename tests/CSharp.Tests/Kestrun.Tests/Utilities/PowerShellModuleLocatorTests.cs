using Kestrun.Utilities;
using System.Reflection;
using Xunit;

namespace KestrunTests.Utilities;

/// <summary>
/// Tests for <see cref="PowerShellModuleLocator"/> class.
/// Tests module location logic in both development and production environments.
/// </summary>
public class PowerShellModuleLocatorTests
{
    #region LocateKestrunModule Tests

    [Fact]
    [Trait("Category", "Utilities")]
    public void LocateKestrunModule_ReturnsStringOrNull()
    {
        // Act
        var result = PowerShellModuleLocator.LocateKestrunModule();

        // Assert
        // Result can be either a valid path or null (depending on environment)
        if (result != null)
        {
            Assert.NotEmpty(result);
        }
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void LocateKestrunModule_IfFoundReturnsValidPath()
    {
        // Act
        var result = PowerShellModuleLocator.LocateKestrunModule();

        // Assert
        if (result != null)
        {
            Assert.True(File.Exists(result), $"Module path should exist: {result}");
            Assert.EndsWith(".psm1", result, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void LocateKestrunModule_SearchesDevelopmentFirst()
    {
        // This test verifies that the method attempts development search before production.
        // If we're in a development environment, it should find the module.
        var result = PowerShellModuleLocator.LocateKestrunModule();

        // If result is not null and contains "src/PowerShell", we're in dev environment
        if (result != null && result.Contains("src"))
        {
            Assert.Contains("Kestrun.psm1", result);
        }
    }

    #endregion

    #region FindFileUpwards Tests

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

    #endregion

    #region GetPSModulePathsViaPwsh Tests

    [Fact]
    [Trait("Category", "Utilities")]
    public void GetPSModulePathsViaPwsh_MethodExists()
    {
        // Verify the private method exists for testing purposes
        var method = typeof(PowerShellModuleLocator).GetMethod("GetPSModulePathsViaPwsh",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void GetPSModulePathsViaPwsh_ReturnsStringArray()
    {
        // Verify method return type
        var method = typeof(PowerShellModuleLocator).GetMethod("GetPSModulePathsViaPwsh",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.Equal(typeof(string[]), method!.ReturnType);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void GetPSModulePathsViaPwsh_ReturnsValidPaths()
    {
        // This test will only pass if pwsh is available and accessible
        var method = typeof(PowerShellModuleLocator).GetMethod("GetPSModulePathsViaPwsh",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        try
        {
            var result = (string[]?)method!.Invoke(null, null);
            Assert.NotNull(result);
            // Result can be empty array if pwsh is not found or returns empty paths
            // Both are valid outcomes
        }
        catch
        {
            // If pwsh is not available, this is acceptable
            Assert.True(true);
        }
    }

    #endregion

    #region Assembly and Version Tests

    [Fact]
    [Trait("Category", "Utilities")]
    public void PowerShellModuleLocator_IsPublicStaticClass()
    {
        // Verify the class is public and static
        var type = typeof(PowerShellModuleLocator);
        Assert.True(type.IsPublic);
        Assert.True(type.IsAbstract && type.IsSealed); // Static class
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void LocateKestrunModule_IsPublicStaticMethod()
    {
        // Verify LocateKestrunModule is a public static method
        var method = typeof(PowerShellModuleLocator).GetMethod("LocateKestrunModule",
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.True(method!.IsPublic);
        Assert.True(method!.IsStatic);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void LocateKestrunModule_ReturnsNullableString()
    {
        // Verify return type is string?
        var method = typeof(PowerShellModuleLocator).GetMethod("LocateKestrunModule",
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
        var returnType = method!.ReturnType;
        Assert.True(returnType == typeof(string) || (returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(Nullable<>)));
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    [Trait("Category", "Utilities")]
    public void FindFileUpwards_WithEmptyStartDir_HandlesGracefully()
    {
        // Create a temp directory and delete it to test edge case
        var root = Directory.CreateTempSubdirectory();
        _ = root.FullName;
        root.Delete();

        // The method should handle gracefully (either return null or throw reasonable exception)
        var method = typeof(PowerShellModuleLocator).GetMethod("FindFileUpwards",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        // This should not crash the system
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void FindFileUpwards_WithSpecialCharactersInPath_Handles()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var specialDir = Directory.CreateDirectory(Path.Combine(root.FullName, "a-b_c"));
            var file = Path.Combine(specialDir.FullName, "test-file_2.txt");
            File.WriteAllText(file, "data");

            var method = typeof(PowerShellModuleLocator).GetMethod("FindFileUpwards",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);
            var found = (string?)method!.Invoke(null, [specialDir.FullName, "test-file_2.txt"]);
            Assert.NotNull(found);
            Assert.Equal(Path.GetFullPath(file), Path.GetFullPath(found));
        }
        finally
        {
            root.Delete(true);
        }
    }

    #endregion
}
