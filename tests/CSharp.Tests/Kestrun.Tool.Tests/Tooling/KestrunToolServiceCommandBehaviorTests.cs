#if NET10_0_OR_GREATER
using System.Reflection;
using System.Text;
using Xunit;

namespace Kestrun.Tool.Tests.Tooling;

public class KestrunToolServiceCommandBehaviorTests
{
    private static readonly Type ProgramType = ResolveProgramType();
    private static readonly Type ParsedCommandType = ProgramType.GetNestedType("ParsedCommand", BindingFlags.NonPublic)!;
    private static readonly Type CommandModeType = ProgramType.GetNestedType("CommandMode", BindingFlags.NonPublic)!;
    private static readonly Type ModuleStorageScopeType = ProgramType.GetNestedType("ModuleStorageScope", BindingFlags.NonPublic)!;
    private static readonly Type ServiceControlResultType = ProgramType.GetNestedType("ServiceControlResult", BindingFlags.NonPublic)!;

    [Fact]
    [Trait("Category", "Tooling")]
    public void WriteServiceControlResult_SupportsRawJsonAndTableModes()
    {
        var result = CreateServiceControlResult(
            operation: "query",
            serviceName: "demo",
            platform: "linux",
            state: "running",
            pid: 1234,
            exitCode: 7,
            message: "failed",
            rawOutput: "RAW-OUT",
            rawError: "RAW-ERR");

        var rawExitCode = Assert.IsType<int>(InvokeProgramMethod(
            "WriteServiceControlResult",
            CreateParsedCommand("ServiceQuery", serviceName: "demo", rawOutput: true),
            result));
        var jsonExitCode = Assert.IsType<int>(InvokeProgramMethod(
            "WriteServiceControlResult",
            CreateParsedCommand("ServiceQuery", serviceName: "demo", jsonOutput: true),
            result));
        var tableExitCode = Assert.IsType<int>(InvokeProgramMethod(
            "WriteServiceControlResult",
            CreateParsedCommand("ServiceQuery", serviceName: "demo"),
            result));

        Assert.Equal(7, rawExitCode);
        Assert.Equal(7, jsonExitCode);
        Assert.Equal(7, tableExitCode);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void RemoveService_WithoutName_Returns2()
    {
        var exitCode = Assert.IsType<int>(InvokeProgramMethod(
            "RemoveService",
            CreateParsedCommand("ServiceRemove", serviceName: null)));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void StartService_WithoutName_Returns2()
    {
        var exitCode = Assert.IsType<int>(InvokeProgramMethod(
            "StartService",
            CreateParsedCommand("ServiceStart", serviceName: null)));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void StopService_WithoutName_Returns2()
    {
        var exitCode = Assert.IsType<int>(InvokeProgramMethod(
            "StopService",
            CreateParsedCommand("ServiceStop", serviceName: null)));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void QueryService_WithoutName_Returns2()
    {
        var exitCode = Assert.IsType<int>(InvokeProgramMethod(
            "QueryService",
            CreateParsedCommand("ServiceQuery", serviceName: null)));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void InfoService_WithMissingNamedBundle_Returns1()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-svc-info-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempRoot);
        try
        {
            var exitCode = Assert.IsType<int>(InvokeProgramMethod(
                "InfoService",
                CreateParsedCommand("ServiceInfo", serviceName: "missing-service", serviceDeploymentRoot: tempRoot)));

            Assert.Equal(1, exitCode);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void InfoService_WithInstalledServicesAndJsonOutput_Returns0()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-svc-info-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempRoot);
        try
        {
            var serviceRoot = Path.Combine(tempRoot, "demo-service");
            var appRoot = Path.Combine(serviceRoot, "Application");
            _ = Directory.CreateDirectory(appRoot);
            File.WriteAllText(
                Path.Combine(appRoot, "Service.psd1"),
                "@{`n    FormatVersion = '1.0'`n    Name = 'demo-service'`n    Description = 'Demo Service'`n    EntryPoint = 'Service.ps1'`n    Version = '1.2.3'`n    ServiceLogPath = './logs/service.log'`n}",
                Encoding.UTF8);

            var exitCode = Assert.IsType<int>(InvokeProgramMethod(
                "InfoService",
                CreateParsedCommand("ServiceInfo", serviceName: null, serviceDeploymentRoot: tempRoot, jsonOutput: true)));

            Assert.Equal(0, exitCode);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryEnumerateInstalledServiceBundleRoots_ReturnsDistinctSortedRoots()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-svc-roots-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempRoot);
        try
        {
            CreateServiceDescriptor(tempRoot, "zeta");
            CreateServiceDescriptor(tempRoot, "alpha");
            CreateServiceDescriptor(tempRoot, "alpha-duplicate");

            var method = GetProgramMethod("TryEnumerateInstalledServiceBundleRoots");
            var args = new object?[] { tempRoot, null, null };
            var success = method.Invoke(null, args);

            Assert.NotNull(success);
            Assert.True(Assert.IsType<bool>(success));

            var roots = Assert.IsType<List<string>>(args[1]);
            Assert.NotEmpty(roots);
            Assert.Equal(3, roots.Count);
            Assert.Equal(roots.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase), roots);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryEnumerateInstalledServiceBundleRoots_WhenNoneFound_ReturnsFalseAndMessage()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-svc-roots-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempRoot);
        try
        {
            var method = GetProgramMethod("TryEnumerateInstalledServiceBundleRoots");
            var args = new object?[] { tempRoot, null, null };
            var success = method.Invoke(null, args);

            Assert.NotNull(success);
            Assert.False(Assert.IsType<bool>(success));
            Assert.Equal("No installed Kestrun services were found.", args[2]?.ToString());
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryHandleMetaCommands_HandlesKnownTopicsAndUnknownHelpTopic()
    {
        var empty = InvokeTryHandleMetaCommands([]);
        var help = InvokeTryHandleMetaCommands(["help"]);
        var helpRun = InvokeTryHandleMetaCommands(["help", "run"]);
        var runHelp = InvokeTryHandleMetaCommands(["run", "help"]);
        var version = InvokeTryHandleMetaCommands(["version"]);
        var info = InvokeTryHandleMetaCommands(["info"]);
        var unknownHelp = InvokeTryHandleMetaCommands(["help", "unknown-topic"]);
        var nonMeta = InvokeTryHandleMetaCommands(["run"]);

        Assert.Equal((true, 0), empty);
        Assert.Equal((true, 0), help);
        Assert.Equal((true, 0), helpRun);
        Assert.Equal((true, 0), runHelp);
        Assert.Equal((true, 0), version);
        Assert.Equal((true, 0), info);
        Assert.Equal((true, 2), unknownHelp);
        Assert.Equal((false, 0), nonMeta);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void HelpAndInfoPrinters_AcceptAllSupportedTopics()
    {
        _ = InvokeProgramMethod("PrintUsage");
        _ = InvokeProgramMethod("PrintHelpForTopic", "run");
        _ = InvokeProgramMethod("PrintHelpForTopic", "module");
        _ = InvokeProgramMethod("PrintHelpForTopic", "service");
        _ = InvokeProgramMethod("PrintHelpForTopic", "info");
        _ = InvokeProgramMethod("PrintHelpForTopic", "version");
        _ = InvokeProgramMethod("PrintVersion");
        _ = InvokeProgramMethod("PrintInfo");
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void WriteModuleNotFoundMessage_FormatsManifestFolderAndDefaultVariants()
    {
        var messages = new List<string>();
        _ = InvokeProgramMethod("WriteModuleNotFoundMessage", "C:/temp/Kestrun.psd1", null, new Action<string>(messages.Add));
        Assert.Contains(messages, static message => message.Contains("Unable to locate manifest file", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(messages, static message => message.Contains("No Kestrun module was found", StringComparison.OrdinalIgnoreCase));

        messages.Clear();
        _ = InvokeProgramMethod("WriteModuleNotFoundMessage", null, "C:/temp/Kestrun", new Action<string>(messages.Add));
        Assert.Contains(messages, static message => message.Contains("Unable to locate Kestrun.psd1 in folder", StringComparison.OrdinalIgnoreCase));

        messages.Clear();
        _ = InvokeProgramMethod("WriteModuleNotFoundMessage", null, null, new Action<string>(messages.Add));
        Assert.Contains(messages, static message => message.Contains("under the executable folder or PSModulePath", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void LocateModuleManifest_ResolvesExplicitFileAndFolderPaths()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-manifest-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempRoot);
        try
        {
            var explicitManifest = Path.Combine(tempRoot, "Kestrun.psd1");
            File.WriteAllText(explicitManifest, "@{}", Encoding.UTF8);

            var byFile = Assert.IsType<string>(InvokeProgramMethod("LocateModuleManifest", explicitManifest, null));
            Assert.Equal(Path.GetFullPath(explicitManifest), byFile);

            var nestedFolder = Path.Combine(tempRoot, "module");
            _ = Directory.CreateDirectory(nestedFolder);
            var folderManifest = Path.Combine(nestedFolder, "Kestrun.psd1");
            File.WriteAllText(folderManifest, "@{}", Encoding.UTF8);

            var byFolder = Assert.IsType<string>(InvokeProgramMethod("LocateModuleManifest", null, nestedFolder));
            Assert.Equal(Path.GetFullPath(folderManifest), byFolder);

            var missing = InvokeProgramMethod("LocateModuleManifest", Path.Combine(tempRoot, "missing.psd1"), null);
            Assert.Null(missing);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void LeadingOptionParsing_HandlesValidAndMissingValues()
    {
        var parseMethod = GetProgramMethod("TryParseLeadingKestrunOptions");
        var successArgs = new object?[] { new[] { "--kestrun-folder", "./module", "run" }, null, null, null, null };
        var success = parseMethod.Invoke(null, successArgs);

        Assert.True(Assert.IsType<bool>(success));
        Assert.Equal(2, Assert.IsType<int>(successArgs[1]));
        Assert.Equal("./module", successArgs[2]?.ToString());
        Assert.Null(successArgs[3]);
        Assert.Equal(string.Empty, successArgs[4]?.ToString());

        var failureArgs = new object?[] { new[] { "--kestrun-manifest" }, null, null, null, null };
        var failure = parseMethod.Invoke(null, failureArgs);

        Assert.False(Assert.IsType<bool>(failure));
        Assert.Equal("Missing value for --kestrun-manifest.", failureArgs[4]?.ToString());
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseCommandFromToken_UnknownCommand_ReturnsError()
    {
        var method = GetProgramMethod("TryParseCommandFromToken");
        var args = new object?[]
        {
            new[] { "unknown" },
            0,
            null,
            null,
            null,
            null
        };

        var success = method.Invoke(null, args);
        Assert.False(Assert.IsType<bool>(success));
        Assert.NotNull(args[4]);
        Assert.Equal("Unknown command: unknown. Use 'kestrun help' to list commands.", args[5]?.ToString());
    }

    private static void CreateServiceDescriptor(string deploymentRoot, string serviceDirectoryName)
    {
        var appRoot = Path.Combine(deploymentRoot, serviceDirectoryName, "Application");
        _ = Directory.CreateDirectory(appRoot);
        File.WriteAllText(
            Path.Combine(appRoot, "Service.psd1"),
            $"@{{`n    FormatVersion = '1.0'`n    Name = '{serviceDirectoryName}'`n    Description = 'Service {serviceDirectoryName}'`n    EntryPoint = 'Service.ps1'`n}}",
            Encoding.UTF8);
    }

    private static Type ResolveProgramType()
    {
        var assembly = Assembly.Load("Kestrun.Tool");
        var programType = assembly.GetType("Kestrun.Tool.Program", throwOnError: false);
        Assert.NotNull(programType);
        return programType;
    }

    private static MethodInfo GetProgramMethod(string methodName)
    {
        var methods = ProgramType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(methods);
        return methods[0];
    }

    private static (bool Success, int ExitCode) InvokeTryHandleMetaCommands(string[] args)
    {
        var method = GetProgramMethod("TryHandleMetaCommands");
        var values = new object?[] { args, 0 };
        var success = method.Invoke(null, values);
        Assert.NotNull(success);
        return (Assert.IsType<bool>(success), Assert.IsType<int>(values[1]));
    }

    private static object? InvokeProgramMethod(string methodName, params object?[] args)
    {
        var method = ProgramType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name, methodName, StringComparison.Ordinal)
                && candidate.GetParameters().Length == args.Length);
        Assert.NotNull(method);
        return method.Invoke(null, args);
    }

    private static object CreateParsedCommand(
        string modeName,
        string? serviceName,
        string? serviceDeploymentRoot = null,
        bool jsonOutput = false,
        bool rawOutput = false)
    {
        var mode = Enum.Parse(CommandModeType, modeName, ignoreCase: false);
        var scope = Enum.Parse(ModuleStorageScopeType, "Local", ignoreCase: false);
        var args = new object?[]
        {
            mode,
            "Service.ps1",
            false,
            Array.Empty<string>(),
            null,
            null,
            serviceName,
            !string.IsNullOrWhiteSpace(serviceName),
            null,
            null,
            null,
            null,
            scope,
            false,
            null,
            serviceDeploymentRoot,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            Array.Empty<string>(),
            false,
            false,
            jsonOutput,
            rawOutput
        };

        var parsed = Activator.CreateInstance(
            ParsedCommandType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: args,
            culture: null);
        Assert.NotNull(parsed);
        return parsed;
    }

    private static object CreateServiceControlResult(
        string operation,
        string serviceName,
        string platform,
        string state,
        int? pid,
        int exitCode,
        string message,
        string rawOutput,
        string rawError)
    {
        var value = Activator.CreateInstance(
            ServiceControlResultType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [operation, serviceName, platform, state, pid, exitCode, message, rawOutput, rawError],
            culture: null);
        Assert.NotNull(value);
        return value;
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
            // Best-effort cleanup for temporary directories.
        }
    }
}
#endif
