using System.Reflection;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace Kestrun.Tool.Tests.Tooling;

public class KestrunToolProgramAndServiceUpdateCoverageTests
{
    private static readonly Type ProgramType = ResolveProgramType();
    private static readonly Type ParsedCommandType = ProgramType.GetNestedType("ParsedCommand", BindingFlags.NonPublic)!;
    private static readonly Type CommandModeType = ProgramType.GetNestedType("CommandMode", BindingFlags.NonPublic)!;
    private static readonly Type ModuleStorageScopeType = ProgramType.GetNestedType("ModuleStorageScope", BindingFlags.NonPublic)!;
    private static readonly Type GlobalOptionsType = ProgramType.GetNestedType("GlobalOptions", BindingFlags.NonPublic)!;
    private static readonly Type ServiceUpdatePathsType = ProgramType.GetNestedType("ServiceUpdatePaths", BindingFlags.NonPublic)!;
    private static readonly Type ServiceInstallDescriptorType = ProgramType.GetNestedType("ServiceInstallDescriptor", BindingFlags.NonPublic)!;
    private static readonly Type ServiceBackupSnapshotType = ProgramType.GetNestedType("ServiceBackupSnapshot", BindingFlags.NonPublic)!;
    private static readonly Type ServiceBundleLayoutType = ProgramType.GetNestedType("ServiceBundleLayout", BindingFlags.NonPublic)!;
    private static readonly Type ResolvedServiceScriptSourceType = ProgramType.GetNestedType("ResolvedServiceScriptSource", BindingFlags.NonPublic)!;
    private static readonly Type ProcessResultType = ProgramType.GetNestedType("ProcessResult", BindingFlags.NonPublic)!;

