#if NET10_0_OR_GREATER
using System.Management.Automation;
using System.Reflection;
using Xunit;

namespace Kestrun.Runner.Tests.Runtime;

public class RunnerRuntimeAdditionalTests
{
    private static readonly Type RunnerType = typeof(RunnerRuntime);

    [Fact]
    public void EnsureNet10Runtime_DoesNotThrowOnCurrentRuntime() => RunnerRuntime.EnsureNet10Runtime("kestrun-tests");

    [Fact]
    public void IsKestrunAssemblyVersionCompatible_EvaluatesExpectedBranches()
    {
        Assert.False(InvokePrivateBool("IsKestrunAssemblyVersionCompatible", null, new Version(1, 0, 0)));
        Assert.False(InvokePrivateBool("IsKestrunAssemblyVersionCompatible", new Version(1, 0, 0), null));
        Assert.False(InvokePrivateBool("IsKestrunAssemblyVersionCompatible", new Version(2, 0, 0), new Version(1, 9, 9)));
        Assert.False(InvokePrivateBool("IsKestrunAssemblyVersionCompatible", new Version(1, 0, 0), new Version(1, 0, 1)));
        Assert.True(InvokePrivateBool("IsKestrunAssemblyVersionCompatible", new Version(1, 0, 2), new Version(1, 0, 1)));
    }

    [Fact]
    public void FormatVersionForDiagnostics_ReturnsUnknownWhenVersionMissing()
    {
        var formatted = Assert.IsType<string>(InvokePrivate("FormatVersionForDiagnostics", (object?)null));
        Assert.Equal("unknown", formatted);
    }

