using Kestrun.Hosting.Options;
using Kestrun.Scripting;
using Xunit;

namespace KestrunTests.Hosting.Options;

public class LanguageOptionsTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void DefaultConstructor_InitializesProperties()
    {
        var options = new LanguageOptions();

        Assert.Null(options.Code);
        Assert.Equal(ScriptLanguage.PowerShell, options.Language);
        Assert.Null(options.ExtraImports);
        Assert.Null(options.ExtraRefs);
        Assert.NotNull(options.Arguments);
        Assert.Empty(options.Arguments);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Code_CanBeSet()
    {
        var options = new LanguageOptions { Code = "Write-Output 'Hello'" };

        Assert.Equal("Write-Output 'Hello'", options.Code);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Language_CanBeSet()
    {
        var options = new LanguageOptions { Language = ScriptLanguage.CSharp };

        Assert.Equal(ScriptLanguage.CSharp, options.Language);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ExtraImports_CanBeSet()
    {
        var options = new LanguageOptions
        {
            ExtraImports = ["System.Linq", "System.Collections"]
        };

        Assert.NotNull(options.ExtraImports);
        Assert.Equal(2, options.ExtraImports.Length);
        Assert.Contains("System.Linq", options.ExtraImports);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ExtraRefs_CanBeSet()
    {
        var assembly = typeof(LanguageOptions).Assembly;
        var options = new LanguageOptions { ExtraRefs = [assembly] };

        Assert.NotNull(options.ExtraRefs);
        _ = Assert.Single(options.ExtraRefs);
        Assert.Same(assembly, options.ExtraRefs[0]);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ScriptBlock_NullCode_ReturnsNull()
    {
        var options = new LanguageOptions { Code = null };

        Assert.Null(options.ScriptBlock);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ScriptBlock_WhitespaceCode_ReturnsNull()
    {
        var options = new LanguageOptions { Code = "   " };

        Assert.Null(options.ScriptBlock);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ScriptBlock_ValidCode_ReturnsScriptBlock()
    {
        var options = new LanguageOptions { Code = "Write-Output 'Test'" };

        Assert.NotNull(options.ScriptBlock);
        Assert.Contains("Write-Output", options.ScriptBlock.ToString());
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ScriptBlock_SetToNull_ClearsCode()
    {
        var options = new LanguageOptions
        {
            Code = "Write-Output 'Test'",
            ScriptBlock = null
        };

        Assert.Null(options.Code);
        Assert.Null(options.ScriptBlock);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ScriptBlock_SetValue_UpdatesCodeAndLanguage()
    {
        var scriptBlock = System.Management.Automation.ScriptBlock.Create("param($x) $x * 2");
        var options = new LanguageOptions { ScriptBlock = scriptBlock };

        Assert.NotNull(options.Code);
        Assert.Contains("$x * 2", options.Code);
        Assert.Equal(ScriptLanguage.PowerShell, options.Language);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Arguments_CaseInsensitive()
    {
        var options = new LanguageOptions();
        options.Arguments!["Key"] = "value1";

        Assert.Equal("value1", options.Arguments["key"]);
        Assert.Equal("value1", options.Arguments["KEY"]);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Code_RoundTrip_Works()
    {
        var options = new LanguageOptions { Code = "Test" };

        Assert.Equal("Test", options.Code);
    }
}
