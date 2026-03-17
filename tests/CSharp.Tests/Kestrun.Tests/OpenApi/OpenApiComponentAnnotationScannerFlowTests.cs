using Kestrun.OpenApi;
using System.Management.Automation;
using Xunit;

namespace KestrunTests.OpenApi;

[Trait("Category", "OpenApi")]
public sealed class OpenApiComponentAnnotationScannerFlowTests
{
    [Fact]
    public void ScanFromPath_ExtractsInlineAndStandaloneAnnotations_WithTypesAndInitializers()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "main.ps1");
            File.WriteAllText(path,
                "[OpenApiParameterComponent(In='Query')]\n" +
                "[int]$page = 1\n" +
                "[OpenApiResponseComponent(Description='ok', ContentType=('application/json'))]\n" +
                "[string]$status\n" +
                "[OpenApiRequestBodyComponent(ContentType=('application/json'))]\n" +
                "$payload = NoDefault\n");

            var result = OpenApiComponentAnnotationScanner.ScanFromPath(path);

            Assert.True(result.ContainsKey("page"));
            Assert.True(result.ContainsKey("status"));
            Assert.True(result.ContainsKey("payload"));

            Assert.Equal(typeof(int), result["page"].VariableType);
            Assert.Equal("int", result["page"].VariableTypeName);
            Assert.Equal(1, Assert.IsType<int>(result["page"].InitialValue));

            Assert.Equal(typeof(string), result["status"].VariableType);
            Assert.False(result["status"].NoDefault);
            Assert.True(result["status"].Annotations.Count > 0);

            Assert.True(result["payload"].NoDefault);
            Assert.True(result["payload"].Annotations.Count > 0);
        }
        finally
        {
            SafeDelete(dir);
        }
    }

    [Fact]
    public void ScanFromPath_FollowsDotSourcedFiles_AndResolvesPSScriptRoot()
    {
        var dir = CreateTempDir();
        try
        {
            var mainPath = Path.Combine(dir, "main.ps1");
            var incPath = Path.Combine(dir, "inc.ps1");

            File.WriteAllText(mainPath,
                ". \"$PSScriptRoot/inc.ps1\"\n" +
                "[OpenApiParameterComponent(In='Query')]\n" +
                "[string]$q = 'x'\n");

            File.WriteAllText(incPath,
                "[OpenApiParameterComponent(In='Path')]\n" +
                "[int]$id = 42\n");

            var result = OpenApiComponentAnnotationScanner.ScanFromPath(mainPath);

            Assert.True(result.ContainsKey("id"));
            Assert.True(result.ContainsKey("q"));
            Assert.Equal(42, Assert.IsType<int>(result["id"].InitialValue));
            Assert.Equal("x", Assert.IsType<string>(result["q"].InitialValue));
        }
        finally
        {
            SafeDelete(dir);
        }
    }

    [Fact]
    public void ScanFromPath_StrictModeClearsPendingAnnotations_OnInterveningStatements()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "main.ps1");
            File.WriteAllText(path,
                "[OpenApiParameterComponent(In='Query')]\n" +
                "[string]$name = 'seed'\n" +
                "[OpenApiResponseComponent(Description='barrier-test', ContentType=('application/json'))]\n" +
                "Write-Host 'barrier'\n" +
                "$name = 'n'\n");

            var result = OpenApiComponentAnnotationScanner.ScanFromPath(path);

            Assert.True(result.ContainsKey("name"));
            _ = Assert.Single(result["name"].Annotations);
        }
        finally
        {
            SafeDelete(dir);
        }
    }

    [Fact]
    public void ScanFromRunningScriptOrPath_UsesProvidedMainPath()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "main.ps1");
            File.WriteAllText(path,
                "[OpenApiParameterComponent(In='Query')]\n" +
                "[Nullable[int]]$count = 5\n");

            using var ps = PowerShell.Create();
            var engine = Assert.IsType<EngineIntrinsics>(ps.Runspace.SessionStateProxy.GetVariable("ExecutionContext"));

            var result = OpenApiComponentAnnotationScanner.ScanFromRunningScriptOrPath(engine, mainPath: path);

            Assert.True(result.ContainsKey("count"));
            Assert.Equal(typeof(int?), result["count"].VariableType);
            Assert.Equal(5, Assert.IsType<int>(result["count"].InitialValue));
        }
        finally
        {
            SafeDelete(dir);
        }
    }

    [Fact]
    public void ScanFromRunningScriptOrPath_ThrowsWhenNoEntryPathIsAvailable()
    {
        using var ps = PowerShell.Create();
        _ = ps.AddScript("$PSCommandPath = $null");
        _ = ps.Invoke();
        var engine = Assert.IsType<EngineIntrinsics>(ps.Runspace.SessionStateProxy.GetVariable("ExecutionContext"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            OpenApiComponentAnnotationScanner.ScanFromRunningScriptOrPath(engine, mainPath: null));

        Assert.Contains("No running script path", ex.Message);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kestrun-scanner-flow-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(dir);
        return dir;
    }

    private static void SafeDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
            // best-effort temp cleanup
        }
    }
}
