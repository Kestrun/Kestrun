#if NET10_0_OR_GREATER
using System.Reflection;
using Xunit;

namespace Kestrun.ServiceHost.Tests.ServiceHost;

public class ServiceHostProgramBehaviorTests
{
    private static readonly Type ProgramType = ResolveProgramType();
    private static readonly Type ParsedOptionsType = ProgramType.GetNestedType("ParsedOptions", BindingFlags.NonPublic)!;
    private static readonly Type ScriptExecutionHostType = ProgramType.GetNestedType("ScriptExecutionHost", BindingFlags.NonPublic)!;

    [Fact]
    [Trait("Category", "ServiceHost")]
    public void TryParseArguments_WithUnknownOption_Fails()
    {
        var (success, _, error) = InvokeTryParseArguments(["--wat"]);

        Assert.False(success);
        Assert.Equal("Unknown option: --wat", error);
    }

    [Fact]
    [Trait("Category", "ServiceHost")]
    public void TryParseArguments_MissingScript_Fails()
    {
        var manifestPath = Path.Combine(Path.GetTempPath(), "Kestrun.psd1");

        var (success, _, error) = InvokeTryParseArguments([
            "--name", "svc",
            "--kestrun-manifest", manifestPath,
        ]);

        Assert.False(success);
        Assert.Equal("Missing --script or --run.", error);
    }

