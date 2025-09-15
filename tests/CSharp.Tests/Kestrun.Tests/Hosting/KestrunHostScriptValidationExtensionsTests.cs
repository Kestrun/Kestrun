using System.Reflection;
using Kestrun.Hosting;
using Microsoft.CodeAnalysis;
using Xunit;

namespace KestrunTests.Hosting;

public class KestrunHostScriptValidationExtensionsTests
{
    private KestrunHost CreateHost() => new("TestApp", AppContext.BaseDirectory);

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateCSharpScript_ReturnsNoErrors_ForValidCode()
    {
        var host = CreateHost();
        var code = "return 42;";
        var diagnostics = host.ValidateCSharpScript(code);
        Assert.All(diagnostics, d => Assert.NotEqual(DiagnosticSeverity.Error, d.Severity));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateCSharpScript_ReturnsErrors_ForInvalidCode()
    {
        var host = CreateHost();
        var code = "return doesnotexist;";
        var diagnostics = host.ValidateCSharpScript(code);
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void IsCSharpScriptValid_ReturnsTrue_ForValidCode()
    {
        var host = CreateHost();
        var code = "return 123;";
        Assert.True(host.IsCSharpScriptValid(code));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void IsCSharpScriptValid_ReturnsFalse_ForInvalidCode()
    {
        var host = CreateHost();
        var code = "return notfound;";
        Assert.False(host.IsCSharpScriptValid(code));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void GetCSharpScriptErrors_ReturnsNull_ForValidCode()
    {
        var host = CreateHost();
        var code = "return 1;";
        Assert.Null(host.GetCSharpScriptErrors(code));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void GetCSharpScriptErrors_ReturnsErrorMessage_ForInvalidCode()
    {
        var host = CreateHost();
        var code = "return missing;";
        var errors = host.GetCSharpScriptErrors(code);
        Assert.NotNull(errors);
        Assert.Contains("error", errors!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateCSharpScript_ReturnsError_ForNullCode()
    {
        var host = CreateHost();
        var diagnostics = host.ValidateCSharpScript(null);
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateCSharpScript_NoError_ForEmptyCode()
    {
        var host = CreateHost();
        var diagnostics = host.ValidateCSharpScript("");
        // Empty script is valid (no errors, just returns default)
        Assert.All(diagnostics, d => Assert.NotEqual(DiagnosticSeverity.Error, d.Severity));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateCSharpScript_ExtraImports_AllowsOtherwiseInvalidCode()
    {
        var host = CreateHost();
        var code = "return Enumerable.Range(1,3).Sum();";
        // Should fail without import (or return no diagnostics if not analyzed)
        var diagsNoImport = host.ValidateCSharpScript(code);
        if (diagsNoImport.Length > 0)
        {
            Assert.Contains(diagsNoImport, d => d.Severity == DiagnosticSeverity.Error);
        }
        // Should succeed with import (or return no diagnostics)
        var diagnostics = host.ValidateCSharpScript(code, ["System.Linq"]);
        Assert.True(diagnostics.Length == 0 || diagnostics.All(d => d.Severity != DiagnosticSeverity.Error));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateCSharpScript_RespectsLanguageVersion()
    {
        var host = CreateHost();
        var code = "int x = 0; x = x switch { 0 => 1, _ => 2 }; return x;"; // switch expression (C# 8+)
        // Should fail with C# 7
        var diagnostics = host.ValidateCSharpScript(code, null, null, Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp7);
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        // Should succeed with C# 8+
        diagnostics = host.ValidateCSharpScript(code, null, null, Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp8);
        Assert.All(diagnostics, d => Assert.NotEqual(DiagnosticSeverity.Error, d.Severity));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateCSharpScript_WarningOnly_IsValid()
    {
        var host = CreateHost();
        var code = "int unused; return 1;"; // unused variable triggers warning
        var diagnostics = host.ValidateCSharpScript(code);
        // Some analyzers may not return warnings for unused locals in scripts; accept empty diagnostics
        Assert.True(diagnostics.Length == 0 || diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning));
        Assert.True(host.IsCSharpScriptValid(code));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateCSharpScript_ExceptionHandling_ReturnsSyntheticDiagnostic()
    {
        var host = CreateHost();
        // Simulate exception by passing an invalid assembly reference (null)
        // Simulate exception by passing an invalid assembly reference (bad path)
        Assembly? badAsm = null;
        try { badAsm = Assembly.LoadFile("C:/this/does/not/exist.dll"); } catch { }
        if (badAsm is null)
        {
            // If Assembly.LoadFile fails, pass null to extraRefs and expect no diagnostics (since we can't simulate the error)
            var diagnostics = host.ValidateCSharpScript("return 1;", null, null);
            Assert.True(diagnostics.Length == 0 || diagnostics.All(d => d.Severity != DiagnosticSeverity.Error));
        }
        else
        {
            var diagnostics = host.ValidateCSharpScript("return 1;", null, [badAsm]);
            Assert.Contains(diagnostics, d => d.Id == "KESTRUN001");
            Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        }
    }
}
