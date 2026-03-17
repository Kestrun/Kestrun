using System.Management.Automation.Language;
using System.Reflection;
using Kestrun.OpenApi;
using Xunit;

namespace KestrunTests.OpenApi;

[Trait("Category", "OpenApi")]
public sealed class OpenApiComponentAnnotationScannerHelpersTests
{
    [Fact]
    public void ScanFromPath_Throws_WhenMaxFilesExceededByDotSourceCycle()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kestrun-scanner-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            var aPath = Path.Combine(tempDir, "a.ps1");
            var bPath = Path.Combine(tempDir, "b.ps1");

            File.WriteAllText(aPath, ". \"$PSScriptRoot/b.ps1\"\n[OpenApiParameterComponent(In='Query')]\n[string]$a='1'\n");
            File.WriteAllText(bPath, ". \"$PSScriptRoot/a.ps1\"\n[OpenApiParameterComponent(In='Query')]\n[string]$b='2'\n");

            _ = Assert.Throws<InvalidOperationException>(() =>
                OpenApiComponentAnnotationScanner.ScanFromPath(aPath, maxFiles: 1));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ScanFromPath_RespectsAttributeTypeFilter()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kestrun-scanner-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempDir);

        try
        {
            var mainPath = Path.Combine(tempDir, "main.ps1");
            File.WriteAllText(mainPath,
                "[OpenApiParameterComponent(In='Query')]\n[string]$page='1'\n" +
                "[OpenApiResponseComponent(Description='ok', ContentType=('application/json'))]\n[object]$resp\n");

            var result = OpenApiComponentAnnotationScanner.ScanFromPath(mainPath, attributeTypeFilter: ["OpenApiResponseComponent"]);

            Assert.False(result.ContainsKey("page"));
            Assert.True(result.ContainsKey("resp"));
            _ = Assert.Single(result["resp"].Annotations);
            _ = Assert.IsType<OpenApiResponseComponentAttribute>(result["resp"].Annotations[0]);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ResolvePowerShellTypeName_HandlesNullableAndArray()
    {
        var nullableType = (Type?)InvokeStatic("ResolvePowerShellTypeName", ["Nullable[int]"]);
        var arrayType = (Type?)InvokeStatic("ResolvePowerShellTypeName", ["datetime[]"]);

        Assert.Equal(typeof(int?), nullableType);
        Assert.Equal(typeof(DateTime[]), arrayType);
    }

    [Fact]
    public void TryParseStringList_ParsesParenthesizedAndAtParenthesizedForms()
    {
        var parsedParen = (string[]?)InvokeStatic("TryParseStringList", ["('application/json','application/xml')"]);
        var parsedAtParen = (string[]?)InvokeStatic("TryParseStringList", ["@('a','b')"]);
        var parsedScalar = (string[]?)InvokeStatic("TryParseStringList", ["application/json"]);

        Assert.NotNull(parsedParen);
        Assert.NotNull(parsedAtParen);
        Assert.Equal(["application/json", "application/xml"], parsedParen);
        Assert.Equal(["a", "b"], parsedAtParen);
        Assert.Null(parsedScalar);
    }

    [Fact]
    public void TryParseBooleanString_RecognizesPowerShellAndPlainTokens()
    {
        Assert.Equal(true, (bool?)InvokeStatic("TryParseBooleanString", ["$true"]));
        Assert.Equal(false, (bool?)InvokeStatic("TryParseBooleanString", ["FALSE"]));
        Assert.Null((bool?)InvokeStatic("TryParseBooleanString", ["maybe"]));
    }

    [Fact]
    public void TryGetConstantLikeValue_ResolvesStaticMemberExpression()
    {
        var expr = ParseExpression("[int]::MaxValue");

        var value = InvokeStatic("TryGetConstantLikeValue", [expr]);

        Assert.Equal(int.MaxValue, Assert.IsType<int>(value));
    }

    private static ExpressionAst ParseExpression(string text)
    {
        var script = $"$x = {text}";
        var ast = Parser.ParseInput(script, out _, out var errors);
        Assert.True(errors.Length == 0, "Failed to parse expression test script.");

        var assignment = Assert.IsType<AssignmentStatementAst>(ast.EndBlock.Statements[0]);
        var commandExpr = Assert.IsType<CommandExpressionAst>(assignment.Right);
        return commandExpr.Expression;
    }

    private static object? InvokeStatic(string methodName, object?[] args)
    {
        var method = typeof(OpenApiComponentAnnotationScanner).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Method not found: {methodName}");

        return method.Invoke(null, args);
    }
}
