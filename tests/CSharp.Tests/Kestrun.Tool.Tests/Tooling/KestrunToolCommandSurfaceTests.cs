#if NET10_0_OR_GREATER
using System.IO.Compression;
using System.Formats.Tar;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Runtime.InteropServices;
using Xunit;

namespace Kestrun.Tool.Tests.Tooling;

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
        Assert.True((bool)Invoke("IsNoCheckOption", "--nocheck"));
        Assert.True((bool)Invoke("IsNoCheckOption", "--no-check"));
        Assert.False((bool)Invoke("IsNoCheckOption", "--check"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void FilterGlobalOptions_RemovesNoCheck_ForMetaCommands()
    {
        var input = new[] { "--nocheck", "version" };
        var filtered = (List<string>)InvokeWithStringArray("FilterGlobalOptions", input);

        Assert.Equal(["version"], filtered);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleInstall_WithVersion_Succeeds()
    {
        var (Success, ParsedCommand, _) = InvokeTryParseArguments(["module", "install", "--version", "1.2.3"]);

        Assert.True(Success);
        Assert.Equal("ModuleInstall", GetParsedCommandMode(ParsedCommand!));
        Assert.Equal("1.2.3", GetParsedCommandField(ParsedCommand!, "ModuleVersion"));
        Assert.Equal("Local", GetParsedCommandField(ParsedCommand!, "ModuleScope"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleInstall_WithGlobalScope_Succeeds()
    {
        var (Success, ParsedCommand, _) = InvokeTryParseArguments(["module", "install", "--scope", "global"]);

        Assert.True(Success);
        Assert.Equal("ModuleInstall", GetParsedCommandMode(ParsedCommand!));
        Assert.Equal("Global", GetParsedCommandField(ParsedCommand!, "ModuleScope"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleUpdate_WithForce_Succeeds()
    {
        var (Success, ParsedCommand, _) = InvokeTryParseArguments(["module", "update", "--force"]);

        Assert.True(Success);
        Assert.Equal("ModuleUpdate", GetParsedCommandMode(ParsedCommand!));
        Assert.Equal("True", GetParsedCommandField(ParsedCommand!, "ModuleForce"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleInstall_WithForce_Fails()
    {
        var (Success, _, Error) = InvokeTryParseArguments(["module", "install", "--force"]);

        Assert.False(Success);
        Assert.Contains("does not accept --force", Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleInstall_WithInvalidScope_Fails()
    {
        var (Success, _, Error) = InvokeTryParseArguments(["module", "install", "--scope", "team"]);

        Assert.False(Success);
        Assert.Contains("Unknown module scope", Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleRemove_WithoutVersion_Succeeds()
    {
        var (Success, ParsedCommand, _) = InvokeTryParseArguments(["module", "remove"]);

        Assert.True(Success);
        Assert.Equal("ModuleRemove", GetParsedCommandMode(ParsedCommand!));
        Assert.Null(GetParsedCommandField(ParsedCommand!, "ModuleVersion"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleRemove_WithVersion_Fails()
    {
        var (Success, _, Error) = InvokeTryParseArguments(["module", "remove", "--version", "1.2.3"]);

        Assert.False(Success);
        Assert.Contains("does not accept --version", Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleInfo_Succeeds()
    {
        var (Success, ParsedCommand, _) = InvokeTryParseArguments(["module", "info"]);

        Assert.True(Success);
        Assert.Equal("ModuleInfo", GetParsedCommandMode(ParsedCommand!));
        Assert.Null(GetParsedCommandField(ParsedCommand!, "ModuleVersion"));
        Assert.Equal("Local", GetParsedCommandField(ParsedCommand!, "ModuleScope"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleCommand_AllowsLauncherInjectedKestrunOptions()
    {
        var (Success, ParsedCommand, _) = InvokeTryParseArguments([
            "--kestrun-folder",
            "C:\\temp\\module",
            "--kestrun-manifest",
            "C:\\temp\\module\\Kestrun.psd1",
            "module",
            "info",
        ]);

        Assert.True(Success);
        Assert.Equal("ModuleInfo", GetParsedCommandMode(ParsedCommand!));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ModuleInstall_MissingVersionValue_Fails()
    {
        var (Success, _, Error) = InvokeTryParseArguments(["module", "install", "--version"]);

        Assert.False(Success);
        Assert.Contains("Missing value for --version", Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_Run_WithScriptOptionMissingValue_Fails()
    {
        var (Success, _, Error) = InvokeTryParseArguments(["run", "--script"]);

        Assert.False(Success);
        Assert.Equal("Missing value for --script.", Error);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseServiceRegisterArguments_KnownOptionWithoutValue_FailsWithMissingValueMessage()
    {
        var (Success, _, Error) = InvokeTryParseServiceRegisterArguments([
            "--service-register",
            "--name",
        ]);

        Assert.False(Success);
        Assert.Equal("Missing value for --name.", Error);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseServiceRegisterArguments_UnknownOption_FailsWithUnknownOptionMessage()
    {
        var (Success, _, Error) = InvokeTryParseServiceRegisterArguments([
            "--service-register",
            "--unknown-option",
            "value",
        ]);

        Assert.False(Success);
        Assert.Equal("Unknown service register option: --unknown-option", Error);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void WriteModuleNotFoundMessage_AlwaysIncludes_ModuleInstallGuidance()
    {
        var lines = new List<string>();
        InvokeVoid("WriteModuleNotFoundMessage", null, null, new Action<string>(lines.Add));

        Assert.Contains(lines, line => line.Contains("module install", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("No Kestrun module was found", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void GetDefaultPowerShellModulePath_UsesExpectedFolderConvention()
    {
        var path = (string)Invoke("GetDefaultPowerShellModulePath");

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

            var (Success, Error) = InvokeTryValidateInstallAction(Path.Combine(tempRoot, "Kestrun"), "local");
            Assert.False(Success);
            Assert.Contains("module update", Error, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
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

            var (Success, Error) = InvokeTryValidateUpdateAction(Path.Combine(tempRoot, "Kestrun"), "1.0.0", force: false);
            Assert.False(Success);
            Assert.Contains("--force", Error, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
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

            var (Success, Error) = InvokeTryValidateUpdateAction(Path.Combine(tempRoot, "Kestrun"), "1.0.0", force: true);
            Assert.True(Success);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
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
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void CompareModuleVersionValues_OrdersPrereleaseSuffixesWithinSameBaseVersion()
    {
        var comparison = (int)Invoke("CompareModuleVersionValues", "1.0.0-beta4", "1.0.0-beta3");
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

            var records = (System.Collections.IEnumerable)Invoke("GetInstalledModuleRecords", Path.Combine(tempRoot, "Kestrun"));
            var firstRecord = records.Cast<object>().First();

            Assert.Equal("1.0.0-beta3", GetRecordField(firstRecord, "Version"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
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

            var records = (System.Collections.IEnumerable)Invoke("GetInstalledModuleRecords", Path.Combine(tempRoot, "Kestrun"));
            var firstRecord = records.Cast<object>().First();

            Assert.Equal("1.0.0", GetRecordField(firstRecord, "Version"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
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

            var (Success, Error) = InvokeTryRemoveInstalledModule(moduleRoot, showProgress: false);

            Assert.True(Success);
            Assert.False(Directory.Exists(moduleRoot));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
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
            var (Success, Error) = InvokeTryRemoveInstalledModule(moduleRoot, showProgress: false);

            Assert.True(Success);
            Assert.True(string.IsNullOrWhiteSpace(Error));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
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

            var (Success, RuntimePath, Error) = InvokeTryResolveServiceRuntimeExecutableFromModule(manifestPath);

            Assert.True(Success);
            Assert.True(string.IsNullOrWhiteSpace(Error));
            Assert.Equal(Path.GetFullPath(runtimePath), RuntimePath);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
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

            var (Success, RuntimePath, Error) = InvokeTryResolveServiceRuntimeExecutableFromModule(manifestPath);

            Assert.True(Success);
            Assert.True(string.IsNullOrWhiteSpace(Error));

            var resolvedRuntimePath = Path.GetFullPath(RuntimePath);
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
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryPrepareServiceBundle_CopiesRuntimeModuleAndScript_UsingOverrideRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        var originalDirectory = Environment.CurrentDirectory;
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            Environment.CurrentDirectory = tempRoot;

            var moduleRoot = Path.Combine(tempRoot, "module-src");
            _ = Directory.CreateDirectory(moduleRoot);
            var manifestPath = Path.Combine(moduleRoot, "Kestrun.psd1");
            File.WriteAllText(manifestPath, "@{`n    ModuleVersion = '1.0.0'`n}", Encoding.UTF8);

            var runtimeRid = GetRuntimeRidForCurrentProcess();
            var runtimeBinaryName = OperatingSystem.IsWindows() ? "kestrun.exe" : "kestrun";
            var runtimeSourcePath = Path.Combine(moduleRoot, "runtimes", runtimeRid, runtimeBinaryName);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(runtimeSourcePath)!);
            File.WriteAllText(runtimeSourcePath, "runtime-binary", Encoding.UTF8);
            StageDedicatedServicePayloadLayout(tempRoot, runtimeRid);

            var scriptPath = Path.Combine(tempRoot, "server.ps1");
            File.WriteAllText(scriptPath, "Write-Output 'hello'", Encoding.UTF8);

            var bundleRoot = Path.Combine(tempRoot, "bundle-root");
            var (Success, Bundle, BundleRootPath, RuntimeExecutablePath, ScriptPath, ModuleManifestPath, Error) = InvokeTryPrepareServiceBundle("svc:test", scriptPath, manifestPath, bundleRoot);

            Assert.True(Success);
            Assert.True(string.IsNullOrWhiteSpace(Error));
            Assert.NotNull(Bundle);

            Assert.True(Directory.Exists(BundleRootPath));
            Assert.True(File.Exists(RuntimeExecutablePath));
            Assert.True(File.Exists(ScriptPath));
            Assert.True(File.Exists(ModuleManifestPath));

            Assert.StartsWith(Path.GetFullPath(bundleRoot), BundleRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("runtime-binary", File.ReadAllText(RuntimeExecutablePath, Encoding.UTF8));
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ServiceInstall_WithContentRootAndDeploymentRoot_Succeeds()
    {
        var (Success, ParsedCommand, _) = InvokeTryParseArguments([
            "service",
            "install",
            "--name",
            "demo",
            "--content-root",
            ".\\app",
            "--deployment-root",
            "D:\\KestrunServices",
            "--script",
            ".\\scripts\\start.ps1",
        ]);

        Assert.True(Success);
        Assert.Equal("ServiceInstall", GetParsedCommandMode(ParsedCommand!));
        Assert.Equal(@".\app", GetParsedCommandField(ParsedCommand!, "ServiceContentRoot"));
        Assert.Equal(@"D:\KestrunServices", GetParsedCommandField(ParsedCommand!, "ServiceDeploymentRoot"));
        Assert.Equal(@".\scripts\start.ps1", GetParsedCommandField(ParsedCommand!, "ScriptPath"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ServiceInstall_WithContentRootAndNoScript_UsesDefaultServerScript()
    {
        var (Success, ParsedCommand, _) = InvokeTryParseArguments([
            "service",
            "install",
            "--name",
            "demo",
            "--content-root",
            ".\\app",
        ]);

        Assert.True(Success);
        Assert.Equal("ServiceInstall", GetParsedCommandMode(ParsedCommand!));
        Assert.Equal("server.ps1", GetParsedCommandField(ParsedCommand!, "ScriptPath"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ServiceInstall_WithArchiveChecksumOptions_Succeeds()
    {
        var (Success, ParsedCommand, _) = InvokeTryParseArguments([
            "service",
            "install",
            "--name",
            "demo",
            "--content-root",
            ".\\app.zip",
            "--content-root-checksum",
            "abcdef0123456789",
            "--content-root-checksum-algorithm",
            "sha256",
        ]);

        Assert.True(Success);
        Assert.Equal("ServiceInstall", GetParsedCommandMode(ParsedCommand!));
        Assert.Equal("abcdef0123456789", GetParsedCommandField(ParsedCommand!, "ServiceContentRootChecksum"));
        Assert.Equal("sha256", GetParsedCommandField(ParsedCommand!, "ServiceContentRootChecksumAlgorithm"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ServiceInstall_WithChecksumAlgorithmOnly_Fails()
    {
        var (Success, _, Error) = InvokeTryParseArguments([
            "service",
            "install",
            "--name",
            "demo",
            "--content-root",
            ".\\app.zip",
            "--content-root-checksum-algorithm",
            "sha256",
        ]);

        Assert.False(Success);
        Assert.Contains("requires --content-root-checksum", Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ServiceInstall_WithUrlAuthOptions_Succeeds()
    {
        var (Success, ParsedCommand, _) = InvokeTryParseArguments([
            "service",
            "install",
            "--name",
            "demo",
            "--content-root",
            "https://example.test/app.zip",
            "--content-root-bearer-token",
            "my-token",
            "--content-root-ignore-certificate",
        ]);

        Assert.True(Success);
        Assert.Equal("my-token", GetParsedCommandField(ParsedCommand!, "ServiceContentRootBearerToken"));
        Assert.Equal("True", GetParsedCommandField(ParsedCommand!, "ServiceContentRootIgnoreCertificate"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ServiceInstall_WithCustomUrlHeaders_Succeeds()
    {
        var (Success, ParsedCommand, _) = InvokeTryParseArguments([
            "service",
            "install",
            "--name",
            "demo",
            "--content-root",
            "https://example.test/app.zip",
            "--content-root-header",
            "x-api-key:my-key",
            "--content-root-header",
            "x-env:prod",
        ]);

        Assert.True(Success);
        Assert.Equal(["x-api-key:my-key", "x-env:prod"], GetParsedCommandStringArrayField(ParsedCommand!, "ServiceContentRootHeaders"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ServiceInstall_WithServiceUserAndPassword_Succeeds()
    {
        var (Success, ParsedCommand, _) = InvokeTryParseArguments([
            "service",
            "install",
            "--name",
            "demo",
            "--service-user",
            "svc-kestrun",
            "--service-password",
            "P@ssw0rd!",
            "--script",
            ".\\server.ps1",
        ]);

        Assert.True(Success);
        Assert.Equal("ServiceInstall", GetParsedCommandMode(ParsedCommand!));
        Assert.Equal("svc-kestrun", GetParsedCommandField(ParsedCommand!, "ServiceUser"));
        Assert.Equal("P@ssw0rd!", GetParsedCommandField(ParsedCommand!, "ServicePassword"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ServiceStart_WithDeploymentRoot_Fails()
    {
        var (Success, _, Error) = InvokeTryParseArguments([
            "service",
            "start",
            "--name",
            "demo",
            "--deployment-root",
            @"D:\KestrunServices",
        ]);

        Assert.False(Success);
        Assert.Contains("does not accept --deployment-root", Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryClassifyServiceContentRoot_WithWhitespace_ReturnsFalseAndEmptyOutputs()
    {
        var (Success, NormalizedContentRoot, ContentRootUri, FullContentRoot) = InvokeTryClassifyServiceContentRoot("   ");

        Assert.False(Success);
        Assert.Equal(string.Empty, NormalizedContentRoot);
        Assert.Null(ContentRootUri);
        Assert.Equal(string.Empty, FullContentRoot);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryClassifyServiceContentRoot_WithHttpUrl_ReturnsUriClassification()
    {
        var (Success, NormalizedContentRoot, ContentRootUri, FullContentRoot) =
            InvokeTryClassifyServiceContentRoot("https://example.test/content/app.zip");

        Assert.True(Success);
        Assert.Equal("https://example.test/content/app.zip", NormalizedContentRoot);
        Assert.NotNull(ContentRootUri);
        Assert.Equal("https", ContentRootUri.Scheme);
        Assert.Equal(string.Empty, FullContentRoot);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveServiceScriptSource_WithoutContentRoot_UsesRequestedScriptPath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        var originalDirectory = Environment.CurrentDirectory;
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            Environment.CurrentDirectory = tempRoot;

            var scriptPath = Path.Combine(tempRoot, "server.ps1");
            File.WriteAllText(scriptPath, "Write-Output 'hello'", Encoding.UTF8);

            var (Success, parsedCommand, parseError) = InvokeTryParseArguments([
                "service",
                "install",
                "--name",
                "demo",
                "--script",
                "server.ps1",
            ]);

            Assert.True(Success);
            Assert.True(string.IsNullOrWhiteSpace(parseError));

            var result = InvokeTryResolveServiceScriptSource(parsedCommand!);
            Assert.True(result.Success, result.Error);
            Assert.Equal(Path.GetFullPath("server.ps1"), Path.GetFullPath(result.FullScriptPath));
            Assert.Equal("server.ps1", result.RelativeScriptPath);
            Assert.True(string.IsNullOrWhiteSpace(result.FullContentRoot));
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_ServiceInstall_WithoutContentRootAndBearerToken_Fails()
    {
        var (Success, _, Error) = InvokeTryParseArguments([
            "service",
            "install",
            "--name",
            "demo",
            "--script",
            "server.ps1",
            "--content-root-bearer-token",
            "token-value",
        ]);

        Assert.False(Success);
        Assert.Contains("--content-root-bearer-token requires --content-root", Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveServiceScriptSource_WithDirectoryContentRoot_ResolvesRelativeScript()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            var contentRoot = Path.Combine(tempRoot, "content");
            var scriptRelativePath = Path.Combine("scripts", "start.ps1");
            var scriptFullPath = Path.Combine(contentRoot, scriptRelativePath);

            _ = Directory.CreateDirectory(Path.GetDirectoryName(scriptFullPath)!);
            File.WriteAllText(scriptFullPath, "Write-Output 'hello-directory-root'", Encoding.UTF8);

            var (Success, parsedCommand, parseError) = InvokeTryParseArguments([
                "service",
                "install",
                "--name",
                "demo",
                "--content-root",
                contentRoot,
                "--script",
                scriptRelativePath,
            ]);

            Assert.True(Success);
            Assert.True(string.IsNullOrWhiteSpace(parseError));

            var result = InvokeTryResolveServiceScriptSource(parsedCommand!);
            Assert.True(result.Success, result.Error);
            Assert.Equal(Path.GetFullPath(scriptFullPath), Path.GetFullPath(result.FullScriptPath));
            Assert.Equal(Path.GetFullPath(contentRoot), Path.GetFullPath(result.FullContentRoot));
            Assert.Equal(scriptRelativePath, result.RelativeScriptPath);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveServiceScriptSource_WithMissingLocalContentRoot_FailsPathNotFound()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            var missingContentRoot = Path.Combine(tempRoot, "missing", "app.zip");

            var (Success, parsedCommand, parseError) = InvokeTryParseArguments([
                "service",
                "install",
                "--name",
                "demo",
                "--content-root",
                missingContentRoot,
                "--script",
                "scripts/start.ps1",
            ]);

            Assert.True(Success);
            Assert.True(string.IsNullOrWhiteSpace(parseError));

            var result = InvokeTryResolveServiceScriptSource(parsedCommand!);
            Assert.False(result.Success);
            Assert.Contains("Service content root path was not found", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveServiceScriptSource_WithContentRootAndAbsoluteScript_Fails()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            var contentRoot = Path.Combine(tempRoot, "content");
            _ = Directory.CreateDirectory(contentRoot);

            var (Success, ParsedCommand, Error) = InvokeTryParseArguments([
                "service",
                "install",
                "--name",
                "demo",
                "--content-root",
                contentRoot,
                "--script",
                Path.Combine(contentRoot, "server.ps1"),
            ]);

            Assert.True(Success);

            var result = InvokeTryResolveServiceScriptSource(ParsedCommand!);
            Assert.False(result.Success);
            Assert.Contains("must be a relative path", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryPrepareServiceBundle_WithContentRoot_CopiesEntireFolderAndPreservesRelativeScriptPath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        var originalDirectory = Environment.CurrentDirectory;
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            Environment.CurrentDirectory = tempRoot;

            var moduleRoot = Path.Combine(tempRoot, "module-src");
            _ = Directory.CreateDirectory(moduleRoot);
            var manifestPath = Path.Combine(moduleRoot, "Kestrun.psd1");
            File.WriteAllText(manifestPath, "@{`n    ModuleVersion = '1.0.0'`n}", Encoding.UTF8);

            var runtimeRid = GetRuntimeRidForCurrentProcess();
            var runtimeBinaryName = OperatingSystem.IsWindows() ? "kestrun.exe" : "kestrun";
            var runtimeSourcePath = Path.Combine(moduleRoot, "runtimes", runtimeRid, runtimeBinaryName);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(runtimeSourcePath)!);
            File.WriteAllText(runtimeSourcePath, "runtime-binary", Encoding.UTF8);
            StageDedicatedServicePayloadLayout(tempRoot, runtimeRid);

            var contentRoot = Path.Combine(tempRoot, "service-content");
            var nestedScripts = Path.Combine(contentRoot, "scripts");
            var nestedConfig = Path.Combine(contentRoot, "config");
            _ = Directory.CreateDirectory(nestedScripts);
            _ = Directory.CreateDirectory(nestedConfig);

            var scriptPath = Path.Combine(nestedScripts, "start.ps1");
            File.WriteAllText(scriptPath, "Write-Output 'hello'", Encoding.UTF8);
            File.WriteAllText(Path.Combine(nestedConfig, "settings.json"), "{}", Encoding.UTF8);

            var bundleRoot = Path.Combine(tempRoot, "bundle-root");
            var (Success, Bundle, BundleRootPath, _, ScriptPath, _, Error) = InvokeTryPrepareServiceBundle(
                "svc:test",
                scriptPath,
                manifestPath,
                bundleRoot,
                contentRoot,
                Path.Combine("scripts", "start.ps1"));

            Assert.True(Success);
            Assert.True(string.IsNullOrWhiteSpace(Error));
            Assert.NotNull(Bundle);

            var bundledConfig = Path.Combine(BundleRootPath, "script", "config", "settings.json");
            var bundledScript = Path.Combine(BundleRootPath, "script", "scripts", "start.ps1");

            Assert.True(File.Exists(bundledConfig));
            Assert.True(File.Exists(bundledScript));
            Assert.Equal(Path.GetFullPath(bundledScript), Path.GetFullPath(ScriptPath));
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveServiceScriptSource_WithZipContentRoot_ExtractsAndFindsScript()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        var extractedRoot = string.Empty;
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            var zipPath = Path.Combine(tempRoot, "app.zip");
            CreateZipArchive(zipPath, new Dictionary<string, string>
            {
                ["scripts/start.ps1"] = "Write-Output 'hello'",
                ["config/settings.json"] = "{}",
            });

            var (_, parsedCommand, parseError) = InvokeTryParseArguments([
                "service",
                "install",
                "--name",
                "demo",
                "--content-root",
                zipPath,
                "--script",
                "scripts/start.ps1",
            ]);
            Assert.True(string.IsNullOrWhiteSpace(parseError), parseError);

            var (Success, FullScriptPath, FullContentRoot, RelativeScriptPath, Error) = InvokeTryResolveServiceScriptSource(parsedCommand!);
            Assert.True(Success);
            Assert.True(File.Exists(FullScriptPath));
            Assert.True(Directory.Exists(FullContentRoot));
            Assert.Equal(Path.Combine("scripts", "start.ps1"), RelativeScriptPath);

            extractedRoot = FullContentRoot;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(extractedRoot) && Directory.Exists(extractedRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(extractedRoot, maxAttempts: 20, initialDelayMs: 50);
            }

            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveServiceScriptSource_WithTgzContentRoot_ExtractsAndFindsScript()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        var extractedRoot = string.Empty;
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            var tgzPath = Path.Combine(tempRoot, "app.tgz");
            CreateTarGzArchive(tgzPath, new Dictionary<string, string>
            {
                ["scripts/start.ps1"] = "Write-Output 'hello from tgz'",
                ["assets/readme.txt"] = "hello",
            });

            var (_, parsedCommand, parseError) = InvokeTryParseArguments([
                "service",
                "install",
                "--name",
                "demo",
                "--content-root",
                tgzPath,
                "--script",
                "scripts/start.ps1",
            ]);
            Assert.True(string.IsNullOrWhiteSpace(parseError));

            var (Success, FullScriptPath, FullContentRoot, RelativeScriptPath, Error) = InvokeTryResolveServiceScriptSource(parsedCommand!);
            Assert.True(Success, Error);
            Assert.True(File.Exists(FullScriptPath));
            Assert.Equal(Path.Combine("scripts", "start.ps1"), RelativeScriptPath);

            extractedRoot = FullContentRoot;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(extractedRoot) && Directory.Exists(extractedRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(extractedRoot, maxAttempts: 20, initialDelayMs: 50);
            }

            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveServiceScriptSource_WithArchiveChecksumMismatch_Fails()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            var zipPath = Path.Combine(tempRoot, "app.zip");
            CreateZipArchive(zipPath, new Dictionary<string, string>
            {
                ["server.ps1"] = "Write-Output 'hello'",
            });

            var (_, parsedCommand, parseError) = InvokeTryParseArguments([
                "service",
                "install",
                "--name",
                "demo",
                "--content-root",
                zipPath,
                "--content-root-checksum",
                "deadbeef",
            ]);
            Assert.True(string.IsNullOrWhiteSpace(parseError));
            var (Success, _, _, _, Error) = InvokeTryResolveServiceScriptSource(parsedCommand!);
            Assert.False(Success);
            Assert.Contains("checksum mismatch", Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveInstallServiceInputs_WithArchiveContentRootAndMissingManifest_CleansUpTemporaryContentRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        var temporaryContentRootPath = string.Empty;
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            var zipPath = Path.Combine(tempRoot, "app.zip");
            CreateZipArchive(zipPath, new Dictionary<string, string>
            {
                ["scripts/start.ps1"] = "Write-Output 'hello'",
            });

            var missingManifestPath = Path.Combine(tempRoot, "missing", "Kestrun.psd1");
            var (_, parsedCommand, parseError) = InvokeTryParseArguments([
                "service",
                "install",
                "--name",
                "demo",
                "--content-root",
                zipPath,
                "--script",
                "scripts/start.ps1",
                "--kestrun-manifest",
                missingManifestPath,
            ]);
            Assert.True(string.IsNullOrWhiteSpace(parseError));

            var (Success, ServiceName, FullScriptPath, RelativeScriptPath, TemporaryContentRootPath, ModuleManifestPath, ExitCode) = InvokeTryResolveInstallServiceInputs(parsedCommand!);
            Assert.False(Success);
            Assert.Equal(3, ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(ModuleManifestPath));

            temporaryContentRootPath = TemporaryContentRootPath;
            Assert.False(string.IsNullOrWhiteSpace(temporaryContentRootPath));
            Assert.False(Directory.Exists(temporaryContentRootPath));
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(temporaryContentRootPath) && Directory.Exists(temporaryContentRootPath))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(temporaryContentRootPath, maxAttempts: 20, initialDelayMs: 50);
            }

            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public async Task TryResolveServiceScriptSource_WithHttpArchiveUrl_DownloadsAndFindsScript()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        var extractedRoot = string.Empty;

        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            var zipBytes = CreateZipArchiveBytes(new Dictionary<string, string>
            {
                ["scripts/start.ps1"] = "Write-Output 'hello-url'",
            });

            await using var server = StartSingleRequestHttpServer(async context =>
            {
                var auth = context.Request.Headers["Authorization"];
                var apiKey = context.Request.Headers["x-api-key"];
                if (!string.Equals(auth, "Bearer my-token", StringComparison.Ordinal))
                {
                    context.Response.StatusCode = 401;
                    context.Response.Close();
                    return;
                }

                if (!string.Equals(apiKey, "my-key", StringComparison.Ordinal))
                {
                    context.Response.StatusCode = 401;
                    context.Response.Close();
                    return;
                }

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/zip";
                context.Response.ContentLength64 = zipBytes.Length;
                await context.Response.OutputStream.WriteAsync(zipBytes).ConfigureAwait(false);
                context.Response.OutputStream.Close();
                context.Response.Close();
            });

            var archiveUrl = $"{server.Prefix}app.zip";
            var (_, parsedCommand, parseError) = InvokeTryParseArguments([
                "service",
                "install",
                "--name",
                "demo",
                "--content-root",
                archiveUrl,
                "--content-root-bearer-token",
                "my-token",
                "--content-root-header",
                "x-api-key:my-key",
                "--script",
                "scripts/start.ps1",
            ]);
            Assert.True(string.IsNullOrWhiteSpace(parseError));

            var (Success, FullScriptPath, FullContentRoot, RelativeScriptPath, Error) = InvokeTryResolveServiceScriptSource(parsedCommand!);
            Assert.True(Success);
            Assert.True(File.Exists(FullScriptPath));
            Assert.Equal(Path.Combine("scripts", "start.ps1"), RelativeScriptPath);
            extractedRoot = FullContentRoot;

            var temporaryExtractionRoot = Directory.GetParent(FullContentRoot);
            Assert.NotNull(temporaryExtractionRoot);

            var topLevelArtifacts = Directory.GetFiles(temporaryExtractionRoot.FullName, "*", SearchOption.TopDirectoryOnly);
            Assert.DoesNotContain(
                topLevelArtifacts,
                path => path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase));

            await server.Completion;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(extractedRoot) && Directory.Exists(extractedRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(extractedRoot, maxAttempts: 20, initialDelayMs: 50);
            }

            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public async Task TryResolveServiceScriptSource_WithHttpArchiveUrlAndInvalidDispositionFileName_DownloadsAndFindsScript()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        var extractedRoot = string.Empty;

        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            var zipBytes = CreateZipArchiveBytes(new Dictionary<string, string>
            {
                ["scripts/start.ps1"] = "Write-Output 'hello-url-invalid-filename'",
            });

            await using var server = StartSingleRequestHttpServer(async context =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/zip";
                context.Response.Headers["Content-Disposition"] = "attachment; filename=\"app:artifact.zip\"";
                context.Response.ContentLength64 = zipBytes.Length;
                await context.Response.OutputStream.WriteAsync(zipBytes).ConfigureAwait(false);
                context.Response.OutputStream.Close();
                context.Response.Close();
            });

            var archiveUrl = $"{server.Prefix}download";
            var (_, parsedCommand, parseError) = InvokeTryParseArguments([
                "service",
                "install",
                "--name",
                "demo",
                "--content-root",
                archiveUrl,
                "--script",
                "scripts/start.ps1",
            ]);
            Assert.True(string.IsNullOrWhiteSpace(parseError), parseError);

            var (Success, FullScriptPath, FullContentRoot, RelativeScriptPath, Error) = InvokeTryResolveServiceScriptSource(parsedCommand!);
            Assert.True(Success, Error);
            Assert.True(File.Exists(FullScriptPath));
            Assert.Equal(Path.Combine("scripts", "start.ps1"), RelativeScriptPath);
            extractedRoot = FullContentRoot;

            await server.Completion;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(extractedRoot) && Directory.Exists(extractedRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(extractedRoot, maxAttempts: 20, initialDelayMs: 50);
            }

            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public async Task TryResolveInstallServiceInputs_WithHttpArchiveContentRootAndMissingManifest_CleansUpTemporaryContentRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        var temporaryContentRootPath = string.Empty;

        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            var zipBytes = CreateZipArchiveBytes(new Dictionary<string, string>
            {
                ["scripts/start.ps1"] = "Write-Output 'hello-url'",
            });

            await using var server = StartSingleRequestHttpServer(async context =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/zip";
                context.Response.ContentLength64 = zipBytes.Length;
                await context.Response.OutputStream.WriteAsync(zipBytes).ConfigureAwait(false);
                context.Response.OutputStream.Close();
                context.Response.Close();
            });

            var archiveUrl = $"{server.Prefix}app.zip";
            var missingManifestPath = Path.Combine(tempRoot, "missing", "Kestrun.psd1");
            var (_, parsedCommand, parseError) = InvokeTryParseArguments([
                "service",
                "install",
                "--name",
                "demo",
                "--content-root",
                archiveUrl,
                "--script",
                "scripts/start.ps1",
                "--kestrun-manifest",
                missingManifestPath,
            ]);
            Assert.True(string.IsNullOrWhiteSpace(parseError));

            var (Success, ServiceName, FullScriptPath, RelativeScriptPath, TemporaryContentRootPath, ModuleManifestPath, ExitCode) = InvokeTryResolveInstallServiceInputs(parsedCommand!);
            Assert.False(Success);
            Assert.Equal(3, ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(ModuleManifestPath));

            temporaryContentRootPath = TemporaryContentRootPath;
            Assert.False(string.IsNullOrWhiteSpace(temporaryContentRootPath));
            Assert.False(Directory.Exists(temporaryContentRootPath));

            await server.Completion;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(temporaryContentRootPath) && Directory.Exists(temporaryContentRootPath))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(temporaryContentRootPath, maxAttempts: 20, initialDelayMs: 50);
            }

            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public async Task TryResolveServiceScriptSource_WithExtensionlessHttpArchiveUrlAndOctetStream_DetectsTgzAndFindsScript()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        var extractedRoot = string.Empty;

        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            var tgzBytes = CreateTarGzArchiveBytes(new Dictionary<string, string>
            {
                ["scripts/start.ps1"] = "Write-Output 'hello-url-tgz'",
            });

            await using var server = StartSingleRequestHttpServer(async context =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/octet-stream";
                context.Response.ContentLength64 = tgzBytes.Length;
                await context.Response.OutputStream.WriteAsync(tgzBytes).ConfigureAwait(false);
                context.Response.OutputStream.Close();
                context.Response.Close();
            });

            var archiveUrl = $"{server.Prefix}download?sig=abc123";
            var (_, parsedCommand, parseError) = InvokeTryParseArguments([
                "service",
                "install",
                "--name",
                "demo",
                "--content-root",
                archiveUrl,
                "--script",
                "scripts/start.ps1",
            ]);
            Assert.True(string.IsNullOrWhiteSpace(parseError), parseError);

            var (Success, FullScriptPath, FullContentRoot, RelativeScriptPath, Error) = InvokeTryResolveServiceScriptSource(parsedCommand!);
            if (!Success)
            {
                throw new Xunit.Sdk.XunitException(Error);
            }
            Assert.True(File.Exists(FullScriptPath));
            Assert.Equal(Path.Combine("scripts", "start.ps1"), RelativeScriptPath);

            extractedRoot = FullContentRoot;

            await server.Completion;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(extractedRoot) && Directory.Exists(extractedRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(extractedRoot, maxAttempts: 20, initialDelayMs: 50);
            }

            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveServiceScriptSource_WithIgnoreCertificateAndLocalArchive_Fails()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            var zipPath = Path.Combine(tempRoot, "app.zip");
            CreateZipArchive(zipPath, new Dictionary<string, string>
            {
                ["server.ps1"] = "Write-Output 'hello'",
            });

            var (_, parsedCommand, parseError) = InvokeTryParseArguments([
                "service",
                "install",
                "--name",
                "demo",
                "--content-root",
                zipPath,
                "--content-root-ignore-certificate",
            ]);
            Assert.True(string.IsNullOrWhiteSpace(parseError));
            var (Success, _, _, _, Error) = InvokeTryResolveServiceScriptSource(parsedCommand!);
            Assert.False(Success);
            Assert.Contains("only supported when --content-root points to an HTTPS archive URL", Error, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveServiceScriptSource_WithCustomHeaderAndLocalArchive_Fails()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tests-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            var zipPath = Path.Combine(tempRoot, "app.zip");
            CreateZipArchive(zipPath, new Dictionary<string, string>
            {
                ["server.ps1"] = "Write-Output 'hello'",
            });

            var (_, parsedCommand, parseError) = InvokeTryParseArguments([
                "service",
                "install",
                "--name",
                "demo",
                "--content-root",
                zipPath,
                "--content-root-header",
                "x-api-key:my-key",
            ]);
            Assert.True(string.IsNullOrWhiteSpace(parseError));
            var (Success, _, _, _, Error) = InvokeTryResolveServiceScriptSource(parsedCommand!);
            Assert.False(Success);
            Assert.Contains("only supported when --content-root points to an HTTP(S) archive URL", Error, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveServiceScriptSource_WithInvalidCustomHeaderFormat_Fails()
    {
        var (_, parsedCommand, parseError) = InvokeTryParseArguments([
            "service",
            "install",
            "--name",
            "demo",
            "--content-root",
            "http://example.test/app.zip",
            "--content-root-header",
            "invalid-header-value",
            "--script",
            "scripts/start.ps1",
        ]);
        Assert.True(string.IsNullOrWhiteSpace(parseError));
        var (Success, _, _, _, Error) = InvokeTryResolveServiceScriptSource(parsedCommand!);
        Assert.False(Success);
        Assert.Contains("Use <name:value>", Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveServiceScriptSource_WithCustomHeaderNameContainingNewline_Fails()
    {
        var (_, parsedCommand, parseError) = InvokeTryParseArguments([
            "service",
            "install",
            "--name",
            "demo",
            "--content-root",
            "http://example.test/app.zip",
            "--content-root-header",
            $"x-api{Environment.NewLine}key:value",
            "--script",
            "scripts/start.ps1",
        ]);
        Assert.True(string.IsNullOrWhiteSpace(parseError));
        var (Success, _, _, _, Error) = InvokeTryResolveServiceScriptSource(parsedCommand!);
        Assert.False(Success);
        Assert.Contains("cannot contain CR or LF characters", Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveServiceScriptSource_WithCustomHeaderValueContainingNewline_Fails()
    {
        var (_, parsedCommand, parseError) = InvokeTryParseArguments([
            "service",
            "install",
            "--name",
            "demo",
            "--content-root",
            "http://example.test/app.zip",
            "--content-root-header",
            $"x-api-key:my{Environment.NewLine}key",
            "--script",
            "scripts/start.ps1",
        ]);
        Assert.True(string.IsNullOrWhiteSpace(parseError));
        var (Success, _, _, _, Error) = InvokeTryResolveServiceScriptSource(parsedCommand!);
        Assert.False(Success);
        Assert.Contains("cannot contain CR or LF characters", Error, StringComparison.Ordinal);
    }

    private static void StageDedicatedServicePayloadLayout(string repositoryRoot, string runtimeRid)
    {
        var payloadRoot = Path.Combine(repositoryRoot, "src", "CSharp", "Kestrun.Tool", "kestrun-service", runtimeRid);
        var hostBinaryName = OperatingSystem.IsWindows() ? "kestrun-service-host.exe" : "kestrun-service-host";
        var modulesDirectory = Path.Combine(payloadRoot, "Modules", "Kestrun");

        _ = Directory.CreateDirectory(payloadRoot);
        _ = Directory.CreateDirectory(modulesDirectory);

        File.WriteAllText(Path.Combine(payloadRoot, hostBinaryName), "service-host", Encoding.UTF8);
        File.WriteAllText(Path.Combine(modulesDirectory, "Kestrun.psd1"), "@{`n    ModuleVersion = '1.0.0'`n}", Encoding.UTF8);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void BuildWindowsServiceHostArguments_UsesBundledManifestPath()
    {
        var runnerPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "bundle", "runtime", "Kestrun.Runner.dll"));
        var bundledScriptPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "bundle", "script", "server.ps1"));
        var bundledManifestPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "bundle", "module", "Kestrun.psd1"));

        var args = (IReadOnlyList<string>)Invoke(
            "BuildDedicatedServiceHostArguments",
            "demo",
            runnerPath,
            bundledScriptPath,
            bundledManifestPath,
            Array.Empty<string>(),
            null);

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

    [Fact]
    [Trait("Category", "Tooling")]
    public void NormalizeWindowsServiceAccountName_MapsFriendlyBuiltinAliases()
    {
        Assert.Equal(@"NT AUTHORITY\NetworkService", InvokeNormalizeWindowsServiceAccountName("NetworkService"));
        Assert.Equal(@"NT AUTHORITY\NetworkService", InvokeNormalizeWindowsServiceAccountName("network service"));
        Assert.Equal(@"NT AUTHORITY\LocalService", InvokeNormalizeWindowsServiceAccountName("LocalService"));
        Assert.Equal("LocalSystem", InvokeNormalizeWindowsServiceAccountName("system"));
        Assert.Equal("DOMAIN\\svc-kestrun", InvokeNormalizeWindowsServiceAccountName("DOMAIN\\svc-kestrun"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void IsWindowsBuiltinServiceAccount_RecognizesBuiltinAccounts()
    {
        Assert.True(InvokeIsWindowsBuiltinServiceAccount("LocalSystem"));
        Assert.True(InvokeIsWindowsBuiltinServiceAccount(@"NT AUTHORITY\NetworkService"));
        Assert.True(InvokeIsWindowsBuiltinServiceAccount(@"NT AUTHORITY\LocalService"));
        Assert.False(InvokeIsWindowsBuiltinServiceAccount(@"DOMAIN\svc-kestrun"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void BuildLinuxSystemdUnitContent_WithServiceUser_UsesSystemScopeUserAndTarget()
    {
        var content = InvokeBuildLinuxSystemdUnitContent(
            "demo",
            "/usr/bin/kestrun-service-host",
            ["--run", "script.ps1"],
            "/opt/kestrun/demo",
            "svc-kestrun");

        Assert.Contains("User=svc-kestrun", content, StringComparison.Ordinal);
        Assert.Contains("WantedBy=multi-user.target", content, StringComparison.Ordinal);
        Assert.DoesNotContain("WantedBy=default.target", content, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void BuildLinuxSystemdUnitContent_WithoutServiceUser_UsesUserScopeTarget()
    {
        var content = InvokeBuildLinuxSystemdUnitContent(
            "demo",
            "/usr/bin/kestrun-service-host",
            ["--run", "script.ps1"],
            "/opt/kestrun/demo",
            null);

        Assert.DoesNotContain("User=", content, StringComparison.Ordinal);
        Assert.Contains("WantedBy=default.target", content, StringComparison.Ordinal);
        Assert.DoesNotContain("WantedBy=multi-user.target", content, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void BuildLaunchdPlist_WithServiceUser_IncludesUserName()
    {
        var plist = InvokeBuildLaunchdPlist(
            "com.kestrun.demo",
            "/opt/kestrun/demo",
            ["/usr/local/bin/kestrun-service-host", "--run", "server.ps1"],
            "svc-kestrun");

        Assert.Contains("<key>UserName</key>", plist, StringComparison.Ordinal);
        Assert.Contains("<string>svc-kestrun</string>", plist, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void BuildLaunchdPlist_WithoutServiceUser_OmitsUserName()
    {
        var plist = InvokeBuildLaunchdPlist(
            "com.kestrun.demo",
            "/opt/kestrun/demo",
            ["/usr/local/bin/kestrun-service-host", "--run", "server.ps1"],
            null);

        Assert.DoesNotContain("<key>UserName</key>", plist, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryDeleteDirectoryWithRetry_Succeeds_WhenDirectoryDoesNotExist()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"kestrun-missing-{Guid.NewGuid():N}");
        Assert.False(Directory.Exists(tempPath));

        var error = InvokeTryDeleteDirectoryWithRetry(tempPath, maxAttempts: 2, initialDelayMs: 10);
        Assert.Null(error);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public async Task TryDeleteDirectoryWithRetry_RetriesUntilLockedFileIsReleased_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-delete-retry-{Guid.NewGuid():N}");
        var lockedFilePath = Path.Combine(tempRoot, "runtime", "Kestrun.Annotations.dll");
        FileStream? lockedFileHandle = null;
        Task? releaseTask = null;

        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(lockedFilePath)!);
            File.WriteAllText(lockedFilePath, "locked", Encoding.UTF8);

            lockedFileHandle = new FileStream(lockedFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
            releaseTask = Task.Run(async () =>
            {
                await Task.Delay(300, TestContext.Current.CancellationToken);
                lockedFileHandle.Dispose();
            });

            var error = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 10, initialDelayMs: 50);
            Assert.Null(error);
            Assert.False(Directory.Exists(tempRoot));
        }
        finally
        {
            if (releaseTask is not null)
            {
                await releaseTask;
            }

            lockedFileHandle?.Dispose();

            if (Directory.Exists(tempRoot))
            {
                _ = InvokeTryDeleteDirectoryWithRetry(tempRoot, maxAttempts: 20, initialDelayMs: 50);
            }
        }
    }

    private static object Invoke(string methodName, params object?[] arguments)
        => InvokeRaw(methodName, arguments);

    private static void InvokeVoid(string methodName, params object?[] arguments)
    {
        var method = GetRequiredProgramMethod(methodName);
        Assert.Equal(typeof(void), method.ReturnType);
        var result = method.Invoke(null, arguments);
        Assert.Null(result);
    }

    private static object InvokeRaw(string methodName, object?[] arguments)
    {
        var method = GetRequiredProgramMethod(methodName);
        Assert.NotEqual(typeof(void), method.ReturnType);
        var result = method.Invoke(null, arguments);
        Assert.NotNull(result);
        return result;
    }

    private static MethodInfo GetRequiredProgramMethod(string methodName)
    {
        var method = ProgramType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method;
    }

    private static bool InvokeRequiredBool(MethodInfo method, object?[] arguments)
    {
        var result = method.Invoke(null, arguments);
        Assert.NotNull(result);
        _ = Assert.IsType<bool>(result);
        return (bool)result;
    }

    private static object InvokeWithStringArray(string methodName, string[] arguments)
        => InvokeRaw(methodName, [arguments]);

    private static (bool Success, object? ParsedCommand, string Error) InvokeTryParseArguments(string[] args)
    {
        var method = GetRequiredProgramMethod("TryParseArguments");

        var values = new object?[] { args, null, null };
        var success = InvokeRequiredBool(method, values);
        var error = values[2]?.ToString() ?? string.Empty;
        return (success, values[1], error);
    }

    private static (bool Success, object? Options, string Error) InvokeTryParseServiceRegisterArguments(string[] args)
    {
        var method = GetRequiredProgramMethod("TryParseServiceRegisterArguments");

        var values = new object?[] { args, null, null };
        var success = InvokeRequiredBool(method, values);
        var error = values[2]?.ToString() ?? string.Empty;
        return (success, values[1], error);
    }

    private static (bool Success, string Version) InvokeTryReadPackageVersion(byte[] packageBytes)
    {
        var method = GetRequiredProgramMethod("TryReadPackageVersion");

        var values = new object?[] { packageBytes, null };
        var success = InvokeRequiredBool(method, values);
        var version = values[1]?.ToString() ?? string.Empty;
        return (success, version);
    }

    private static (bool Success, string Version) InvokeTryReadModuleSemanticVersionFromManifest(string manifestPath)
    {
        var method = GetRequiredProgramMethod("TryReadModuleSemanticVersionFromManifest");

        var values = new object?[] { manifestPath, null };
        var success = InvokeRequiredBool(method, values);
        var version = values[1]?.ToString() ?? string.Empty;
        return (success, version);
    }

    private static (bool Success, string Error) InvokeTryValidateInstallAction(string moduleRoot, string scopeToken)
    {
        var method = GetRequiredProgramMethod("TryValidateInstallAction");

        var values = new object?[] { moduleRoot, scopeToken, null };
        var success = InvokeRequiredBool(method, values);
        var error = values[2]?.ToString() ?? string.Empty;
        return (success, error);
    }

    private static (bool Success, string Error) InvokeTryValidateUpdateAction(string moduleRoot, string packageVersion, bool force)
    {
        var method = GetRequiredProgramMethod("TryValidateUpdateAction");

        var values = new object?[] { moduleRoot, packageVersion, force, null };
        var success = InvokeRequiredBool(method, values);
        var error = values[3]?.ToString() ?? string.Empty;
        return (success, error);
    }

    private static (bool Success, string Error) InvokeTryRemoveInstalledModule(string moduleRoot, bool showProgress)
    {
        var method = GetRequiredProgramMethod("TryRemoveInstalledModule");

        var values = new object?[] { moduleRoot, showProgress, null };
        var success = InvokeRequiredBool(method, values);
        var error = values[2]?.ToString() ?? string.Empty;
        return (success, error);
    }

    private static (bool Success, string RuntimePath, string Error) InvokeTryResolveServiceRuntimeExecutableFromModule(string manifestPath)
    {
        var method = GetRequiredProgramMethod("TryResolveServiceRuntimeExecutableFromModule");

        var values = new object?[] { manifestPath, null, null };
        var success = InvokeRequiredBool(method, values);
        var runtimePath = values[1]?.ToString() ?? string.Empty;
        var error = values[2]?.ToString() ?? string.Empty;
        return (success, runtimePath, error);
    }

    private static (bool Success, object? Bundle, string BundleRootPath, string RuntimeExecutablePath, string ScriptPath, string ModuleManifestPath, string Error)
        InvokeTryPrepareServiceBundle(
            string serviceName,
            string scriptPath,
            string manifestPath,
            string bundleRoot,
            string? contentRoot = null,
            string? relativeScriptPath = null)
    {
        var method = GetRequiredProgramMethod("TryPrepareServiceBundle");

        var values = new object?[]
        {
            serviceName,
            scriptPath,
            manifestPath,
            contentRoot,
            relativeScriptPath ?? Path.GetFileName(scriptPath),
            null,
            null,
            bundleRoot,
        };
        var success = InvokeRequiredBool(method, values);
        var bundle = values[5];
        var error = values[6]?.ToString() ?? string.Empty;

        return (
            success,
            bundle,
            GetBundleField(bundle, "RootPath"),
            GetBundleField(bundle, "RuntimeExecutablePath"),
            GetBundleField(bundle, "ScriptPath"),
            GetBundleField(bundle, "ModuleManifestPath"),
            error);
    }

    private static (bool Success, string FullScriptPath, string FullContentRoot, string RelativeScriptPath, string Error)
        InvokeTryResolveServiceScriptSource(object parsedCommand)
    {
        var method = GetRequiredProgramMethod("TryResolveServiceScriptSource");

        var values = new object?[] { parsedCommand, null, null };
        var success = InvokeRequiredBool(method, values);
        var source = values[1];
        var error = values[2]?.ToString() ?? string.Empty;

        var fullScriptPath = string.Empty;
        var fullContentRoot = string.Empty;
        var relativeScriptPath = string.Empty;
        if (source is not null)
        {
            fullScriptPath = GetRecordField(source, "FullScriptPath") ?? string.Empty;
            fullContentRoot = GetRecordField(source, "FullContentRoot") ?? string.Empty;
            relativeScriptPath = GetRecordField(source, "RelativeScriptPath") ?? string.Empty;
        }

        return (
            success,
            fullScriptPath,
            fullContentRoot,
            relativeScriptPath,
            error);
    }

    private static (bool Success, string NormalizedContentRoot, Uri? ContentRootUri, string FullContentRoot)
        InvokeTryClassifyServiceContentRoot(string? contentRoot)
    {
        var method = GetRequiredProgramMethod("TryClassifyServiceContentRoot");

        var values = new object?[] { contentRoot, null, null, null };
        var success = InvokeRequiredBool(method, values);
        var normalizedContentRoot = values[1]?.ToString() ?? string.Empty;
        var contentRootUri = values[2] as Uri;
        var fullContentRoot = values[3]?.ToString() ?? string.Empty;

        return (success, normalizedContentRoot, contentRootUri, fullContentRoot);
    }

    private static (bool Success, string ServiceName, string FullScriptPath, string RelativeScriptPath, string TemporaryContentRootPath, string ModuleManifestPath, int ExitCode)
        InvokeTryResolveInstallServiceInputs(object parsedCommand)
    {
        var method = GetRequiredProgramMethod("TryResolveInstallServiceInputs");

        var values = new object?[] { parsedCommand, null, null, null, 0 };
        var success = InvokeRequiredBool(method, values);

        var serviceName = values[1]?.ToString() ?? string.Empty;
        var moduleManifestPath = values[3]?.ToString() ?? string.Empty;
        var exitCode = values[4] is int intExitCode ? intExitCode : 0;

        var fullScriptPath = string.Empty;
        var relativeScriptPath = string.Empty;
        var temporaryContentRootPath = string.Empty;
        var scriptSource = values[2];
        if (scriptSource is not null)
        {
            fullScriptPath = GetRecordField(scriptSource, "FullScriptPath") ?? string.Empty;
            relativeScriptPath = GetRecordField(scriptSource, "RelativeScriptPath") ?? string.Empty;
            temporaryContentRootPath = GetRecordField(scriptSource, "TemporaryContentRootPath") ?? string.Empty;
        }

        return (success, serviceName, fullScriptPath, relativeScriptPath, temporaryContentRootPath, moduleManifestPath, exitCode);
    }

    private static IReadOnlyList<string> InvokeBuildElevatedRelaunchArguments(string executablePath, IReadOnlyList<string> args) => Assert.IsAssignableFrom<IReadOnlyList<string>>(InvokeRaw("BuildElevatedRelaunchArguments", [executablePath, args]));

    private static string InvokeNormalizeWindowsServiceAccountName(string serviceUser) => Assert.IsType<string>(InvokeRaw("NormalizeWindowsServiceAccountName", [serviceUser]));

    private static bool InvokeIsWindowsBuiltinServiceAccount(string accountName) => InvokeRequiredBool(GetRequiredProgramMethod("IsWindowsBuiltinServiceAccount"), [accountName]);

    private static string InvokeBuildLinuxSystemdUnitContent(
        string serviceName,
        string exePath,
        IReadOnlyList<string> runnerArgs,
        string workingDirectory,
        string? serviceUser) => Assert.IsType<string>(InvokeRaw("BuildLinuxSystemdUnitContent", [serviceName, exePath, runnerArgs, workingDirectory, serviceUser]));

    private static string InvokeBuildLaunchdPlist(
        string label,
        string workingDirectory,
        IReadOnlyList<string> programArguments,
        string? serviceUser) => Assert.IsType<string>(InvokeRaw("BuildLaunchdPlist", [label, workingDirectory, programArguments, serviceUser]));

    private static string? InvokeTryDeleteDirectoryWithRetry(string directoryPath, int maxAttempts, int initialDelayMs)
    {
        var method = GetRequiredProgramMethod("TryDeleteDirectoryWithRetry");

        try
        {
            _ = method.Invoke(null, [directoryPath, maxAttempts, initialDelayMs]);
            return null;
        }
        catch (TargetInvocationException ex)
        {
            return ex.InnerException?.Message ?? ex.Message;
        }
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
        return property.GetValue(bundle)?.ToString() ?? string.Empty;
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

    private static void CreateZipArchive(string zipPath, IReadOnlyDictionary<string, string> entries)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var entry in entries)
        {
            var zipEntry = archive.CreateEntry(entry.Key);
            using var stream = zipEntry.Open();
            using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: false);
            writer.Write(entry.Value);
        }
    }

    private static void CreateTarGzArchive(string tgzPath, IReadOnlyDictionary<string, string> entries)
    {
        using var outputFile = File.Create(tgzPath);
        using var gzip = new GZipStream(outputFile, CompressionLevel.SmallestSize, leaveOpen: false);
        using var tarWriter = new TarWriter(gzip, leaveOpen: false);

        foreach (var entry in entries)
        {
            var bytes = Encoding.UTF8.GetBytes(entry.Value);
            using var payload = new MemoryStream(bytes, writable: false);
            var tarEntry = new PaxTarEntry(TarEntryType.RegularFile, entry.Key)
            {
                DataStream = payload,
            };

            tarWriter.WriteEntry(tarEntry);
        }
    }

    private static byte[] CreateZipArchiveBytes(IReadOnlyDictionary<string, string> entries)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                var zipEntry = archive.CreateEntry(entry.Key);
                using var stream = zipEntry.Open();
                using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: false);
                writer.Write(entry.Value);
            }
        }

        return memory.ToArray();
    }

    private static byte[] CreateTarGzArchiveBytes(IReadOnlyDictionary<string, string> entries)
    {
        using var memory = new MemoryStream();
        using (var gzip = new GZipStream(memory, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var tarWriter = new TarWriter(gzip, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                var bytes = Encoding.UTF8.GetBytes(entry.Value);
                using var payload = new MemoryStream(bytes, writable: false);
                var tarEntry = new PaxTarEntry(TarEntryType.RegularFile, entry.Key)
                {
                    DataStream = payload,
                };

                tarWriter.WriteEntry(tarEntry);
            }
        }

        return memory.ToArray();
    }

    private static SingleRequestHttpServer StartSingleRequestHttpServer(Func<HttpListenerContext, Task> requestHandler)
    {
        var (listener, prefix) = StartHttpListenerWithRetry();
        var completion = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            await requestHandler(context).ConfigureAwait(false);
        });

        return new SingleRequestHttpServer(listener, prefix, completion);
    }

    private sealed class SingleRequestHttpServer(HttpListener listener, string prefix, Task completion) : IAsyncDisposable
    {
        public string Prefix { get; } = prefix;

        public Task Completion { get; } = completion;

        public async ValueTask DisposeAsync()
        {
            listener.Stop();
            listener.Close();

            try
            {
                await Completion.ConfigureAwait(false);
            }
            catch (HttpListenerException)
            {
                // Listener shutdown can race with GetContextAsync() during cleanup.
            }
            catch (ObjectDisposedException)
            {
                // Listener shutdown can race with GetContextAsync() during cleanup.
            }
        }
    }

    private static (HttpListener Listener, string Prefix) StartHttpListenerWithRetry(int maxAttempts = 8)
    {
        HttpListenerException? lastException = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var prefix = $"http://127.0.0.1:{FindAvailablePort()}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);

            try
            {
                listener.Start();
                return (listener, prefix);
            }
            catch (HttpListenerException ex)
            {
                listener.Close();
                lastException = ex;
            }
            catch
            {
                listener.Close();
                throw;
            }
        }

        throw new InvalidOperationException(
            $"Failed to start test HttpListener after {maxAttempts} attempts.",
            lastException);
    }

    private static int FindAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string GetParsedCommandMode(object parsedCommand)
    {
        var mode = GetParsedCommandField(parsedCommand, "Mode");
        Assert.NotNull(mode);
        return mode;
    }

    private static string? GetParsedCommandField(object parsedCommand, string propertyName)
    {
        var property = parsedCommand.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property.GetValue(parsedCommand)?.ToString();
    }

    private static string[] GetParsedCommandStringArrayField(object parsedCommand, string propertyName)
    {
        var property = parsedCommand.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsType<string[]>(property.GetValue(parsedCommand));
    }

    private static string? GetRecordField(object record, string propertyName)
    {
        var property = record.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property.GetValue(record)?.ToString();
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
