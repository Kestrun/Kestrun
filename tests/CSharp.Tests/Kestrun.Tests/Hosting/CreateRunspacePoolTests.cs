using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using Kestrun.Hosting;
using Kestrun.Scripting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace KestrunTests.Hosting;

public class CreateRunspacePoolTests
{
    private static readonly ILogger Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Sink(new NullSink())
        .CreateLogger();

    [Fact]
    [Trait("Category", "Hosting")]
    public void Uses_Default_MaxRunspaces_When_Zero()
    {
        using var host = new KestrunHost("Tests", Logger);
        using var pool = host.CreateRunspacePool(0);

        Assert.Equal(Environment.ProcessorCount * 2, pool.MaxRunspaces);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Uses_Provided_MaxRunspaces()
    {
        using var host = new KestrunHost("Tests", Logger);
        using var pool = host.CreateRunspacePool(5);

        Assert.Equal(5, pool.MaxRunspaces);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Adds_OpenApi_StartupScript_ForPs1()
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "openapi.ps1");
        using var host = new KestrunHost("Tests", Logger);
        using var pool = host.CreateRunspacePool(1, openApiClassesPath: scriptPath);

        var iss = GetInitialSessionState(pool);
        var expected = NormalizePathForComparison(scriptPath);
        Assert.Contains(iss.StartupScripts, p => string.Equals(NormalizePathForComparison(p), expected, PathComparison));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Adds_OpenApi_Assembly_ForDll()
    {
        var dllPath = Path.Combine(Path.GetTempPath(), "openapi.dll");
        using var host = new KestrunHost("Tests", Logger);
        using var pool = host.CreateRunspacePool(1, openApiClassesPath: dllPath);

        var iss = GetInitialSessionState(pool);
        var expected = NormalizePathForComparison(dllPath);
        Assert.Contains(
            iss.Assemblies,
            a => string.Equals(NormalizePathForComparison(a.FileName), expected, PathComparison)
                 || string.Equals(NormalizePathForComparison(a.Name), expected, PathComparison)
        );
        Assert.DoesNotContain(iss.StartupScripts, p => string.Equals(NormalizePathForComparison(p), expected, PathComparison));
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string NormalizePathForComparison(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        // Normalize separators and attempt to canonicalize.
        // Path.GetFullPath() also normalizes "C:/" -> "C:\\" on Windows.
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Injects_Host_And_User_Variables()
    {
        using var host = new KestrunHost("Tests", Logger);
        var userVariables = new Dictionary<string, object>
        {
            { "CustomVar", 123 },
            { "FromPs", new PSVariable("FromPs", "value", ScopedItemOptions.None) }
        };

        using var pool = host.CreateRunspacePool(1, userVariables: userVariables);

        var iss = GetInitialSessionState(pool);
        Assert.Contains(iss.Variables, v => v.Name == "KrServer" && ReferenceEquals(v.Value, host));
        Assert.Contains(iss.Variables, v => v.Name == "CustomVar" && v.Value is int i && i == 123);
        Assert.Contains(iss.Variables, v => v.Name == "FromPs" && v.Value is string s && s == "value");
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Adds_User_Functions()
    {
        var functions = new Dictionary<string, string>
        {
            { "MyFunc", "param($x) $x + 1" }
        };

        using var host = new KestrunHost("Tests", Logger);
        using var pool = host.CreateRunspacePool(1, userFunctions: functions);

        var iss = GetInitialSessionState(pool);
        Assert.Contains(iss.Commands, c => c.Name == "MyFunc");
    }

    private static InitialSessionState GetInitialSessionState(KestrunRunspacePoolManager pool)
    {
        var field = typeof(KestrunRunspacePoolManager)
            .GetField("_iss", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);

        var iss = field.GetValue(pool) as InitialSessionState;
        return Assert.IsType<InitialSessionState>(iss);
    }

    private sealed class NullSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
        }
    }
}
