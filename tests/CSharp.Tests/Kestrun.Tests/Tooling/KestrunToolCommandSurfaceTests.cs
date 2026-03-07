#if NET10_0_OR_GREATER
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Runtime.InteropServices;
using Xunit;

namespace KestrunTests.Tooling;

public class KestrunToolCommandSurfaceTests
{
    private static readonly Type ProgramType = ResolveProgramType();

    [Fact]
    [Trait("Category", "Tooling")]
    public void ParseGlobalOptions_Recognizes_NoCheck_AndPreservesModuleCommand()
    {
        var args = new[] { "--nocheck", "module", "install", "--version", "1.2.3" };
        var result = InvokeWithStringArray("ParseGlobalOptions", args);

        Assert.True(GetResultBoolean(result, "SkipGalleryCheck"));
        Assert.Equal(["module", "install", "--version", "1.2.3"], GetResultCommandArgs(result));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void ParseGlobalOptions_DoesNotConsume_NoCheck_AfterSentinel()
    {
        var input = new[] { "run", "--arguments", "--nocheck" };
        var result = InvokeWithStringArray("ParseGlobalOptions", input);

        Assert.False(GetResultBoolean(result, "SkipGalleryCheck"));
        Assert.Equal(input, GetResultCommandArgs(result));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void IsNoCheckOption_Accepts_Aliases()
    {
        Assert.True((bool)Invoke("IsNoCheckOption", "--nocheck")!);
        Assert.True((bool)Invoke("IsNoCheckOption", "--no-check")!);
        Assert.False((bool)Invoke("IsNoCheckOption", "--check")!);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void FilterGlobalOptions_RemovesNoCheck_ForMetaCommands()
    {
        var input = new[] { "--nocheck", "version" };
        var filtered = (List<string>)InvokeWithStringArray("FilterGlobalOptions", input)!;

        Assert.Equal(["version"], filtered);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleInstall_WithVersion_Succeeds()
    {
        var parse = InvokeTryParseArguments(["module", "install", "--version", "1.2.3"]);

        Assert.True(parse.Success);
        Assert.Equal("ModuleInstall", GetParsedCommandMode(parse.ParsedCommand!));
        Assert.Equal("1.2.3", GetParsedCommandField(parse.ParsedCommand!, "ModuleVersion"));
        Assert.Equal("Local", GetParsedCommandField(parse.ParsedCommand!, "ModuleScope"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleInstall_WithGlobalScope_Succeeds()
    {
        var parse = InvokeTryParseArguments(["module", "install", "--scope", "global"]);

        Assert.True(parse.Success);
        Assert.Equal("ModuleInstall", GetParsedCommandMode(parse.ParsedCommand!));
        Assert.Equal("Global", GetParsedCommandField(parse.ParsedCommand!, "ModuleScope"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleUpdate_WithForce_Succeeds()
    {
        var parse = InvokeTryParseArguments(["module", "update", "--force"]);

        Assert.True(parse.Success);
        Assert.Equal("ModuleUpdate", GetParsedCommandMode(parse.ParsedCommand!));
        Assert.Equal("True", GetParsedCommandField(parse.ParsedCommand!, "ModuleForce"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleInstall_WithForce_Fails()
    {
        var parse = InvokeTryParseArguments(["module", "install", "--force"]);

        Assert.False(parse.Success);
        Assert.Contains("does not accept --force", parse.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleInstall_WithInvalidScope_Fails()
    {
        var parse = InvokeTryParseArguments(["module", "install", "--scope", "team"]);

        Assert.False(parse.Success);
        Assert.Contains("Unknown module scope", parse.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleRemove_WithoutVersion_Succeeds()
    {
        var parse = InvokeTryParseArguments(["module", "remove"]);

        Assert.True(parse.Success);
        Assert.Equal("ModuleRemove", GetParsedCommandMode(parse.ParsedCommand!));
        Assert.Null(GetParsedCommandField(parse.ParsedCommand!, "ModuleVersion"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleRemove_WithVersion_Fails()
    {
        var parse = InvokeTryParseArguments(["module", "remove", "--version", "1.2.3"]);

        Assert.False(parse.Success);
        Assert.Contains("does not accept --version", parse.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleInfo_Succeeds()
    {
        var parse = InvokeTryParseArguments(["module", "info"]);

        Assert.True(parse.Success);
        Assert.Equal("ModuleInfo", GetParsedCommandMode(parse.ParsedCommand!));
        Assert.Null(GetParsedCommandField(parse.ParsedCommand!, "ModuleVersion"));
        Assert.Equal("Local", GetParsedCommandField(parse.ParsedCommand!, "ModuleScope"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleCommand_AllowsLauncherInjectedKestrunOptions()
    {
        var parse = InvokeTryParseArguments([
            "--kestrun-folder",
            "C:\\temp\\module",
            "--kestrun-manifest",
            "C:\\temp\\module\\Kestrun.psd1",
            "module",
            "info",
        ]);

        Assert.True(parse.Success);
        Assert.Equal("ModuleInfo", GetParsedCommandMode(parse.ParsedCommand!));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleInstall_MissingVersionValue_Fails()
    {
        var parse = InvokeTryParseArguments(["module", "install", "--version"]);

        Assert.False(parse.Success);
        Assert.Contains("Missing value for --version", parse.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void WriteModuleNotFoundMessage_AlwaysIncludes_ModuleInstallGuidance()
    {
        var lines = new List<string>();
        _ = Invoke("WriteModuleNotFoundMessage", null, null, new Action<string>(lines.Add));

        Assert.Contains(lines, line => line.Contains("module install", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("No Kestrun module was found", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void GetDefaultPowerShellModulePath_UsesExpectedFolderConvention()
    {
        var path = (string)Invoke("GetDefaultPowerShellModulePath")!;

        Assert.EndsWith("Modules", path, StringComparison.OrdinalIgnoreCase);

        if (OperatingSystem.IsWindows())
        {
            Assert.Contains("PowerShell", path, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Contains(Path.Combine(".local", "share", "powershell"), path, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryReadPackageVersion_ReturnsStableVersion_WhenNuspecPrereleaseExists()
    {
        const string nuspec = """
<?xml version="1.0"?>
<package>
  <metadata>
    <id>Kestrun</id>
    <version>1.0.0</version>
    <prerelease>beta4</prerelease>
  </metadata>
</package>
""";

        var packageBytes = CreatePackageWithNuspec(nuspec);
        var result = InvokeTryReadPackageVersion(packageBytes);

        Assert.True(result.Success);
        Assert.Equal("1.0.0", result.Version);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryValidateInstallAction_Fails_WhenModuleAlreadyInstalled()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            var versionFolder = Path.Combine(tempRoot, "Kestrun", "1.0.0");
            _ = Directory.CreateDirectory(versionFolder);

            File.WriteAllText(
                Path.Combine(versionFolder, "Kestrun.psd1"),
                "@{`n    ModuleVersion = '1.0.0'`n}",
                Encoding.UTF8);

            var result = InvokeTryValidateInstallAction(Path.Combine(tempRoot, "Kestrun"), "local");
            Assert.False(result.Success);
            Assert.Contains("module update", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryValidateUpdateAction_Fails_WhenTargetVersionFolderExists_WithoutForce()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(Path.Combine(tempRoot, "Kestrun", "1.0.0"));

            var result = InvokeTryValidateUpdateAction(Path.Combine(tempRoot, "Kestrun"), "1.0.0", force: false);
            Assert.False(result.Success);
            Assert.Contains("--force", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryValidateUpdateAction_Succeeds_WhenTargetVersionFolderExists_WithForce()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(Path.Combine(tempRoot, "Kestrun", "1.0.0"));

            var result = InvokeTryValidateUpdateAction(Path.Combine(tempRoot, "Kestrun"), "1.0.0", force: true);
            Assert.True(result.Success);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryReadModuleSemanticVersionFromManifest_IncludesPrereleaseSuffix_WhenPresent()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            var manifestPath = Path.Combine(tempRoot, "Kestrun.psd1");
            File.WriteAllText(
                manifestPath,
                """
@{
    ModuleVersion = '1.0.0'
    PrivateData = @{
        PSData = @{
            Prerelease = 'beta3'
        }
    }
}
""",
                Encoding.UTF8);

            var result = InvokeTryReadModuleSemanticVersionFromManifest(manifestPath);
            Assert.True(result.Success);
            Assert.Equal("1.0.0-beta3", result.Version);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void CompareModuleVersionValues_OrdersPrereleaseSuffixesWithinSameBaseVersion()
    {
        var comparison = (int)Invoke("CompareModuleVersionValues", "1.0.0-beta4", "1.0.0-beta3")!;
        Assert.True(comparison > 0);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void GetInstalledModuleRecords_UsesManifestPrerelease_WhenDirectoryIsStableVersion()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            var stableFolder = Path.Combine(tempRoot, "Kestrun", "1.0.0");
            _ = Directory.CreateDirectory(stableFolder);

            var manifestPath = Path.Combine(stableFolder, "Kestrun.psd1");
            File.WriteAllText(
                manifestPath,
                """
@{
    ModuleVersion = '1.0.0'
    PrivateData = @{
        PSData = @{
            Prerelease = 'beta3'
        }
    }
}
""",
                Encoding.UTF8);

            var records = (System.Collections.IEnumerable)Invoke("GetInstalledModuleRecords", Path.Combine(tempRoot, "Kestrun"))!;
            var firstRecord = records.Cast<object>().First();

            Assert.Equal("1.0.0-beta3", GetRecordField(firstRecord, "Version"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void GetInstalledModuleRecords_NormalizesPrereleaseVersionDirectoryToStableVersion()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            var prereleaseFolder = Path.Combine(tempRoot, "Kestrun", "1.0.0-rc1");
            _ = Directory.CreateDirectory(prereleaseFolder);

            var manifestPath = Path.Combine(prereleaseFolder, "Kestrun.psd1");
            File.WriteAllText(
                manifestPath,
                "@{`n    ModuleVersion = '1.0.0'`n}",
                Encoding.UTF8);

            var records = (System.Collections.IEnumerable)Invoke("GetInstalledModuleRecords", Path.Combine(tempRoot, "Kestrun"))!;
            var firstRecord = records.Cast<object>().First();

            Assert.Equal("1.0.0", GetRecordField(firstRecord, "Version"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryRemoveInstalledModule_RemovesModuleRootAndContents()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            var moduleRoot = Path.Combine(tempRoot, "Kestrun");
            var nestedDirectory = Path.Combine(moduleRoot, "1.0.0", "nested");
            _ = Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(Path.Combine(moduleRoot, "1.0.0", "Kestrun.psd1"), "@{ ModuleVersion = '1.0.0' }", Encoding.UTF8);
            File.WriteAllText(Path.Combine(nestedDirectory, "notes.txt"), "sample", Encoding.UTF8);

            var result = InvokeTryRemoveInstalledModule(moduleRoot, showProgress: false);

            Assert.True(result.Success);
            Assert.False(Directory.Exists(moduleRoot));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryRemoveInstalledModule_Succeeds_WhenModuleRootMissing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            var moduleRoot = Path.Combine(tempRoot, "Kestrun");
            var result = InvokeTryRemoveInstalledModule(moduleRoot, showProgress: false);

            Assert.True(result.Success);
            Assert.True(string.IsNullOrWhiteSpace(result.Error));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveServiceRuntimeExecutableFromModule_ReturnsRuntimePath_WhenRuntimeExists()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            var moduleRoot = Path.Combine(tempRoot, "module");
            _ = Directory.CreateDirectory(moduleRoot);
            var manifestPath = Path.Combine(moduleRoot, "Kestrun.psd1");
            File.WriteAllText(manifestPath, "@{`n    ModuleVersion = '1.0.0'`n}", Encoding.UTF8);

            var runtimeRid = GetRuntimeRidForCurrentProcess();
            var runtimeBinaryName = OperatingSystem.IsWindows() ? "kestrun.exe" : "kestrun";
            var runtimePath = Path.Combine(moduleRoot, "runtimes", runtimeRid, runtimeBinaryName);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(runtimePath)!);
            File.WriteAllText(runtimePath, "runtime-binary", Encoding.UTF8);

            var result = InvokeTryResolveServiceRuntimeExecutableFromModule(manifestPath);

            Assert.True(result.Success);
            Assert.True(string.IsNullOrWhiteSpace(result.Error));
            Assert.Equal(Path.GetFullPath(runtimePath), result.RuntimePath);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveServiceRuntimeExecutableFromModule_FallsBackToRepoRuntimeLayout_WhenModuleRuntimeMissing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        var originalDirectory = Environment.CurrentDirectory;
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            Environment.CurrentDirectory = tempRoot;

            var moduleRoot = Path.Combine(tempRoot, "module");
            _ = Directory.CreateDirectory(moduleRoot);
            var manifestPath = Path.Combine(moduleRoot, "Kestrun.psd1");
            File.WriteAllText(manifestPath, "@{`n    ModuleVersion = '1.0.0'`n}", Encoding.UTF8);

            var runtimeRid = GetRuntimeRidForCurrentProcess();
            var runtimeBinaryName = OperatingSystem.IsWindows() ? "kestrun.exe" : "kestrun";
            var fallbackRuntimePath = Path.Combine(tempRoot, "src", "PowerShell", "Kestrun", "runtimes", runtimeRid, runtimeBinaryName);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(fallbackRuntimePath)!);
            File.WriteAllText(fallbackRuntimePath, "fallback-runtime", Encoding.UTF8);

            var result = InvokeTryResolveServiceRuntimeExecutableFromModule(manifestPath);

            Assert.True(result.Success);
            Assert.True(string.IsNullOrWhiteSpace(result.Error));

            var resolvedRuntimePath = Path.GetFullPath(result.RuntimePath);
            var expectedSuffix = Path.Combine("src", "PowerShell", "Kestrun", "runtimes", runtimeRid, runtimeBinaryName);
            var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            Assert.True(File.Exists(resolvedRuntimePath));
            Assert.EndsWith(expectedSuffix, resolvedRuntimePath, pathComparison);
            Assert.Equal("fallback-runtime", File.ReadAllText(resolvedRuntimePath, Encoding.UTF8));
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryPrepareServiceBundle_CopiesRuntimeModuleAndScript_UsingOverrideRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            var moduleRoot = Path.Combine(tempRoot, "module-src");
            _ = Directory.CreateDirectory(moduleRoot);
            var manifestPath = Path.Combine(moduleRoot, "Kestrun.psd1");
            File.WriteAllText(manifestPath, "@{`n    ModuleVersion = '1.0.0'`n}", Encoding.UTF8);

            var runtimeRid = GetRuntimeRidForCurrentProcess();
            var runtimeBinaryName = OperatingSystem.IsWindows() ? "kestrun.exe" : "kestrun";
            var runtimeSourcePath = Path.Combine(moduleRoot, "runtimes", runtimeRid, runtimeBinaryName);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(runtimeSourcePath)!);
            File.WriteAllText(runtimeSourcePath, "runtime-binary", Encoding.UTF8);

            var scriptPath = Path.Combine(tempRoot, "server.ps1");
            File.WriteAllText(scriptPath, "Write-Output 'hello'", Encoding.UTF8);

            var bundleRoot = Path.Combine(tempRoot, "bundle-root");
            var result = InvokeTryPrepareServiceBundle("svc:test", scriptPath, manifestPath, bundleRoot);

            Assert.True(result.Success);
            Assert.True(string.IsNullOrWhiteSpace(result.Error));
            Assert.NotNull(result.Bundle);

            Assert.True(Directory.Exists(result.BundleRootPath));
            Assert.True(File.Exists(result.RuntimeExecutablePath));
            Assert.True(File.Exists(result.ScriptPath));
            Assert.True(File.Exists(result.ModuleManifestPath));

            Assert.StartsWith(Path.GetFullPath(bundleRoot), result.BundleRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("runtime-binary", File.ReadAllText(result.RuntimeExecutablePath, Encoding.UTF8));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void BuildWindowsServiceHostArguments_UsesBundledManifestPath()
    {
        var bundledScriptPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "bundle", "script", "server.ps1"));
        var bundledManifestPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "bundle", "module", "Kestrun.psd1"));

        var args = (IReadOnlyList<string>)Invoke(
            "BuildWindowsServiceHostArgumentsCore",
            "demo",
            bundledScriptPath,
            bundledManifestPath,
            Array.Empty<string>(),
            null)!;

        Assert.Contains("--kestrun-manifest", args);
        Assert.Contains(Path.GetFullPath(bundledManifestPath), args);
        Assert.DoesNotContain("--kestrun-folder", args);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void BuildElevatedRelaunchArguments_PrependsCommand_WhenExecutableIsDotnetHost()
    {
        var dotnetExecutablePath = OperatingSystem.IsWindows()
            ? Path.Combine("C:\\", "Program Files", "dotnet", "dotnet.exe")
            : Path.Combine(Path.DirectorySeparatorChar.ToString(), "usr", "bin", "dotnet");

        var result = InvokeBuildElevatedRelaunchArguments(
            dotnetExecutablePath,
            ["service", "install", "--name", "demo"]);

        Assert.NotEmpty(result);
        Assert.True(
            result[0].EndsWith("Kestrun.Tool.dll", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result[0], "kestrun", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["service", "install", "--name", "demo"], result.Skip(1));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void BuildElevatedRelaunchArguments_LeavesArgsUnchanged_WhenExecutableIsToolBinary()
    {
        var result = InvokeBuildElevatedRelaunchArguments(
            @"C:\ProgramData\Kestrun\services\demo\runtime\kestrun.exe",
            ["service", "install", "--name", "demo"]);

        Assert.Equal(["service", "install", "--name", "demo"], result);
    }

    private static object Invoke(string methodName, params object?[] arguments)
        => InvokeRaw(methodName, arguments);

    private static object InvokeRaw(string methodName, object?[] arguments)
    {
        var method = ProgramType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!.Invoke(null, arguments)!;
    }

    private static object InvokeWithStringArray(string methodName, string[] arguments)
        => InvokeRaw(methodName, [arguments]);

    private static (bool Success, object? ParsedCommand, string Error) InvokeTryParseArguments(string[] args)
    {
        var method = ProgramType.GetMethod("TryParseArguments", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var values = new object?[] { args, null, null };
        var success = (bool)method!.Invoke(null, values)!;
        var error = values[2]?.ToString() ?? string.Empty;
        return (success, values[1], error);
    }

    private static (bool Success, string Version) InvokeTryReadPackageVersion(byte[] packageBytes)
    {
        var method = ProgramType.GetMethod("TryReadPackageVersion", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var values = new object?[] { packageBytes, null };
        var success = (bool)method!.Invoke(null, values)!;
        var version = values[1]?.ToString() ?? string.Empty;
        return (success, version);
    }

    private static (bool Success, string Version) InvokeTryReadModuleSemanticVersionFromManifest(string manifestPath)
    {
        var method = ProgramType.GetMethod("TryReadModuleSemanticVersionFromManifest", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var values = new object?[] { manifestPath, null };
        var success = (bool)method!.Invoke(null, values)!;
        var version = values[1]?.ToString() ?? string.Empty;
        return (success, version);
    }

    private static (bool Success, string Error) InvokeTryValidateInstallAction(string moduleRoot, string scopeToken)
    {
        var method = ProgramType.GetMethod("TryValidateInstallAction", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var values = new object?[] { moduleRoot, scopeToken, null };
        var success = (bool)method!.Invoke(null, values)!;
        var error = values[2]?.ToString() ?? string.Empty;
        return (success, error);
    }

    private static (bool Success, string Error) InvokeTryValidateUpdateAction(string moduleRoot, string packageVersion, bool force)
    {
        var method = ProgramType.GetMethod("TryValidateUpdateAction", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var values = new object?[] { moduleRoot, packageVersion, force, null };
        var success = (bool)method!.Invoke(null, values)!;
        var error = values[3]?.ToString() ?? string.Empty;
        return (success, error);
    }

    private static (bool Success, string Error) InvokeTryRemoveInstalledModule(string moduleRoot, bool showProgress)
    {
        var method = ProgramType.GetMethod("TryRemoveInstalledModule", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var values = new object?[] { moduleRoot, showProgress, null };
        var success = (bool)method!.Invoke(null, values)!;
        var error = values[2]?.ToString() ?? string.Empty;
        return (success, error);
    }

    private static (bool Success, string RuntimePath, string Error) InvokeTryResolveServiceRuntimeExecutableFromModule(string manifestPath)
    {
        var method = ProgramType.GetMethod("TryResolveServiceRuntimeExecutableFromModule", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var values = new object?[] { manifestPath, null, null };
        var success = (bool)method!.Invoke(null, values)!;
        var runtimePath = values[1]?.ToString() ?? string.Empty;
        var error = values[2]?.ToString() ?? string.Empty;
        return (success, runtimePath, error);
    }

    private static (bool Success, object? Bundle, string BundleRootPath, string RuntimeExecutablePath, string ScriptPath, string ModuleManifestPath, string Error)
        InvokeTryPrepareServiceBundle(string serviceName, string scriptPath, string manifestPath, string bundleRoot)
    {
        var method = ProgramType.GetMethod("TryPrepareServiceBundle", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var values = new object?[] { serviceName, scriptPath, manifestPath, null, null, bundleRoot };
        var success = (bool)method!.Invoke(null, values)!;
        var bundle = values[3];
        var error = values[4]?.ToString() ?? string.Empty;

        return (
            success,
            bundle,
            GetBundleField(bundle, "RootPath"),
            GetBundleField(bundle, "RuntimeExecutablePath"),
            GetBundleField(bundle, "ScriptPath"),
            GetBundleField(bundle, "ModuleManifestPath"),
            error);
    }

    private static IReadOnlyList<string> InvokeBuildElevatedRelaunchArguments(string executablePath, IReadOnlyList<string> args)
    {
        var method = ProgramType.GetMethod("BuildElevatedRelaunchArguments", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        return (IReadOnlyList<string>)method!.Invoke(null, [executablePath, args])!;
    }

    private static string GetRuntimeRidForCurrentProcess()
    {
        var osPrefix = OperatingSystem.IsWindows()
            ? "win"
            : OperatingSystem.IsLinux()
                ? "linux"
                : OperatingSystem.IsMacOS()
                    ? "osx"
                    : throw new PlatformNotSupportedException("Unsupported OS for runtime RID tests.");

        var archSuffix = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException($"Unsupported process architecture for runtime RID tests: {RuntimeInformation.ProcessArchitecture}"),
        };

        return $"{osPrefix}-{archSuffix}";
    }

    private static string GetBundleField(object? bundle, string propertyName)
    {
        if (bundle is null)
        {
            return string.Empty;
        }

        var property = bundle.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property!.GetValue(bundle)?.ToString() ?? string.Empty;
    }

    private static byte[] CreatePackageWithNuspec(string nuspec)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("Kestrun.nuspec");
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, Encoding.UTF8, leaveOpen: false);
            writer.Write(nuspec);
        }

        return stream.ToArray();
    }

    private static string GetParsedCommandMode(object parsedCommand)
    {
        var mode = GetParsedCommandField(parsedCommand, "Mode");
        Assert.NotNull(mode);
        return mode!;
    }

    private static string? GetParsedCommandField(object parsedCommand, string propertyName)
    {
        var property = parsedCommand.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property!.GetValue(parsedCommand)?.ToString();
    }

    private static string? GetRecordField(object record, string propertyName)
    {
        var property = record.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property!.GetValue(record)?.ToString();
    }

    private static bool GetResultBoolean(object result, string propertyName)
    {
        var property = result.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return (bool)property!.GetValue(result)!;
    }

    private static string[] GetResultCommandArgs(object result)
    {
        var property = result.GetType().GetProperty("CommandArgs", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return (string[])property!.GetValue(result)!;
    }

    private static Type ResolveProgramType()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, "Kestrun.Tool", StringComparison.Ordinal))
            ?? Assembly.Load("Kestrun.Tool");

        return assembly.GetType("Kestrun.Tool.Program", throwOnError: true, ignoreCase: false)!;
    }
}
#endif

