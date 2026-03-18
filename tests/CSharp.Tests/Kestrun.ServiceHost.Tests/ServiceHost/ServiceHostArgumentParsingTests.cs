#if NET10_0_OR_GREATER
using System.Reflection;
using Xunit;

namespace Kestrun.ServiceHost.Tests.ServiceHost;

public class ServiceHostArgumentParsingTests
{
    private static readonly Type ProgramType = ResolveProgramType();

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_WithScriptThenRun_FailsAsMutuallyExclusive()
    {
        var (success, _, error) = InvokeTryParseArguments([
            "--name", "svc",
            "--kestrun-manifest", "Kestrun.psd1",
            "--script", "foo.ps1",
            "--run", "bar.ps1"
        ]);

        Assert.False(success);
        Assert.Equal("Options --script and --run are mutually exclusive.", error);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_WithRunThenScript_FailsAsMutuallyExclusive()
    {
        var (success, _, error) = InvokeTryParseArguments([
            "--name", "svc",
            "--kestrun-manifest", "Kestrun.psd1",
            "--run", "bar.ps1",
            "--script", "foo.ps1"
        ]);

        Assert.False(success);
        Assert.Equal("Options --script and --run are mutually exclusive.", error);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_WithCommonRunPath_SucceedsAndSetsExpectedValues()
    {
        var runnerExecutablePath = Path.Combine(Path.GetTempPath(), "kestrun.exe");
        var scriptPath = Path.Combine(Path.GetTempPath(), "server.ps1");
        var moduleManifestPath = Path.Combine(Path.GetTempPath(), "Kestrun.psd1");

        var (success, parsedOptions, error) = InvokeTryParseArguments([
            "--runner-exe", runnerExecutablePath,
            "--run", scriptPath,
            "--kestrun-manifest", moduleManifestPath,
            "--discover-pshome",
            "--arguments", "--port", "5000"
        ]);

        Assert.True(success);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(parsedOptions);

        Assert.Equal("kestrun-direct-server", GetParsedOptionsString(parsedOptions, "ServiceName"));
        Assert.Equal(Path.GetFullPath(runnerExecutablePath), GetParsedOptionsString(parsedOptions, "RunnerExecutablePath"));
        Assert.Equal(Path.GetFullPath(scriptPath), GetParsedOptionsString(parsedOptions, "ScriptPath"));
        Assert.Equal(Path.GetFullPath(moduleManifestPath), GetParsedOptionsString(parsedOptions, "ModuleManifestPath"));
        Assert.True(GetParsedOptionsBool(parsedOptions, "DirectRunMode"));
        Assert.True(GetParsedOptionsBool(parsedOptions, "DiscoverPowerShellHome"));
        Assert.Equal(["--port", "5000"], GetParsedOptionsScriptArguments(parsedOptions));
    }

    [Fact]
    [Trait("Category", "ServiceHost")]
    public void TryParseArguments_WithScriptAndNoName_UsesScriptStemAsDefaultServiceName()
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "museum-api.ps1");
        var moduleManifestPath = Path.Combine(Path.GetTempPath(), "Kestrun.psd1");

        var (success, parsedOptions, error) = InvokeTryParseArguments([
            "--script", scriptPath,
            "--kestrun-manifest", moduleManifestPath,
        ]);

        Assert.True(success);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(parsedOptions);

        Assert.Equal("museum-api", GetParsedOptionsString(parsedOptions, "ServiceName"));
        Assert.False(GetParsedOptionsBool(parsedOptions, "DirectRunMode"));
    }

    [Fact]
    [Trait("Category", "ServiceHost")]
    public void TryParseArguments_WithRunAndRootPath_UsesDirectFallbackServiceName()
    {
        var rootPath = Path.GetPathRoot(Path.GetTempPath());
        Assert.False(string.IsNullOrWhiteSpace(rootPath));

        var moduleManifestPath = Path.Combine(Path.GetTempPath(), "Kestrun.psd1");

        var (success, parsedOptions, error) = InvokeTryParseArguments([
            "--run", rootPath,
            "--kestrun-manifest", moduleManifestPath,
        ]);

        Assert.True(success);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(parsedOptions);

        Assert.Equal("kestrun-direct", GetParsedOptionsString(parsedOptions, "ServiceName"));
        Assert.True(GetParsedOptionsBool(parsedOptions, "DirectRunMode"));
    }

    private static Type ResolveProgramType()
    {
        var assembly = Assembly.Load("Kestrun.ServiceHost");
        var programType = assembly.GetType("Kestrun.ServiceHost.Program", throwOnError: false);
        Assert.NotNull(programType);
        return programType;
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

    private static (bool Success, object? ParsedOptions, string Error) InvokeTryParseArguments(string[] args)
    {
        var method = GetRequiredProgramMethod("TryParseArguments");

        var values = new object?[] { args, null, null };
        var success = InvokeRequiredBool(method, values);
        var error = values[2]?.ToString() ?? string.Empty;
        return (success, values[1], error);
    }

    private static string GetParsedOptionsString(object parsedOptions, string propertyName)
    {
        var value = GetParsedOptionsProperty(parsedOptions, propertyName);
        _ = Assert.IsType<string>(value);
        return (string)value;
    }

    private static bool GetParsedOptionsBool(object parsedOptions, string propertyName)
    {
        var value = GetParsedOptionsProperty(parsedOptions, propertyName);
        _ = Assert.IsType<bool>(value);
        return (bool)value;
    }

    private static string[] GetParsedOptionsScriptArguments(object parsedOptions)
    {
        var value = GetParsedOptionsProperty(parsedOptions, "ScriptArguments");
        _ = Assert.IsType<string[]>(value);
        return (string[])value;
    }

    private static object? GetParsedOptionsProperty(object parsedOptions, string propertyName)
    {
        var property = parsedOptions.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property.GetValue(parsedOptions);
    }
}
#endif