    [Fact]
    [Trait("Category", "Tooling")]
    public void Main_MetaAndUnknownCommands_ReturnExpectedExitCodes()
    {
        var helpExitCode = InvokeInt("Main", (object)new[] { "help" });
        var unknownExitCode = InvokeInt("Main", (object)new[] { "unknown-command" });

        Assert.Equal(0, helpExitCode);
        Assert.Equal(2, unknownExitCode);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryHandleInternalServiceRegisterMode_HandlesMissingValuesAndNonRegisterCommands()
    {
        var normalArgs = new object?[] { new[] { "run" }, 0 };
        var normalHandled = InvokeBool("TryHandleInternalServiceRegisterMode", normalArgs);
        Assert.False(normalHandled);
        Assert.Equal(0, Assert.IsType<int>(normalArgs[1]));

        var invalidRegisterArgs = new object?[] { new[] { "--service-register", "--name" }, 0 };
        var invalidRegisterHandled = InvokeBool("TryHandleInternalServiceRegisterMode", invalidRegisterArgs);
        Assert.True(invalidRegisterHandled);
        Assert.Equal(2, Assert.IsType<int>(invalidRegisterArgs[1]));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryDispatchParsedCommand_RunMode_IsNotHandled()
    {
        var parsed = CreateParsedCommand(modeName: "Run", scriptPath: "./missing.ps1");
        var options = CreateGlobalOptions(["run"], skipGalleryCheck: true);
        var args = new object?[] { parsed, options, new[] { "run" }, 0 };

        var handled = InvokeBool("TryDispatchParsedCommand", args);

        Assert.False(handled);
        Assert.Equal(0, Assert.IsType<int>(args[3]));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void ServiceCommandHandlers_OnNonWindows_DelegateAndReturnValidationErrors()
    {
        var remove = CreateParsedCommand(modeName: "ServiceRemove", serviceName: null);
        var start = CreateParsedCommand(modeName: "ServiceStart", serviceName: null);
        var stop = CreateParsedCommand(modeName: "ServiceStop", serviceName: null);

        Assert.Equal(2, InvokeInt("HandleServiceRemoveCommand", remove, new[] { "service", "remove" }));
        Assert.Equal(2, InvokeInt("HandleServiceStartCommand", start, new[] { "service", "start" }));
        Assert.Equal(2, InvokeInt("HandleServiceStopCommand", stop, new[] { "service", "stop" }));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void ExecuteRunMode_MissingScript_Returns2()
    {
        var parsed = CreateParsedCommand(
            modeName: "Run",
            scriptPath: Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.ps1"),
            scriptPathProvided: true);

        var exitCode = InvokeInt("ExecuteRunMode", parsed, true);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void DaemonAndWindowsCommandHelpers_BuildExpectedArguments()
    {
        var daemonArgs = Assert.IsAssignableFrom<IReadOnlyList<string>>(Invoke("BuildDaemonHostArgumentsForService",
            "demo-service",
            "not-a-service-host.exe",
            "/usr/bin/pwsh",
            "/tmp/Service.ps1",
            "/tmp/Kestrun.psd1",
            new[] { "--port", "9000" },
            "/tmp/service.log"));

        Assert.Contains("run", daemonArgs);
        Assert.Contains("--arguments", daemonArgs);

        Assert.True(InvokeBool("UsesDedicatedServiceHostExecutable", "kestrun-service-host"));
        Assert.True(InvokeBool("UsesDedicatedServiceHostExecutable", "Kestrun.ServiceHost.exe"));
        Assert.False(InvokeBool("UsesDedicatedServiceHostExecutable", "kestrun.exe"));

        var escaped = Assert.IsType<string>(Invoke("EscapeWindowsCommandLineArgument", "hello world"));
        Assert.StartsWith("\"", escaped, StringComparison.Ordinal);

        var commandLine = Assert.IsType<string>(Invoke("BuildWindowsCommandLine", "C:/Program Files/Kestrun/kestrun.exe", new[] { "--name", "demo service" }));
        Assert.Contains("\"C:/Program Files/Kestrun/kestrun.exe\"", commandLine, StringComparison.Ordinal);
        Assert.Contains("\"demo service\"", commandLine, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void NormalizeServiceLogPath_AndGetLinuxUnitName_HandleCommonInputs()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-log-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempRoot);
        try
        {
            var normalized = Assert.IsType<string>(Invoke("NormalizeServiceLogPath", tempRoot, "service.log"));
            Assert.EndsWith(Path.Combine(Path.GetFileName(tempRoot), "service.log"), normalized, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

            var unitName = Assert.IsType<string>(Invoke("GetLinuxUnitName", "demo service@1"));
            Assert.Equal("demo-service-1.service", unitName);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void RunProcess_AndProgressFormatters_ReturnExpectedValues()
    {
        var processResult = OperatingSystem.IsWindows()
            ? Invoke("RunProcess", "cmd.exe", new[] { "/c", "echo out & echo err 1>&2 & exit 3" }, false)!
            : Invoke("RunProcess", "/bin/sh", new[] { "-c", "printf out; printf err 1>&2; exit 3" }, false)!;
        Assert.Equal(3, GetRecordInt(processResult, "ExitCode"));
        Assert.Contains("out", GetRecordString(processResult, "Output"), StringComparison.Ordinal);
        Assert.Contains("err", GetRecordString(processResult, "Error"), StringComparison.Ordinal);

        Assert.Equal("1.5 KB", Assert.IsType<string>(Invoke("FormatByteSize", 1536L)!));
        Assert.Equal("1 KB / 2 KB", Assert.IsType<string>(Invoke("FormatByteProgressDetail", 1024L, 2048L)!));
        Assert.Equal("2/5 files", Assert.IsType<string>(Invoke("FormatFileProgressDetail", 2L, 5L)!));
        Assert.Contains("copying module", Assert.IsType<string>(Invoke("FormatServiceBundleStepProgressDetail", 3L, 5L)!), StringComparison.Ordinal);

        var localScope = Enum.Parse(ModuleStorageScopeType, "Local", ignoreCase: false);
        var globalScope = Enum.Parse(ModuleStorageScopeType, "Global", ignoreCase: false);
        Assert.Equal("local", Assert.IsType<string>(Invoke("GetScopeToken", localScope)!));
        Assert.Equal("global", Assert.IsType<string>(Invoke("GetScopeToken", globalScope)!));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void WriteLinuxUserSystemdFailureHint_HandlesDiagnosticTextWithoutThrowing()
    {
        var result = Activator.CreateInstance(
            ProcessResultType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [1, string.Empty, "Failed to connect to bus"],
            culture: null);
        Assert.NotNull(result);

        _ = Invoke("WriteLinuxUserSystemdFailureHint", result);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryValidateUpdateServiceCommand_CoversInvalidAndValidCombinations()
    {
        var failbackWithRepo = CreateParsedCommand(modeName: "ServiceUpdate", serviceFailback: true, serviceUseRepositoryKestrun: true);
        var argsFailback = new object?[] { failbackWithRepo, false, false, 0 };
        Assert.False(InvokeBool("TryValidateUpdateServiceCommand", argsFailback));
        Assert.Equal(2, Assert.IsType<int>(argsFailback[3]));

        var repoAndManifest = CreateParsedCommand(modeName: "ServiceUpdate", serviceUseRepositoryKestrun: true, kestrunManifestPath: "./Kestrun.psd1");
        var argsRepoManifest = new object?[] { repoAndManifest, false, false, 0 };
        Assert.False(InvokeBool("TryValidateUpdateServiceCommand", argsRepoManifest));
        Assert.Equal(2, Assert.IsType<int>(argsRepoManifest[3]));

        var missingAll = CreateParsedCommand(modeName: "ServiceUpdate");
        var argsMissingAll = new object?[] { missingAll, false, false, 0 };
        Assert.False(InvokeBool("TryValidateUpdateServiceCommand", argsMissingAll));
        Assert.Equal(2, Assert.IsType<int>(argsMissingAll[3]));

        var packageOnly = CreateParsedCommand(modeName: "ServiceUpdate", serviceContentRoot: "./demo.krpack");
        var argsPackageOnly = new object?[] { packageOnly, false, false, 0 };
        Assert.True(InvokeBool("TryValidateUpdateServiceCommand", argsPackageOnly));
        Assert.True(Assert.IsType<bool>(argsPackageOnly[1]));
        Assert.False(Assert.IsType<bool>(argsPackageOnly[2]));
        Assert.Equal(0, Assert.IsType<int>(argsPackageOnly[3]));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveSourceModuleRoot_AndBackupSelection_WorkAsExpected()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-source-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempRoot);
        try
        {
            var existingManifest = Path.Combine(tempRoot, "Kestrun.psd1");
            File.WriteAllText(existingManifest, "@{}", Encoding.UTF8);

            var sourceArgs = new object?[] { existingManifest, null, null };
            Assert.True(InvokeBool("TryResolveSourceModuleRoot", sourceArgs));
            Assert.Equal(Path.GetFullPath(tempRoot), Path.GetFullPath(Assert.IsType<string>(sourceArgs[1])));

            var missingArgs = new object?[] { Path.Combine(tempRoot, "missing", "Kestrun.psd1"), null, null };
            Assert.False(InvokeBool("TryResolveSourceModuleRoot", missingArgs));
            Assert.Contains("Unable to resolve module root", Assert.IsType<string>(missingArgs[2]), StringComparison.Ordinal);

            var latestArgs = new object?[] { tempRoot, null, null };
            Assert.False(InvokeBool("TryResolveLatestServiceBackupDirectory", latestArgs));
            Assert.Contains("No backup folder", Assert.IsType<string>(latestArgs[2]), StringComparison.Ordinal);

            var backupRoot = Path.Combine(tempRoot, "backup");
            _ = Directory.CreateDirectory(Path.Combine(backupRoot, "20260101000000"));
            _ = Directory.CreateDirectory(Path.Combine(backupRoot, "20260201000000"));

            var latestWithFoldersArgs = new object?[] { tempRoot, null, null };
            Assert.True(InvokeBool("TryResolveLatestServiceBackupDirectory", latestWithFoldersArgs));
            Assert.EndsWith("20260201000000", Assert.IsType<string>(latestWithFoldersArgs[1]), StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryFailbackAndExecuteFailback_RestoreFromBackupAndRemoveConsumedFolder()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-failback-{Guid.NewGuid():N}");
        var scriptRoot = Path.Combine(tempRoot, "Application");
        var moduleRoot = Path.Combine(tempRoot, "Modules", "Kestrun");
        var backupPath = Path.Combine(tempRoot, "backup", "20260321010101");

        _ = Directory.CreateDirectory(scriptRoot);
        _ = Directory.CreateDirectory(moduleRoot);
        _ = Directory.CreateDirectory(Path.Combine(backupPath, "application"));
        _ = Directory.CreateDirectory(Path.Combine(backupPath, "module"));

        File.WriteAllText(Path.Combine(scriptRoot, "Service.ps1"), "old-script", Encoding.UTF8);
        File.WriteAllText(Path.Combine(moduleRoot, "Kestrun.psd1"), "old-manifest", Encoding.UTF8);
        File.WriteAllText(Path.Combine(backupPath, "application", "Service.ps1"), "new-script", Encoding.UTF8);
        File.WriteAllText(Path.Combine(backupPath, "module", "Kestrun.psd1"), "new-manifest", Encoding.UTF8);

        try
        {
            var failbackArgs = new object?[] { tempRoot, scriptRoot, moduleRoot, null, null };
            Assert.True(InvokeBool("TryFailbackServiceFromBackup", failbackArgs));
            Assert.Equal("new-script", File.ReadAllText(Path.Combine(scriptRoot, "Service.ps1"), Encoding.UTF8));
            Assert.Equal("new-manifest", File.ReadAllText(Path.Combine(moduleRoot, "Kestrun.psd1"), Encoding.UTF8));
            Assert.False(Directory.Exists(backupPath));

            var consumedBackupPath = Path.Combine(tempRoot, "backup", "20260321020202");
            _ = Directory.CreateDirectory(Path.Combine(consumedBackupPath, "application"));
            File.WriteAllText(Path.Combine(consumedBackupPath, "application", "Service.ps1"), "latest-script", Encoding.UTF8);

            var paths = CreateServiceUpdatePaths(tempRoot, scriptRoot, moduleRoot, consumedBackupPath);
            var executeArgs = new object?[] { paths, 0 };
            Assert.True(InvokeBool("TryExecuteServiceFailback", executeArgs));
            Assert.Equal(0, Assert.IsType<int>(executeArgs[1]));
            Assert.Equal("latest-script", File.ReadAllText(Path.Combine(scriptRoot, "Service.ps1"), Encoding.UTF8));
            Assert.False(Directory.Exists(consumedBackupPath));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryApplyServiceApplicationAndModuleReplacement_CopiesBackupAndPreservedFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-update-apply-{Guid.NewGuid():N}");
        var scriptRoot = Path.Combine(tempRoot, "Application");
        var moduleRoot = Path.Combine(tempRoot, "Modules", "Kestrun");
        var backupRoot = Path.Combine(tempRoot, "backup", "20260321101010");
        var contentRoot = Path.Combine(tempRoot, "incoming-app");
        var sourceModuleRoot = Path.Combine(tempRoot, "incoming-module");

        _ = Directory.CreateDirectory(Path.Combine(scriptRoot, "keep"));
        _ = Directory.CreateDirectory(moduleRoot);
        _ = Directory.CreateDirectory(contentRoot);
        _ = Directory.CreateDirectory(sourceModuleRoot);

        File.WriteAllText(Path.Combine(scriptRoot, "site.txt"), "old-site", Encoding.UTF8);
        File.WriteAllText(Path.Combine(scriptRoot, "keep", "secret.txt"), "old-secret", Encoding.UTF8);
        File.WriteAllText(Path.Combine(contentRoot, "site.txt"), "new-site", Encoding.UTF8);

        File.WriteAllText(Path.Combine(moduleRoot, "Kestrun.psd1"), "old-module", Encoding.UTF8);
        File.WriteAllText(Path.Combine(sourceModuleRoot, "Kestrun.psd1"), "new-module", Encoding.UTF8);

        try
        {
            var paths = CreateServiceUpdatePaths(tempRoot, scriptRoot, moduleRoot, backupRoot);

            var appArgs = new object?[] { paths, contentRoot, new[] { "keep/secret.txt" }, 0 };
            Assert.True(InvokeBool("TryApplyServiceApplicationReplacement", appArgs));
            Assert.Equal(0, Assert.IsType<int>(appArgs[3]));

            Assert.Equal("new-site", File.ReadAllText(Path.Combine(scriptRoot, "site.txt"), Encoding.UTF8));
            Assert.Equal("old-secret", File.ReadAllText(Path.Combine(scriptRoot, "keep", "secret.txt"), Encoding.UTF8));
            Assert.True(File.Exists(Path.Combine(backupRoot, "application", "site.txt")));

            var moduleArgs = new object?[] { sourceModuleRoot, paths, false, 0 };
            Assert.True(InvokeBool("TryApplyDirectModuleReplacement", moduleArgs));
            Assert.True(Assert.IsType<bool>(moduleArgs[2]));
            Assert.Equal(0, Assert.IsType<int>(moduleArgs[3]));
            Assert.Equal("new-module", File.ReadAllText(Path.Combine(moduleRoot, "Kestrun.psd1"), Encoding.UTF8));
            Assert.True(File.Exists(Path.Combine(backupRoot, "module", "Kestrun.psd1")));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void RepositoryManifestAndModuleUpdateEvaluation_WorkForMissingAndNewerBundledVersions()
    {
        var repoRoot = FindRepositoryRoot();
        Assert.False(string.IsNullOrWhiteSpace(repoRoot));

        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-module-version-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempRoot);

        try
        {
            var resolvedManifestPath = Assert.IsType<string>(Invoke("ResolveRepositoryModuleManifestPath", repoRoot));
            Assert.True(File.Exists(resolvedManifestPath));
            Assert.EndsWith(Path.Combine("src", "PowerShell", "Kestrun", "Kestrun.psd1"), resolvedManifestPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

            var repositoryManifestPath = Path.Combine(tempRoot, "repo.psd1");
            File.WriteAllText(repositoryManifestPath, "@{\nModuleVersion = '1.2.0'\n}", Encoding.UTF8);

            var missingBundledArgs = new object?[] { repositoryManifestPath, Path.Combine(tempRoot, "missing.psd1"), false, null, null };
            Assert.True(InvokeBool("TryEvaluateRepositoryModuleUpdateNeeded", missingBundledArgs));
            Assert.True(Assert.IsType<bool>(missingBundledArgs[2]));

            var bundledManifestPath = Path.Combine(tempRoot, "bundled.psd1");
            File.WriteAllText(bundledManifestPath, "@{\nModuleVersion = '1.3.0'\n}", Encoding.UTF8);

            var newerBundledArgs = new object?[] { repositoryManifestPath, bundledManifestPath, false, null, null };
            Assert.True(InvokeBool("TryEvaluateRepositoryModuleUpdateNeeded", newerBundledArgs));
            Assert.False(Assert.IsType<bool>(newerBundledArgs[2]));
            Assert.Contains("current or newer", Assert.IsType<string>(newerBundledArgs[3]), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void ServiceInfoAndUpdateSummaryWriters_RenderExpectedContent()
    {
        var descriptor = CreateServiceInstallDescriptor(
            formatVersion: "1.0",
            name: "demo",
            entryPoint: "Service.ps1",
            description: "Demo",
            version: "1.2.3",
            serviceLogPath: "./logs/service.log",
            preservePaths: ["keep/secret.txt"]);

        var backup = CreateServiceBackupSnapshot(
            version: "20260321121212",
            updatedAtUtc: DateTimeOffset.UtcNow,
            path: "/tmp/backup");

        var backupListType = typeof(List<>).MakeGenericType(ServiceBackupSnapshotType);
        var backupList = Activator.CreateInstance(backupListType)!;
        _ = backupListType.GetMethod("Add")!.Invoke(backupList, [backup]);

        using var writer = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(writer);

            _ = Invoke("WriteServiceInfoHumanReadable", "demo", "/tmp/service", "/tmp/service/Application/Service.psd1", descriptor, backupList);

            var paths = CreateServiceUpdatePaths("/tmp/service", "/tmp/service/Application", "/tmp/service/Modules/Kestrun", Path.Combine(Path.GetTempPath(), "kestrun-summary-backup"));
            _ = Directory.CreateDirectory(GetRecordString(paths, "BackupRoot"));
            _ = Invoke("WriteServiceUpdateSummary", "demo", paths, true, false, true);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = writer.ToString();
        Assert.Contains("Name: demo", output, StringComparison.Ordinal);
        Assert.Contains("Backups: 1", output, StringComparison.Ordinal);
        Assert.Contains("\"ServiceName\": \"demo\"", output, StringComparison.Ordinal);
        Assert.Contains("\"ServiceHostUpdated\": true", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveUpdateManifestPathAndServiceUpdatePaths_HandleSuccessAndFailureCases()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-update-paths-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempRoot);
        var repoRoot = FindRepositoryRoot();
        Assert.False(string.IsNullOrWhiteSpace(repoRoot));

        try
        {
            var missingManifestCommand = CreateParsedCommand(modeName: "ServiceUpdate", kestrunManifestPath: Path.Combine(tempRoot, "missing.psd1"));
            var missingManifestArgs = new object?[] { missingManifestCommand, null, 0 };
            Assert.False(InvokeBool("TryResolveUpdateManifestPath", missingManifestArgs));
            Assert.Equal(3, Assert.IsType<int>(missingManifestArgs[2]));

            var repositoryCommand = CreateParsedCommand(modeName: "ServiceUpdate", serviceUseRepositoryKestrun: true);
            var repositoryArgs = new object?[] { repositoryCommand, repoRoot, null, 0 };
            Assert.True(InvokeBool("TryResolveUpdateManifestPath", repositoryArgs));
            Assert.True(File.Exists(Assert.IsType<string>(repositoryArgs[2])));
            Assert.Equal(0, Assert.IsType<int>(repositoryArgs[3]));

            var serviceDirectoryName = Assert.IsType<string>(Invoke("GetServiceDeploymentDirectoryName", "Demo.Service")!);
            var serviceRoot = Path.Combine(tempRoot, serviceDirectoryName);
            var applicationRoot = Path.Combine(serviceRoot, "Application");
            _ = Directory.CreateDirectory(applicationRoot);
            File.WriteAllText(Path.Combine(applicationRoot, "Service.psd1"), "@{`nFormatVersion='1.0'`nName='Demo.Service'`nEntryPoint='Service.ps1'`nDescription='Demo'`n}", Encoding.UTF8);

            var pathArgs = new object?[] { "Demo.Service", tempRoot, null, 0 };
            Assert.True(InvokeBool("TryResolveServiceUpdatePaths", pathArgs));
            Assert.Equal(0, Assert.IsType<int>(pathArgs[3]));

            var resolvedPaths = pathArgs[2]!;
            Assert.Equal(Path.GetFullPath(serviceRoot), Path.GetFullPath(GetRecordString(resolvedPaths, "ServiceRootPath")));
            Assert.EndsWith(Path.Combine("Application"), GetRecordString(resolvedPaths, "ScriptRoot"), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryPrepareServiceUpdateExecution_WithMissingService_FailsPreflight()
    {
        var args = new object?[] { $"missing-{Guid.NewGuid():N}", null, null, 0 };

        var success = InvokeBool("TryPrepareServiceUpdateExecution", args);

        Assert.False(success);
        Assert.Equal(1, Assert.IsType<int>(args[3]));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryEnsureWindowsServiceIsStopped_ReturnsMeaningfulErrors()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var knownServiceArgs = new object?[] { "EventLog", string.Empty };
        Assert.False(InvokeBool("TryEnsureWindowsServiceIsStopped", knownServiceArgs));
        Assert.True(!string.IsNullOrWhiteSpace(Assert.IsType<string>(knownServiceArgs[1])));

        var missingServiceArgs = new object?[] { $"missing-{Guid.NewGuid():N}", string.Empty };
        Assert.False(InvokeBool("TryEnsureWindowsServiceIsStopped", missingServiceArgs));
        Assert.True(!string.IsNullOrWhiteSpace(Assert.IsType<string>(missingServiceArgs[1])));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryRunServiceUpdateOperations_AppliesPackageAndModuleBeforeHostUpdateCheck()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-update-ops-{Guid.NewGuid():N}");
        var serviceRoot = Path.Combine(tempRoot, "service");
        var scriptRoot = Path.Combine(serviceRoot, "Application");
        var moduleRoot = Path.Combine(serviceRoot, "Modules", "Kestrun");
        var runtimeRoot = Path.Combine(serviceRoot, "runtime");
        var backupRoot = Path.Combine(serviceRoot, "backup", "20260321140000");
        var incomingContentRoot = Path.Combine(tempRoot, "incoming-app");
        var incomingModuleRoot = Path.Combine(tempRoot, "incoming-module");

        _ = Directory.CreateDirectory(Path.Combine(scriptRoot, "keep"));
        _ = Directory.CreateDirectory(moduleRoot);
        _ = Directory.CreateDirectory(runtimeRoot);
        _ = Directory.CreateDirectory(Path.Combine(incomingContentRoot, "keep"));
        _ = Directory.CreateDirectory(incomingModuleRoot);

        File.WriteAllText(Path.Combine(scriptRoot, "Service.ps1"), "old-script", Encoding.UTF8);
        File.WriteAllText(Path.Combine(scriptRoot, "keep", "secret.txt"), "old-secret", Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(scriptRoot, "Service.psd1"),
            "@{`nFormatVersion='1.0'`nName='demo-service'`nEntryPoint='Service.ps1'`nDescription='Demo'`nVersion='1.0.0'`n}",
            Encoding.UTF8);

        File.WriteAllText(Path.Combine(moduleRoot, "Kestrun.psd1"), "@{`nModuleVersion='1.0.0'`n}", Encoding.UTF8);

        File.WriteAllText(Path.Combine(incomingContentRoot, "Service.ps1"), "new-script", Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(incomingContentRoot, "Service.psd1"),
            "@{`nFormatVersion='1.0'`nName='demo-service'`nEntryPoint='Service.ps1'`nDescription='Demo update'`nVersion='2.0.0'`nPreservePaths=@('keep/secret.txt')`n}",
            Encoding.UTF8);
        File.WriteAllText(Path.Combine(incomingModuleRoot, "Kestrun.psd1"), "@{`nModuleVersion='2.0.0'`n}", Encoding.UTF8);

        try
        {
            var command = CreateParsedCommand(
                modeName: "ServiceUpdate",
                serviceName: "demo-service",
                serviceContentRoot: incomingContentRoot,
                kestrunManifestPath: Path.Combine(incomingModuleRoot, "Kestrun.psd1"));

            var scriptSource = Activator.CreateInstance(
                ResolvedServiceScriptSourceType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args:
                [
                    Path.Combine(incomingContentRoot, "Service.ps1"),
                    incomingContentRoot,
                    "Service.ps1",
                    null,
                    "demo-service",
                    "Demo update",
                    "2.0.0",
                    null,
                    new[] { "keep/secret.txt" }
                ],
                culture: null);
            Assert.NotNull(scriptSource);

            var paths = CreateServiceUpdatePaths(serviceRoot, scriptRoot, moduleRoot, backupRoot);
            var updateArgs = new object?[]
            {
                command,
                true,
                true,
                paths,
                scriptSource,
                true,
                false,
                false,
                false,
                0
            };

            var updateSuccess = InvokeBool("TryRunServiceUpdateOperations", updateArgs);
            Assert.Equal("new-script", File.ReadAllText(Path.Combine(scriptRoot, "Service.ps1"), Encoding.UTF8));
            Assert.Equal("old-secret", File.ReadAllText(Path.Combine(scriptRoot, "keep", "secret.txt"), Encoding.UTF8));
            Assert.Equal("@{`nModuleVersion='2.0.0'`n}", File.ReadAllText(Path.Combine(moduleRoot, "Kestrun.psd1"), Encoding.UTF8));
            Assert.True(Directory.Exists(Path.Combine(backupRoot, "application")));
            Assert.True(Directory.Exists(Path.Combine(backupRoot, "module")));
            Assert.Equal(updateSuccess ? 0 : 1, Assert.IsType<int>(updateArgs[9]));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void ArchiveExtensionAndArchiveNameHelpers_ResolveSupportedFormats()
    {
        var mediaTypeZipArgs = new object?[] { "application/zip", null };
        Assert.True(InvokeBool("TryGetServiceContentRootArchiveExtensionFromMediaType", mediaTypeZipArgs));
        Assert.Equal(".zip", Assert.IsType<string>(mediaTypeZipArgs[1]));

        var mediaTypeTarArgs = new object?[] { "application/x-tar", null };
        Assert.True(InvokeBool("TryGetServiceContentRootArchiveExtensionFromMediaType", mediaTypeTarArgs));
        Assert.Equal(".tar", Assert.IsType<string>(mediaTypeTarArgs[1]));

        var mediaTypeGzipArgs = new object?[] { "application/gzip", null };
        Assert.True(InvokeBool("TryGetServiceContentRootArchiveExtensionFromMediaType", mediaTypeGzipArgs));
        Assert.Equal(".tgz", Assert.IsType<string>(mediaTypeGzipArgs[1]));

        var unsupportedMediaTypeArgs = new object?[] { "application/json", null };
        Assert.False(InvokeBool("TryGetServiceContentRootArchiveExtensionFromMediaType", unsupportedMediaTypeArgs));
        Assert.Equal(string.Empty, Assert.IsType<string>(unsupportedMediaTypeArgs[1]));

        Assert.True(InvokeBool("IsSupportedServiceContentRootArchive", "demo.krpack"));
        Assert.True(InvokeBool("IsSupportedServiceContentRootArchive", "demo.zip"));
        Assert.True(InvokeBool("IsSupportedServiceContentRootArchive", "demo.tar"));
        Assert.True(InvokeBool("IsSupportedServiceContentRootArchive", "demo.tar.gz"));
        Assert.True(InvokeBool("IsSupportedServiceContentRootArchive", "demo.tgz"));
        Assert.False(InvokeBool("IsSupportedServiceContentRootArchive", "demo.txt"));

        Assert.Equal("bundle.tar.gz", Assert.IsType<string>(Invoke("BuildServiceContentRootArchiveFileName", "bundle.tar", ".tar.gz")));
        Assert.Equal("content-root.zip", Assert.IsType<string>(Invoke("BuildServiceContentRootArchiveFileName", string.Empty, ".zip")));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryDetectArchiveExtensionFromSignature_RecognizesZipTarAndTgz()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-signature-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempRoot);
        try
        {
            var zipPath = Path.Combine(tempRoot, "sample.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("hello.txt");
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write("hello");
            }

            var tgzPath = Path.Combine(tempRoot, "sample.tgz");
            File.WriteAllBytes(tgzPath, [0x1F, 0x8B, 0x08, 0x00, 0x00]);

            var tarPath = Path.Combine(tempRoot, "sample.tar");
            var tarBytes = new byte[512];
            tarBytes[257] = (byte)'u';
            tarBytes[258] = (byte)'s';
            tarBytes[259] = (byte)'t';
            tarBytes[260] = (byte)'a';
            tarBytes[261] = (byte)'r';
            File.WriteAllBytes(tarPath, tarBytes);

            var unknownPath = Path.Combine(tempRoot, "sample.bin");
            File.WriteAllBytes(unknownPath, [0x00, 0x01, 0x02, 0x03]);

            var zipArgs = new object?[] { zipPath, null };
            Assert.True(InvokeBool("TryDetectServiceContentRootArchiveExtensionFromSignature", zipArgs));
            Assert.Equal(".zip", Assert.IsType<string>(zipArgs[1]));

            var tgzArgs = new object?[] { tgzPath, null };
            Assert.True(InvokeBool("TryDetectServiceContentRootArchiveExtensionFromSignature", tgzArgs));
            Assert.Equal(".tgz", Assert.IsType<string>(tgzArgs[1]));

            var tarArgs = new object?[] { tarPath, null };
            Assert.True(InvokeBool("TryDetectServiceContentRootArchiveExtensionFromSignature", tarArgs));
            Assert.Equal(".tar", Assert.IsType<string>(tarArgs[1]));

            var unknownArgs = new object?[] { unknownPath, null };
            Assert.False(InvokeBool("TryDetectServiceContentRootArchiveExtensionFromSignature", unknownArgs));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveArchiveFileName_UsesDispositionUriAndMediaTypeFallback()
    {
        using var responseWithDisposition = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3])
        };
        responseWithDisposition.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = "bundle.krpack",
        };

        var fromDisposition = Invoke("TryResolveServiceContentRootArchiveFileName", new Uri("https://example.invalid/download"), responseWithDisposition);
        Assert.Equal("bundle.krpack", Assert.IsType<string>(fromDisposition));

        using var responseFromUri = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([4, 5, 6])
        };

        var fromUri = Invoke("TryResolveServiceContentRootArchiveFileName", new Uri("https://example.invalid/files/demo.tar"), responseFromUri);
        Assert.Equal("demo.tar", Assert.IsType<string>(fromUri));

        using var responseFromMediaType = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([7, 8, 9])
        };
        responseFromMediaType.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/gzip");

        var fromMediaType = Invoke("TryResolveServiceContentRootArchiveFileName", new Uri("https://example.invalid/"), responseFromMediaType);
        Assert.Equal("content-root.tgz", Assert.IsType<string>(fromMediaType));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void ChecksumHelpers_ValidateSupportedTokensAndArchiveDigestComparison()
    {
        var createShaArgs = new object?[] { "sha2", null, null, null };
        Assert.True(InvokeBool("TryCreateChecksumAlgorithm", createShaArgs));
        using (Assert.IsAssignableFrom<HashAlgorithm>(createShaArgs[1]))
        {
            Assert.Equal("sha256", Assert.IsType<string>(createShaArgs[2]));
            Assert.Equal(string.Empty, Assert.IsType<string>(createShaArgs[3]));
        }

        var createInvalidArgs = new object?[] { "sha3", null, null, null };
        Assert.False(InvokeBool("TryCreateChecksumAlgorithm", createInvalidArgs));
        Assert.Contains("Unsupported", Assert.IsType<string>(createInvalidArgs[3]), StringComparison.OrdinalIgnoreCase);

        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-checksum-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempRoot);
        try
        {
            var archivePath = Path.Combine(tempRoot, "payload.krpack");
            File.WriteAllText(archivePath, "payload", Encoding.UTF8);
            var expectedSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archivePath)));

            var validCommand = CreateParsedCommand(
                modeName: "ServiceInstall",
                serviceContentRootChecksum: expectedSha256,
                serviceContentRootChecksumAlgorithm: "sha256");

            var validArgs = new object?[] { validCommand, archivePath, null };
            Assert.True(InvokeBool("TryValidateServiceContentRootArchiveChecksum", validArgs));
            Assert.Equal(string.Empty, Assert.IsType<string>(validArgs[2]));

            var mismatchCommand = CreateParsedCommand(
                modeName: "ServiceInstall",
                serviceContentRootChecksum: new string('A', expectedSha256.Length),
                serviceContentRootChecksumAlgorithm: "sha256");

            var mismatchArgs = new object?[] { mismatchCommand, archivePath, null };
            Assert.False(InvokeBool("TryValidateServiceContentRootArchiveChecksum", mismatchArgs));
            Assert.Contains("mismatch", Assert.IsType<string>(mismatchArgs[2]), StringComparison.OrdinalIgnoreCase);

            var invalidHashCommand = CreateParsedCommand(
                modeName: "ServiceInstall",
                serviceContentRootChecksum: "zz-not-hex");

            var invalidHashArgs = new object?[] { invalidHashCommand, archivePath, null };
            Assert.False(InvokeBool("TryValidateServiceContentRootArchiveChecksum", invalidHashArgs));
            Assert.Contains("hexadecimal", Assert.IsType<string>(invalidHashArgs[2]), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveDeploymentRoot_AndTryRemoveServiceBundle_HandleOverridePaths()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-deploy-root-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempRoot);
        try
        {
            var resolveArgs = new object?[] { tempRoot, null, null };
            Assert.True(InvokeBool("TryResolveServiceDeploymentRoot", resolveArgs));
            Assert.Equal(Path.GetFullPath(tempRoot), Path.GetFullPath(Assert.IsType<string>(resolveArgs[1])));

            var invalidOverridePath = Path.Combine(tempRoot, "override-as-file");
            File.WriteAllText(invalidOverridePath, "not-a-directory", Encoding.UTF8);
            var invalidResolveArgs = new object?[] { invalidOverridePath, null, null };
            Assert.False(InvokeBool("TryResolveServiceDeploymentRoot", invalidResolveArgs));
            Assert.Contains("Unable to use deployment root", Assert.IsType<string>(invalidResolveArgs[2]), StringComparison.Ordinal);

            var serviceName = "demo.service";
            var serviceDirectoryName = Assert.IsType<string>(Invoke("GetServiceDeploymentDirectoryName", serviceName));
            var bundleRoot = Path.Combine(tempRoot, serviceDirectoryName);
            _ = Directory.CreateDirectory(Path.Combine(bundleRoot, "Application"));
            File.WriteAllText(Path.Combine(bundleRoot, "Application", "Service.ps1"), "Write-Output 'hello'", Encoding.UTF8);

            _ = Invoke("TryRemoveServiceBundle", serviceName, tempRoot);

            Assert.False(Directory.Exists(bundleRoot));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void InfoService_WithNamedAndEnumeratedBundles_ReturnsSuccess()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-info-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempRoot);
        try
        {
            CreateInstalledServiceDescriptor(tempRoot, "alpha-service", "1.2.3");
            CreateInstalledServiceDescriptor(tempRoot, "beta-service", "1.2.4");

            using var namedWriter = new StringWriter();
            var originalOut = Console.Out;
            try
            {
                Console.SetOut(namedWriter);
                var named = CreateParsedCommand(modeName: "ServiceInfo", serviceName: "alpha-service", serviceDeploymentRoot: tempRoot, jsonOutput: true);
                var namedExitCode = InvokeInt("InfoService", named);
                Assert.Equal(0, namedExitCode);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            Assert.Contains("\"ServiceName\": \"alpha-service\"", namedWriter.ToString(), StringComparison.Ordinal);

            using var listWriter = new StringWriter();
            originalOut = Console.Out;
            try
            {
                Console.SetOut(listWriter);
                var allServices = CreateParsedCommand(modeName: "ServiceInfo", serviceName: null, serviceNameProvided: false, serviceDeploymentRoot: tempRoot, jsonOutput: true);
                var allExitCode = InvokeInt("InfoService", allServices);
                Assert.Equal(0, allExitCode);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            var output = listWriter.ToString();
            Assert.Contains("\"ServiceName\": \"alpha-service\"", output, StringComparison.Ordinal);
            Assert.Contains("\"ServiceName\": \"beta-service\"", output, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void LinuxServiceParsersAndCommandHandlers_ExerciseCommonFailureAndParsePaths()
    {
        var inactiveResult = Activator.CreateInstance(
            ProcessResultType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [1, "inactive", "unit not loaded"],
            culture: null);
        Assert.NotNull(inactiveResult);
        Assert.True(InvokeBool("IsLinuxServiceAlreadyStopped", inactiveResult));

        var activeResult = Activator.CreateInstance(
            ProcessResultType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [1, "active", string.Empty],
            culture: null);
        Assert.NotNull(activeResult);
        Assert.False(InvokeBool("IsLinuxServiceAlreadyStopped", activeResult));

        var windowsPid = Invoke("TryExtractWindowsServicePid", "SERVICE_NAME: demo\nPID              : 4242\n");
        Assert.Equal(4242, Assert.IsType<int>(windowsPid));

        var macPid = Invoke("TryExtractMacServicePid", "\t1257\t0\tcom.demo.service\n");
        Assert.Equal(1257, Assert.IsType<int>(macPid));

        var noMacPid = Invoke("TryExtractMacServicePid", "state = running;");
        Assert.Null(noMacPid);

        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-linux-unit-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempRoot);
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var unitDir = Path.Combine(userProfile, ".config", "systemd", "user");
            _ = Directory.CreateDirectory(unitDir);

            var serviceName = "kestrun-unit-scope";
            var unitFilePath = Path.Combine(unitDir, Assert.IsType<string>(Invoke("GetLinuxUnitName", serviceName)));
            File.WriteAllText(unitFilePath, "[Unit]\nDescription=demo\n", Encoding.UTF8);

            var scopeArgs = new object?[] { serviceName, false };
            Assert.True(InvokeBool("TryGetInstalledLinuxUnitScope", scopeArgs));
            Assert.False(Assert.IsType<bool>(scopeArgs[1]));

            var missingScopeArgs = new object?[] { "missing-service", false };
            Assert.False(InvokeBool("TryGetInstalledLinuxUnitScope", missingScopeArgs));

            var start = CreateParsedCommand(modeName: "ServiceStart", serviceName: serviceName, serviceLogPath: Path.Combine(tempRoot, "service.log"));
            var stop = CreateParsedCommand(modeName: "ServiceStop", serviceName: serviceName, serviceLogPath: Path.Combine(tempRoot, "service.log"));
            var query = CreateParsedCommand(modeName: "ServiceQuery", serviceName: serviceName, serviceLogPath: Path.Combine(tempRoot, "service.log"), rawOutput: true);

            Assert.InRange(InvokeInt("StartService", start), 0, int.MaxValue);
            Assert.InRange(InvokeInt("StopService", stop), 0, int.MaxValue);
            Assert.InRange(InvokeInt("QueryService", query), 0, int.MaxValue);

            if (File.Exists(unitFilePath))
            {
                File.Delete(unitFilePath);
            }
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void LinuxAndMacServiceCommandHelpers_ReturnStructuredResults()
    {
        var missingLinuxService = $"missing-linux-{Guid.NewGuid():N}";
        try
        {
            var linuxStart = Invoke("StartLinuxUserDaemon", missingLinuxService, null, false);
            Assert.NotNull(linuxStart);
            Assert.Equal("linux", GetRecordString(linuxStart, "Platform"));
            Assert.Equal("start", GetRecordString(linuxStart, "Operation"));
            Assert.InRange(GetRecordInt(linuxStart, "ExitCode"), 1, int.MaxValue);
        }
        catch (TargetInvocationException ex)
        {
            _ = Assert.IsType<System.ComponentModel.Win32Exception>(ex.InnerException);
        }

        var linuxStop = Invoke("StopLinuxUserDaemon", $"missing-linux-{Guid.NewGuid():N}", null, false);
        Assert.NotNull(linuxStop);
        Assert.Equal("linux", GetRecordString(linuxStop, "Platform"));
        Assert.Equal("stop", GetRecordString(linuxStop, "Operation"));
        Assert.Equal(2, GetRecordInt(linuxStop, "ExitCode"));

        try
        {
            var linuxQuery = Invoke("QueryLinuxUserDaemon", missingLinuxService, null, false);
            Assert.NotNull(linuxQuery);
            Assert.Equal("linux", GetRecordString(linuxQuery, "Platform"));
            Assert.Equal("query", GetRecordString(linuxQuery, "Operation"));
            Assert.InRange(GetRecordInt(linuxQuery, "ExitCode"), 1, int.MaxValue);
        }
        catch (TargetInvocationException ex)
        {
            _ = Assert.IsType<System.ComponentModel.Win32Exception>(ex.InnerException);
        }

        var missingMacService = $"missing-mac-{Guid.NewGuid():N}";
        var macStart = Invoke("StartMacLaunchAgent", missingMacService, null, false);
        Assert.NotNull(macStart);
        Assert.Equal("macos", GetRecordString(macStart, "Platform"));
        Assert.Equal(2, GetRecordInt(macStart, "ExitCode"));

        var macStop = Invoke("StopMacLaunchAgent", missingMacService, null, false);
        Assert.NotNull(macStop);
        Assert.Equal("macos", GetRecordString(macStop, "Platform"));
        Assert.Equal(2, GetRecordInt(macStop, "ExitCode"));

        try
        {
            var macQuery = Invoke("QueryMacLaunchAgent", missingMacService, null, false);
            Assert.NotNull(macQuery);
            Assert.Equal("macos", GetRecordString(macQuery, "Platform"));
            Assert.Equal("query", GetRecordString(macQuery, "Operation"));
            Assert.InRange(GetRecordInt(macQuery, "ExitCode"), 1, int.MaxValue);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is System.ComponentModel.Win32Exception)
        {
            _ = Assert.IsType<System.ComponentModel.Win32Exception>(ex.InnerException);
        }

        try
        {
            var removeResult = InvokeInt("RemoveMacLaunchAgent", missingMacService);
            Assert.InRange(removeResult, 0, int.MaxValue);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is System.ComponentModel.Win32Exception)
        {
            _ = Assert.IsType<System.ComponentModel.Win32Exception>(ex.InnerException);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void InstallPreparedServiceForCurrentPlatform_OnLinux_UsesBundleLayoutAndReturnsCode()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-install-platform-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempRoot);
        try
        {
            var scriptPath = Path.Combine(tempRoot, "Service.ps1");
            File.WriteAllText(scriptPath, "Write-Output 'service'", Encoding.UTF8);

            var manifestPath = Path.Combine(tempRoot, "Kestrun.psd1");
            File.WriteAllText(manifestPath, "@{}", Encoding.UTF8);

            var serviceBundle = Activator.CreateInstance(
                ServiceBundleLayoutType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args:
                [
                    tempRoot,
                    "/usr/bin/pwsh",
                    "/usr/bin/kestrun-service-host",
                    scriptPath,
                    manifestPath
                ],
                culture: null);

            Assert.NotNull(serviceBundle);

            var parsed = CreateParsedCommand(modeName: "ServiceInstall", serviceName: "demo-service");
            var result = InvokeInt("InstallPreparedServiceForCurrentPlatform", parsed, "demo-service", Path.Combine(tempRoot, "service.log"), serviceBundle);

            Assert.InRange(result, 0, int.MaxValue);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static Type ResolveProgramType()
    {
        var assembly = Assembly.Load("Kestrun.Tool");
        var programType = assembly.GetType("Kestrun.Tool.Program", throwOnError: false);
        Assert.NotNull(programType);
        return programType;
    }

    private static MethodInfo GetMethod(string name, object?[] arguments)
    {
        var candidates = ProgramType
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(method => string.Equals(method.Name, name, StringComparison.Ordinal))
            .Where(method => method.GetParameters().Length == arguments.Length)
            .ToList();

        foreach (var candidate in candidates)
        {
            var parameters = candidate.GetParameters();
            var matches = true;
            for (var i = 0; i < parameters.Length; i++)
            {
                var argument = arguments[i];
                if (argument is null)
                {
                    continue;
                }

                var parameterType = parameters[i].ParameterType;
                if (parameterType.IsByRef)
                {
                    parameterType = parameterType.GetElementType()!;
                }

                if (!parameterType.IsInstanceOfType(argument))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Unable to resolve method '{name}'.");
    }

    private static object? Invoke(string name, params object?[] args)
    {
        var method = GetMethod(name, args);
        return method.Invoke(null, args);
    }

    private static bool InvokeBool(string name, params object?[] args)
    {
        var result = Invoke(name, args);
        Assert.NotNull(result);
        return Assert.IsType<bool>(result);
    }

    private static int InvokeInt(string name, params object?[] args)
    {
        var result = Invoke(name, args);
        Assert.NotNull(result);
        return Assert.IsType<int>(result);
    }

    private static object CreateParsedCommand(
        string modeName,
        string scriptPath = "Service.ps1",
        bool scriptPathProvided = false,
        string[]? scriptArguments = null,
        string? kestrunFolder = null,
        string? kestrunManifestPath = null,
        string? serviceName = null,
        bool? serviceNameProvided = null,
        string? serviceLogPath = null,
        string? serviceUser = null,
        string? servicePassword = null,
        string? moduleVersion = null,
        string moduleScopeName = "Local",
        bool moduleForce = false,
        string? serviceContentRoot = null,
        string? serviceDeploymentRoot = null,
        string? serviceContentRootChecksum = null,
        string? serviceContentRootChecksumAlgorithm = null,
        string? serviceContentRootBearerToken = null,
        bool serviceContentRootIgnoreCertificate = false,
        string[]? serviceContentRootHeaders = null,
        bool serviceFailback = false,
        bool serviceUseRepositoryKestrun = false,
        bool jsonOutput = false,
        bool rawOutput = false)
    {
        var mode = Enum.Parse(CommandModeType, modeName, ignoreCase: false);
        var scope = Enum.Parse(ModuleStorageScopeType, moduleScopeName, ignoreCase: false);

        var parsed = Activator.CreateInstance(
            ParsedCommandType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                mode,
                scriptPath,
                scriptPathProvided,
                scriptArguments ?? [],
                kestrunFolder,
                kestrunManifestPath,
                serviceName,
                serviceNameProvided ?? !string.IsNullOrWhiteSpace(serviceName),
                serviceLogPath,
                serviceUser,
                servicePassword,
                moduleVersion,
                scope,
                moduleForce,
                serviceContentRoot,
                serviceDeploymentRoot,
                serviceContentRootChecksum,
                serviceContentRootChecksumAlgorithm,
                serviceContentRootBearerToken,
                serviceContentRootIgnoreCertificate,
                serviceContentRootHeaders ?? [],
                serviceFailback,
                serviceUseRepositoryKestrun,
                jsonOutput,
                rawOutput,
            ],
            culture: null);

        Assert.NotNull(parsed);
        return parsed;
    }

    private static object CreateGlobalOptions(string[] commandArgs, bool skipGalleryCheck)
    {
        var options = Activator.CreateInstance(
            GlobalOptionsType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [commandArgs, skipGalleryCheck],
            culture: null);
        Assert.NotNull(options);
        return options;
    }

    private static object CreateServiceUpdatePaths(string serviceRootPath, string scriptRoot, string moduleRoot, string backupRoot)
    {
        var value = Activator.CreateInstance(
            ServiceUpdatePathsType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [serviceRootPath, scriptRoot, moduleRoot, backupRoot],
            culture: null);
        Assert.NotNull(value);
        return value;
    }

    private static object CreateServiceInstallDescriptor(
        string formatVersion,
        string name,
        string entryPoint,
        string description,
        string? version,
        string? serviceLogPath,
        IReadOnlyList<string> preservePaths)
    {
        var descriptor = Activator.CreateInstance(
            ServiceInstallDescriptorType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [formatVersion, name, entryPoint, description, version, serviceLogPath, preservePaths],
            culture: null);
        Assert.NotNull(descriptor);
        return descriptor;
    }

    private static object CreateServiceBackupSnapshot(string version, DateTimeOffset? updatedAtUtc, string path)
    {
        var snapshot = Activator.CreateInstance(
            ServiceBackupSnapshotType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [version, updatedAtUtc, path],
            culture: null);
        Assert.NotNull(snapshot);
        return snapshot;
    }

    private static void CreateInstalledServiceDescriptor(string deploymentRoot, string serviceName, string version)
    {
        var serviceDirectoryName = Assert.IsType<string>(Invoke("GetServiceDeploymentDirectoryName", serviceName));
        var serviceRoot = Path.Combine(deploymentRoot, serviceDirectoryName);
        var appRoot = Path.Combine(serviceRoot, "Application");
        _ = Directory.CreateDirectory(appRoot);
        File.WriteAllText(
            Path.Combine(appRoot, "Service.psd1"),
            $"@{{`nFormatVersion='1.0'`nName='{serviceName}'`nEntryPoint='Service.ps1'`nDescription='Demo'`nVersion='{version}'`nServiceLogPath='./logs/service.log'`n}}",
            Encoding.UTF8);

        var backupRoot = Path.Combine(serviceRoot, "backup", "20260101000000");
        _ = Directory.CreateDirectory(backupRoot);
    }

    private static int GetRecordInt(object record, string propertyName)
    {
        var property = record.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsType<int>(property.GetValue(record));
    }

    private static string GetRecordString(object record, string propertyName)
    {
        var property = record.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property.GetValue(record)?.ToString() ?? string.Empty;
    }

    private static string FindRepositoryRoot()
    {
        var candidates = new[]
        {
            Environment.CurrentDirectory,
            AppContext.BaseDirectory,
        };

        foreach (var candidate in candidates)
        {
            foreach (var current in EnumerateDirectoryAndParents(candidate))
            {
                var manifestPath = Path.Combine(current, "src", "PowerShell", "Kestrun", "Kestrun.psd1");
                if (File.Exists(manifestPath))
                {
                    return current;
                }
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> EnumerateDirectoryAndParents(string path)
    {
        var current = new DirectoryInfo(Path.GetFullPath(path));
        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