    [Fact]
    [Trait("Category", "ServiceHost")]
    public void TryParseArguments_MissingManifest_Fails()
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "run.ps1");

        var (success, _, error) = InvokeTryParseArguments([
            "--name", "svc",
            "--script", scriptPath,
        ]);

        Assert.False(success);
        Assert.Equal("Missing --kestrun-manifest.", error);
    }

    [Fact]
    [Trait("Category", "ServiceHost")]
    public void BuildDefaultServiceNameFromScriptPath_UsesExpectedFallbacks()
    {
        var nonDirect = Assert.IsType<string>(InvokeProgramMethod("BuildDefaultServiceNameFromScriptPath", Path.GetPathRoot(Path.GetTempPath()), false));
        var direct = Assert.IsType<string>(InvokeProgramMethod("BuildDefaultServiceNameFromScriptPath", Path.GetPathRoot(Path.GetTempPath()), true));

        Assert.Equal("kestrun-service", nonDirect);
        Assert.Equal("kestrun-direct", direct);
    }

    [Fact]
    [Trait("Category", "ServiceHost")]
    public void ResolveServiceRootFromManifestPath_UsesParentOfModuleRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kestrun-service-root-{Guid.NewGuid():N}");
        try
        {
            var modulePath = Path.Combine(root, "bundle", "Modules", "Kestrun", "Kestrun.psd1");
            _ = Directory.CreateDirectory(Path.GetDirectoryName(modulePath)!);
            File.WriteAllText(modulePath, "@{}", System.Text.Encoding.UTF8);

            var resolved = Assert.IsType<string>(InvokeProgramMethod("ResolveServiceRootFromManifestPath", modulePath));
            Assert.Equal(Path.GetFullPath(Path.Combine(root, "bundle")), resolved);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    [Trait("Category", "ServiceHost")]
    public void SanitizeAndFormatHelpers_ReturnExpectedValues()
    {
        var sanitized = Assert.IsType<string>(InvokeProgramMethod("SanitizeFileNameSegment", "  svc:name  "));
        var formatted = Assert.IsType<string>(InvokeProgramMethod("FormatScriptArguments", (object)new[] { "", "--port", "5000", "hello world" }));

        Assert.Contains("svc", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain(":", sanitized, StringComparison.Ordinal);
        Assert.Equal("\"\", --port, 5000, \"hello world\"", formatted);
    }

    [Fact]
    [Trait("Category", "ServiceHost")]
    public void ConfigurePowerShellHome_RespectsDiscoverMode()
    {
        var original = Environment.GetEnvironmentVariable("PSHOME");
        var logs = new List<string>();
        try
        {
            Environment.SetEnvironmentVariable("PSHOME", "existing-home");
            _ = InvokeProgramMethod(
                "ConfigurePowerShellHome",
                true,
                Path.Combine(Path.GetTempPath(), "bundle", "Modules", "Kestrun", "Kestrun.psd1"),
                new Action<string>(logs.Add));

            Assert.Equal("existing-home", Environment.GetEnvironmentVariable("PSHOME"));
            Assert.Contains(logs, static message => message.Contains("discovery mode", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PSHOME", original);
        }
    }

    [Fact]
    [Trait("Category", "ServiceHost")]
    public void ScriptExecutionHost_StartReturns2_WhenScriptMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kestrun-service-host-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(root);
            var scriptPath = Path.Combine(root, "missing.ps1");
            var manifestPath = Path.Combine(root, "Kestrun.psd1");
            File.WriteAllText(manifestPath, "@{}", System.Text.Encoding.UTF8);
            var logPath = Path.Combine(root, "service.log");

            var host = CreateScriptExecutionHost(
                CreateParsedOptions("svc", "runner.exe", scriptPath, manifestPath, [], null, false, false),
                logPath);

            var exitCode = Assert.IsType<int>(InvokeHostMethod(host, "Start"));

            Assert.Equal(2, exitCode);
            Assert.Contains("Script file not found", File.ReadAllText(logPath, System.Text.Encoding.UTF8), StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    [Trait("Category", "ServiceHost")]
    public void ScriptExecutionHost_StartReturns2_WhenManifestMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kestrun-service-host-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(root);
            var scriptPath = Path.Combine(root, "run.ps1");
            var manifestPath = Path.Combine(root, "missing.psd1");
            File.WriteAllText(scriptPath, "Write-Output 'ok'", System.Text.Encoding.UTF8);
            var logPath = Path.Combine(root, "service.log");

            var host = CreateScriptExecutionHost(
                CreateParsedOptions("svc", "runner.exe", scriptPath, manifestPath, [], null, false, false),
                logPath);

            var exitCode = Assert.IsType<int>(InvokeHostMethod(host, "Start"));

            Assert.Equal(2, exitCode);
            Assert.Contains("manifest file not found", File.ReadAllText(logPath, System.Text.Encoding.UTF8), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    [Trait("Category", "ServiceHost")]
    public void ScriptExecutionHost_StartReturns0_WhenExecutionTaskAlreadyAssigned()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kestrun-service-host-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(root);
            var logPath = Path.Combine(root, "service.log");
            var host = CreateScriptExecutionHost(
                CreateParsedOptions("svc", "runner.exe", "script.ps1", "Kestrun.psd1", [], null, false, false),
                logPath);

            SetHostField(host, "_executionTask", Task.FromResult(0));
            var exitCode = Assert.IsType<int>(InvokeHostMethod(host, "Start"));

            Assert.Equal(0, exitCode);
            Assert.Contains("already running", File.ReadAllText(logPath, System.Text.Encoding.UTF8), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    [Trait("Category", "ServiceHost")]
    public void ScriptExecutionHost_RegisterOnExit_InvokesImmediately_WhenExitCodeKnown()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kestrun-service-host-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(root);
            var host = CreateScriptExecutionHost(
                CreateParsedOptions("svc", "runner.exe", "script.ps1", "Kestrun.psd1", [], null, false, false),
                Path.Combine(root, "service.log"));

            SetHostField(host, "_exitCode", 7);
            var observed = -1;
            _ = InvokeHostMethod(host, "RegisterOnExit", new Action<int>(code => observed = code));

            Assert.Equal(7, observed);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    [Trait("Category", "ServiceHost")]
    public void ScriptExecutionHost_StopTwice_LogsDuplicateStop()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kestrun-service-host-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(root);
            var logPath = Path.Combine(root, "service.log");
            var host = CreateScriptExecutionHost(
                CreateParsedOptions("svc", "runner.exe", "script.ps1", "Kestrun.psd1", [], null, false, false),
                logPath);

            _ = InvokeHostMethod(host, "Stop");
            _ = InvokeHostMethod(host, "Stop");

            var logText = File.ReadAllText(logPath, System.Text.Encoding.UTF8);
            Assert.Contains("Stop already requested", logText, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    [Trait("Category", "ServiceHost")]
    public void ScriptExecutionHost_StopAfterDispose_LogsDisposedMessage()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kestrun-service-host-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(root);
            var logPath = Path.Combine(root, "service.log");
            var host = CreateScriptExecutionHost(
                CreateParsedOptions("svc", "runner.exe", "script.ps1", "Kestrun.psd1", [], null, false, false),
                logPath);

            _ = InvokeHostMethod(host, "Dispose");
            _ = InvokeHostMethod(host, "Stop");

            var logText = File.ReadAllText(logPath, System.Text.Encoding.UTF8);
            Assert.Contains("after host disposal", logText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static Type ResolveProgramType()
    {
        var assembly = Assembly.Load("Kestrun.ServiceHost");
        var programType = assembly.GetType("Kestrun.ServiceHost.Program", throwOnError: false);
        Assert.NotNull(programType);
        return programType;
    }

    private static MethodInfo GetProgramMethod(string methodName)
    {
        var method = ProgramType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method;
    }

    private static MethodInfo GetProgramMethod(string methodName, object?[] arguments)
    {
        var candidates = ProgramType
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            .Where(method => method.GetParameters().Length == arguments.Length)
            .ToList();

        foreach (var candidate in candidates)
        {
            var parameters = candidate.GetParameters();
            var matched = true;
            for (var index = 0; index < parameters.Length; index++)
            {
                var argument = arguments[index];
                if (argument is null)
                {
                    if (parameters[index].ParameterType.IsValueType
                        && Nullable.GetUnderlyingType(parameters[index].ParameterType) is null)
                    {
                        matched = false;
                        break;
                    }

                    continue;
                }

                if (!parameters[index].ParameterType.IsInstanceOfType(argument))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Unable to resolve method '{methodName}' for the supplied argument list.");
    }

    private static (bool Success, object? ParsedOptions, string Error) InvokeTryParseArguments(string[] args)
    {
        var method = GetProgramMethod("TryParseArguments");
        var values = new object?[] { args, null, null };

        var result = method.Invoke(null, values);
        Assert.NotNull(result);

        return ((bool)result, values[1], values[2]?.ToString() ?? string.Empty);
    }

    private static object InvokeProgramMethod(string methodName, params object?[] arguments)
    {
        var method = GetProgramMethod(methodName, arguments);
        return method.Invoke(null, arguments)!;
    }

    private static object CreateParsedOptions(
        string serviceName,
        string runnerExecutablePath,
        string scriptPath,
        string moduleManifestPath,
        string[] scriptArguments,
        string? serviceLogPath,
        bool directRunMode,
        bool discoverPowerShellHome)
    {
        var instance = Activator.CreateInstance(
            ParsedOptionsType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [serviceName, runnerExecutablePath, scriptPath, moduleManifestPath, scriptArguments, serviceLogPath, directRunMode, discoverPowerShellHome],
            culture: null);

        Assert.NotNull(instance);
        return instance;
    }

    private static object CreateScriptExecutionHost(object parsedOptions, string bootstrapLogPath)
    {
        var host = Activator.CreateInstance(
            ScriptExecutionHostType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [parsedOptions, bootstrapLogPath],
            culture: null);

        Assert.NotNull(host);
        return host;
    }

    private static object? InvokeHostMethod(object host, string methodName, params object?[] args)
    {
        var method = ScriptExecutionHostType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method.Invoke(host, args);
    }

    private static void SetHostField(object host, string fieldName, object? value)
    {
        var field = ScriptExecutionHostType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(host, value);
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
