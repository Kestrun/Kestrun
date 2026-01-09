using Kestrun.OpenApi;
using Xunit;

namespace KestrunTests.OpenApi;

public class OpenApiComponentAnnotationScannerTests
{
    [Fact]
    [Trait("Category", "OpenAPI")]
    public void ScanFromPath_FollowsDotSourcingAndCapturesVariables()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kestrun-tests-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            var includePath = Path.Combine(tempDir, "inc.ps1");
            var mainPath = Path.Combine(tempDir, "main.ps1");

            File.WriteAllText(includePath,
                "[OpenApiParameterComponent(In = 'Header', Description = 'Correlation id')]\n" +
                "[string]$correlationId = NoDefault\n");

            File.WriteAllText(mainPath,
                ". \"$PSScriptRoot\\inc.ps1\"\n" +
                "[OpenApiParameterComponent(In = 'Query', Description = 'Page')]\n" +
                "[int]$page = 1\n");

            var result = OpenApiComponentAnnotationScanner.ScanFromPath(mainPath);

            Assert.NotNull(result);
            Assert.True(result.ContainsKey("correlationId"));
            Assert.True(result.ContainsKey("page"));

            var correlation = result["correlationId"];
            Assert.NotNull(correlation);
            Assert.True(correlation.NoDefault);
            Assert.Equal(typeof(string), correlation.VariableType);
            Assert.NotEmpty(correlation.Annotations);

            var page = result["page"];
            Assert.NotNull(page);
            Assert.False(page.NoDefault);
            Assert.Equal(typeof(int), page.VariableType);
            Assert.Equal(1, page.InitialValue);
            Assert.NotEmpty(page.Annotations);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void ScanFromPath_MarksNoDefaultWhenInitializerIsNoDefault()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kestrun-tests-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            var mainPath = Path.Combine(tempDir, "main.ps1");
            File.WriteAllText(mainPath,
                "[OpenApiParameterComponent(In = 'Query', Description = 'Category')]\n" +
                "[string]$category = NoDefault\n");

            var result = OpenApiComponentAnnotationScanner.ScanFromPath(mainPath);

            Assert.True(result.ContainsKey("category"));
            var category = result["category"];
            Assert.True(category.NoDefault);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    [Trait("Category", "OpenAPI")]
    public void ScanFromPath_ConvertsArrayArguments_ForArrayTypedAnnotationProperties()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kestrun-tests-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            var mainPath = Path.Combine(tempDir, "main.ps1");

            // PowerShell attribute array syntax (comma expression) inside parentheses.
            File.WriteAllText(mainPath,
                "[OpenApiResponseComponent(Description = 'Operation completed successfully', ContentType = ('application/json', 'application/xml'))]\n" +
                "[object]$OK\n");

            var result = OpenApiComponentAnnotationScanner.ScanFromPath(mainPath);

            Assert.True(result.TryGetValue("OK", out var ok));
            Assert.NotNull(ok);
            Assert.NotEmpty(ok.Annotations);

            var responseComponent = ok.Annotations.OfType<OpenApiResponseComponent>().Single();
            Assert.Equal(new[] { "application/json", "application/xml" }, responseComponent.ContentType);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }
    }
}
