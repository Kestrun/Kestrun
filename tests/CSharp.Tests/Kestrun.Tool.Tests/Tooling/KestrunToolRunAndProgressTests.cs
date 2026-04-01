#if NET10_0_OR_GREATER
using System.Reflection;
using Xunit;

namespace Kestrun.Tool.Tests.Tooling;

public class KestrunToolRunAndProgressTests
{
    private static readonly Type ProgramType = ResolveProgramType();
    private static readonly Type ProgressBarType = ResolveProgressBarType();

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_RunWithoutScript_UsesDefaultScriptPath()
    {
        var (success, parsedCommand, error) = InvokeTryParseArguments(["run"]);

        Assert.True(success);
        Assert.True(string.IsNullOrWhiteSpace(error));
        Assert.Equal("Run", GetParsedCommandField(parsedCommand!, "Mode"));
        Assert.Equal("Service.ps1", GetParsedCommandField(parsedCommand!, "ScriptPath"));
        Assert.Equal("False", GetParsedCommandField(parsedCommand!, "ScriptPathProvided"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_RunWithPositionalScriptAndTrailingArgWithoutSeparator_Fails()
    {
        var (success, _, error) = InvokeTryParseArguments([
            "run",
            "./service.ps1",
            "trailing",
        ]);

        Assert.False(success);
        Assert.Contains("must be preceded by --arguments", error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_RunWithUnknownOption_Fails()
    {
        var (success, _, error) = InvokeTryParseArguments(["run", "--unknown"]);

        Assert.False(success);
        Assert.Equal("Unknown option: --unknown", error);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_RunWithScriptSpecifiedTwice_Fails()
    {
        var (success, _, error) = InvokeTryParseArguments([
            "run",
            "--script",
            "one.ps1",
            "--script",
            "two.ps1",
        ]);

        Assert.False(success);
        Assert.Contains("provided multiple times", error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_RunWithFolderAndManifestAliases_Succeeds()
    {
        var (success, parsedCommand, error) = InvokeTryParseArguments([
            "run",
            "-k",
            "./module",
            "-m",
            "./module/Kestrun.psd1",
            "--script",
            "./service.ps1",
            "--",
            "--port",
            "9000",
        ]);

        Assert.True(success);
        Assert.True(string.IsNullOrWhiteSpace(error));
        Assert.Equal("./module", GetParsedCommandField(parsedCommand!, "KestrunFolder"));
        Assert.Equal("./module/Kestrun.psd1", GetParsedCommandField(parsedCommand!, "KestrunManifestPath"));
        Assert.Equal(["--port", "9000"], GetParsedCommandScriptArguments(parsedCommand!));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_RunWithMissingFolderValue_Fails()
    {
        var (success, _, error) = InvokeTryParseArguments(["run", "--kestrun-folder"]);

        Assert.False(success);
        Assert.Equal("Missing value for --kestrun-folder.", error);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseArguments_RunWithMissingManifestValue_Fails()
    {
        var (success, _, error) = InvokeTryParseArguments(["run", "--kestrun-manifest"]);

        Assert.False(success);
        Assert.Equal("Missing value for --kestrun-manifest.", error);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void RunForegroundProcess_ReturnsChildExitCode()
    {
        string fileName;
        IReadOnlyList<string> arguments;

        if (OperatingSystem.IsWindows())
        {
            fileName = "cmd.exe";
            arguments = ["/c", "exit 5"];
        }
        else
        {
            fileName = "/bin/sh";
            arguments = ["-c", "exit 5"];
        }

        var exitCode = Assert.IsType<int>(InvokeProgramMethod("RunForegroundProcess", fileName, arguments));
        Assert.Equal(5, exitCode);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void ExecuteScriptViaServiceHost_WhenHostBinaryMissing_ReturnsOne()
    {
        if (TryResolveDedicatedServiceHostExecutablePath(out var discoveredHostPath))
        {
            Assert.Skip($"Dedicated service-host is discoverable on this machine: {discoveredHostPath}");
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"kestrun-tool-run-{Guid.NewGuid():N}");
        var scriptPath = Path.Combine(tempRoot, "run.ps1");
        var modulePath = Path.Combine(tempRoot, "Kestrun.psd1");

        try
        {
            _ = Directory.CreateDirectory(tempRoot);
            File.WriteAllText(scriptPath, "Write-Output 'ok'", System.Text.Encoding.UTF8);
            File.WriteAllText(modulePath, "@{}", System.Text.Encoding.UTF8);

            var result = Assert.IsType<int>(InvokeProgramMethod("ExecuteScriptViaServiceHost", scriptPath, Array.Empty<string>(), modulePath));
            Assert.Equal(1, result);
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
    public void ConsoleProgressBar_BuildBar_ProducesExpectedWidth()
    {
        var barAtZero = Assert.IsType<string>(InvokeProgressMethod("BuildBar", null, 0));
        var barAtHalf = Assert.IsType<string>(InvokeProgressMethod("BuildBar", null, 50));
        var barAtHundred = Assert.IsType<string>(InvokeProgressMethod("BuildBar", null, 100));

        Assert.Equal(30, barAtZero.Length);
        Assert.StartsWith("[", barAtZero, StringComparison.Ordinal);
        Assert.EndsWith("]", barAtZero, StringComparison.Ordinal);
        Assert.Contains('#', barAtHalf);
        Assert.DoesNotContain('-', barAtHundred);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void ConsoleProgressBar_GetPercent_HandlesKnownAndUnknownTotals()
    {
        var withTotal = CreateProgressBar("Download", 100, null);
        var withoutTotal = CreateProgressBar("Download", null, null);

        var fifty = Assert.IsType<int>(InvokeProgressMethod("GetPercent", withTotal, 50L));
        var clamped = Assert.IsType<int>(InvokeProgressMethod("GetPercent", withTotal, 500L));
        var unknown = Assert.IsType<int>(InvokeProgressMethod("GetPercent", withoutTotal, 1L));

        Assert.Equal(50, fifty);
        Assert.Equal(100, clamped);
        Assert.Equal(-1, unknown);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void ConsoleProgressBar_Render_UpdatesInternalState()
    {
        var bar = CreateProgressBar("Install", 100, (value, total) => $"{value}/{total}");

        _ = InvokeProgressMethod("Render", bar, 10L, 10);

        var hasRendered = Assert.IsType<bool>(GetProgressField(bar, "_hasRendered"));
        var lastLength = Assert.IsType<int>(GetProgressField(bar, "_lastRenderedLength"));

        Assert.True(hasRendered);
        Assert.True(lastLength > 0);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void ConsoleProgressBar_BuildRenderedLine_TrimsToConsoleWidth()
    {
        var bar = CreateProgressBar("Bundling service runtime modules", 116, (value, total) => $"{value}/{total} files");

        var rendered = Assert.IsType<string>(InvokeProgressMethod("BuildRenderedLine", bar, "116/116 files", 100, 79));

        Assert.True(rendered.Length <= 79);
        Assert.Contains("100%", rendered, StringComparison.Ordinal);
        Assert.StartsWith("Bundling service runtime modules", rendered, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void ConsoleProgressBar_ReportCompleteAndDispose_DoNotThrowWhenOutputRedirected()
    {
        var bar = CreateProgressBar("Install", 10, null);

        _ = bar.GetType().GetMethod("Report", BindingFlags.Instance | BindingFlags.Public)!.Invoke(bar, [5L, false]);
        _ = bar.GetType().GetMethod("Complete", BindingFlags.Instance | BindingFlags.Public)!.Invoke(bar, [10L]);
        _ = bar.GetType().GetMethod("Dispose", BindingFlags.Instance | BindingFlags.Public)!.Invoke(bar, null);
    }

    private static Type ResolveProgramType()
    {
        var assembly = Assembly.Load("Kestrun.Tool");
        var programType = assembly.GetType("Kestrun.Tool.Program", throwOnError: false);
        Assert.NotNull(programType);
        return programType;
    }

    private static Type ResolveProgressBarType()
    {
        var assembly = Assembly.Load("Kestrun.Tool");
        var progressType = assembly.GetType("Kestrun.Tool.ConsoleProgressBar", throwOnError: false);
        Assert.NotNull(progressType);
        return progressType;
    }

    private static MethodInfo GetProgramMethod(string methodName)
    {
        var method = ProgramType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return method;
    }

    private static object InvokeProgramMethod(string methodName, params object?[] args)
    {
        var method = GetProgramMethod(methodName);
        var result = method.Invoke(null, args);
        Assert.NotNull(result);
        return result;
    }

    private static (bool Success, object? ParsedCommand, string Error) InvokeTryParseArguments(string[] args)
    {
        var method = GetProgramMethod("TryParseArguments");
        var values = new object?[] { args, null, null };
        var success = method.Invoke(null, values);

        Assert.NotNull(success);
        _ = Assert.IsType<bool>(success);

        return ((bool)success, values[1], values[2]?.ToString() ?? string.Empty);
    }

    private static bool TryResolveDedicatedServiceHostExecutablePath(out string executablePath)
    {
        var method = GetProgramMethod("TryResolveDedicatedServiceHostExecutableFromToolDistribution");
        var values = new object?[] { null };
        var result = method.Invoke(null, values);

        Assert.NotNull(result);
        _ = Assert.IsType<bool>(result);

        executablePath = values[0]?.ToString() ?? string.Empty;
        return (bool)result;
    }

    private static string GetParsedCommandField(object parsedCommand, string fieldName)
    {
        var property = parsedCommand.GetType().GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property.GetValue(parsedCommand)?.ToString() ?? string.Empty;
    }

    private static string[] GetParsedCommandScriptArguments(object parsedCommand)
    {
        var property = parsedCommand.GetType().GetProperty("ScriptArguments", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsType<string[]>(property.GetValue(parsedCommand));
    }

    private static object CreateProgressBar(string label, long? total, Func<long, long?, string>? formatter)
    {
        var instance = Activator.CreateInstance(
            ProgressBarType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [label, total, formatter],
            culture: null);

        Assert.NotNull(instance);
        return instance;
    }

    private static object? InvokeProgressMethod(string methodName, object? instance, params object?[] args)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Public | (instance is null ? BindingFlags.Static : BindingFlags.Instance);
        var method = ProgressBarType.GetMethod(methodName, flags);
        Assert.NotNull(method);
        return method.Invoke(instance, args);
    }

    private static object? GetProgressField(object instance, string fieldName)
    {
        var field = ProgressBarType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field.GetValue(instance);
    }
}
#endif