    [Fact]
    public void HasPowerShellManagementModule_RecognizesCompatibleManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kestrun-runner-pshome-{Guid.NewGuid():N}");
        try
        {
            var moduleDirectory = Path.Combine(root, "Modules", "Microsoft.PowerShell.Management");
            _ = Directory.CreateDirectory(moduleDirectory);
            var manifestPath = Path.Combine(moduleDirectory, "Microsoft.PowerShell.Management.psd1");
            File.WriteAllText(manifestPath, "@{ CompatiblePSEditions = @('Core') }", System.Text.Encoding.UTF8);

            Assert.True(InvokePrivateBool("HasPowerShellManagementModule", root));
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void HasPowerShellManagementModule_ReturnsFalseWhenManifestDoesNotDeclareCore()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kestrun-runner-pshome-{Guid.NewGuid():N}");
        try
        {
            var moduleDirectory = Path.Combine(root, "Modules", "Microsoft.PowerShell.Management");
            _ = Directory.CreateDirectory(moduleDirectory);
            var manifestPath = Path.Combine(moduleDirectory, "Microsoft.PowerShell.Management.psd1");
            File.WriteAllText(manifestPath, "@{ CompatiblePSEditions = @('Desktop') }", System.Text.Encoding.UTF8);

            Assert.False(InvokePrivateBool("HasPowerShellManagementModule", root));
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void EnsurePsModulePathContains_AddsPathOnlyOnce()
    {
        var modulesPath = Path.Combine(Path.GetTempPath(), $"kestrun-runner-modules-{Guid.NewGuid():N}");
        var original = Environment.GetEnvironmentVariable("PSModulePath");
        try
        {
            _ = Directory.CreateDirectory(modulesPath);
            Environment.SetEnvironmentVariable("PSModulePath", string.Empty);

            _ = InvokePrivate("EnsurePsModulePathContains", modulesPath);
            var first = Environment.GetEnvironmentVariable("PSModulePath");
            Assert.Equal(modulesPath, first);

            _ = InvokePrivate("EnsurePsModulePathContains", modulesPath);
            var second = Environment.GetEnvironmentVariable("PSModulePath");
            Assert.Equal(modulesPath, second);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PSModulePath", original);
            TryDeleteDirectory(modulesPath);
        }
    }

    [Fact]
    public void ResolveBootstrapLogPath_UsesConfiguredDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kestrun-runner-logs-{Guid.NewGuid():N}");
        try
        {
            _ = Directory.CreateDirectory(root);
            var resolved = RunnerRuntime.ResolveBootstrapLogPath(root, "runner.log");
            Assert.Equal(Path.Combine(Path.GetFullPath(root), "runner.log"), resolved);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void ResolveBootstrapLogPath_UsesConfiguredFilePath()
    {
        var configuredFile = Path.Combine(Path.GetTempPath(), $"kestrun-runner-log-{Guid.NewGuid():N}.log");
        var resolved = RunnerRuntime.ResolveBootstrapLogPath(configuredFile, "ignored.log");
        Assert.Equal(Path.GetFullPath(configuredFile), resolved);
    }

    [Fact]
    public void DispatchPowerShellOutput_SkipsWhitespaceAndNullEntries()
    {
        static IEnumerable<PSObject> BuildOutput()
        {
            yield return null!;
            yield return new PSObject("   ");
            yield return new PSObject("ok");
        }

        var captured = new List<string>();
        RunnerRuntime.DispatchPowerShellOutput(BuildOutput(), captured.Add, skipWhitespace: true);

        Assert.Equal(["ok"], captured);
    }

    [Fact]
    public void DispatchPowerShellStreams_InvokesCallbacks()
    {
        using var ps = PowerShell.Create();
        var streams = ps.Streams;

        streams.Warning.Add(new WarningRecord("warn message"));
        streams.Verbose.Add(new VerboseRecord("verbose message"));
        streams.Debug.Add(new DebugRecord("debug message"));
        streams.Information.Add(new InformationRecord("info message", "test"));
        streams.Error.Add(new ErrorRecord(new InvalidOperationException("boom"), "ERR", ErrorCategory.NotSpecified, null));

        var warnings = new List<string>();
        var verbose = new List<string>();
        var debug = new List<string>();
        var info = new List<string>();
        var errors = new List<string>();

        RunnerRuntime.DispatchPowerShellStreams(
            streams,
            warnings.Add,
            verbose.Add,
            debug.Add,
            info.Add,
            errors.Add,
            skipWhitespace: true);

        Assert.Contains("warn message", warnings);
        Assert.Contains("verbose message", verbose);
        Assert.Contains("debug message", debug);
        Assert.Contains("info message", info);
        Assert.Contains(errors, static value => value.Contains("boom", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NormalizePowerShellHomeCandidate_ReturnsExpectedForBasicInputs()
    {
        var empty = Assert.IsType<string>(InvokePrivate("NormalizePowerShellHomeCandidate", "   "));
        var directory = Assert.IsType<string>(InvokePrivate("NormalizePowerShellHomeCandidate", Path.GetTempPath()));

        Assert.Equal(string.Empty, empty);
        Assert.Equal(Path.GetFullPath(Path.GetTempPath()), directory);
    }

    [Fact]
    public void IsPowerShellExecutablePath_RecognizesPwshNames()
    {
        Assert.True(InvokePrivateBool("IsPowerShellExecutablePath", "/usr/bin/pwsh"));
        Assert.True(InvokePrivateBool("IsPowerShellExecutablePath", "C:\\Program Files\\PowerShell\\7\\pwsh.exe"));
        Assert.False(InvokePrivateBool("IsPowerShellExecutablePath", "/usr/bin/bash"));
    }

    [Fact]
    public void TryResolveFinalPath_ReturnsNullForRegularFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kestrun-runner-file-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, "hello", System.Text.Encoding.UTF8);
            var resolved = InvokePrivate("TryResolveFinalPath", path);
            Assert.Null(resolved);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void TryEnsureDirectory_DoesNotThrowForWhitespaceOrValidPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kestrun-runner-dir-{Guid.NewGuid():N}");
        try
        {
            _ = InvokePrivate("TryEnsureDirectory", "   ");
            _ = InvokePrivate("TryEnsureDirectory", root);
            Assert.True(Directory.Exists(root));
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void RunProcessCapture_ReturnsCapturedOutputForSimpleCommand()
    {
        string fileName;
        string[] args;
        if (OperatingSystem.IsWindows())
        {
            fileName = "cmd.exe";
            args = ["/c", "echo hello"];
        }
        else
        {
            fileName = "/bin/sh";
            args = ["-c", "printf 'hello'\n"];
        }

        var result = InvokePrivate("RunProcessCapture", fileName, args);
        Assert.NotNull(result);

        var exitCode = Assert.IsType<int>(GetProperty(result, "ExitCode"));
        var output = Assert.IsType<string>(GetProperty(result, "Output"));

        Assert.Equal(0, exitCode);
        Assert.Contains("hello", output, StringComparison.Ordinal);
    }

    private static object? GetProperty(object instance, string name)
    {
        var property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property.GetValue(instance);
    }

    private static object? InvokePrivate(string methodName, params object?[] args)
    {
        var method = RunnerType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method.Invoke(null, args);
    }

    private static bool InvokePrivateBool(string methodName, params object?[] args)
    {
        var result = InvokePrivate(methodName, args);
        Assert.NotNull(result);
        _ = Assert.IsType<bool>(result);
        return (bool)result;
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
