using Kestrun.Scripting;
using Xunit;

namespace KestrunTests.Scripting;

[Trait("Category", "Scripting")]
public class ScriptLanguageTests
{
    [Fact]
    public void Native_HasValueZero() =>
        // Assert
        Assert.Equal(0, (int)ScriptLanguage.Native);

    [Fact]
    public void AllLanguages_HaveUniqueValues()
    {
        // Arrange
        var values = new[]
        {
            ScriptLanguage.Native,
            ScriptLanguage.PowerShell,
            ScriptLanguage.CSharp,
            ScriptLanguage.FSharp,
            ScriptLanguage.Python,
            ScriptLanguage.JavaScript,
            ScriptLanguage.VBNet
        };

        // Act
        var distinctValues = values.Distinct().ToArray();

        // Assert
        Assert.Equal(values.Length, distinctValues.Length);
    }

    [Theory]
    [InlineData("Native", ScriptLanguage.Native)]
    [InlineData("PowerShell", ScriptLanguage.PowerShell)]
    [InlineData("CSharp", ScriptLanguage.CSharp)]
    [InlineData("FSharp", ScriptLanguage.FSharp)]
    [InlineData("Python", ScriptLanguage.Python)]
    [InlineData("JavaScript", ScriptLanguage.JavaScript)]
    [InlineData("VBNet", ScriptLanguage.VBNet)]
    public void Parse_ValidNames_ReturnsCorrectValue(string name, ScriptLanguage expected)
    {
        // Act
        var result = Enum.Parse<ScriptLanguage>(name);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("native", ScriptLanguage.Native)]
    [InlineData("powershell", ScriptLanguage.PowerShell)]
    [InlineData("csharp", ScriptLanguage.CSharp)]
    public void Parse_CaseInsensitive_ReturnsCorrectValue(string name, ScriptLanguage expected)
    {
        // Act
        var result = Enum.Parse<ScriptLanguage>(name, ignoreCase: true);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(ScriptLanguage.Native, "Native")]
    [InlineData(ScriptLanguage.PowerShell, "PowerShell")]
    [InlineData(ScriptLanguage.CSharp, "CSharp")]
    [InlineData(ScriptLanguage.FSharp, "FSharp")]
    [InlineData(ScriptLanguage.Python, "Python")]
    [InlineData(ScriptLanguage.JavaScript, "JavaScript")]
    [InlineData(ScriptLanguage.VBNet, "VBNet")]
    public void ToString_ReturnsCorrectName(ScriptLanguage language, string expected)
    {
        // Act
        var result = language.ToString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(ScriptLanguage.Native)]
    [InlineData(ScriptLanguage.PowerShell)]
    [InlineData(ScriptLanguage.CSharp)]
    [InlineData(ScriptLanguage.FSharp)]
    [InlineData(ScriptLanguage.Python)]
    [InlineData(ScriptLanguage.JavaScript)]
    [InlineData(ScriptLanguage.VBNet)]
    public void IsDefined_AllLanguages_ReturnsTrue(ScriptLanguage language)
    {
        // Act
        var result = Enum.IsDefined(typeof(ScriptLanguage), language);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDefined_InvalidValue_ReturnsFalse()
    {
        // Arrange
        var invalidValue = (ScriptLanguage)999;

        // Act
        var result = Enum.IsDefined(typeof(ScriptLanguage), invalidValue);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Default_IsNative()
    {
        // Arrange
        ScriptLanguage defaultValue = default;

        // Assert
        Assert.Equal(ScriptLanguage.Native, defaultValue);
    }

    [Fact]
    public void Comparison_Works()
    {
        // Assert
        Assert.True(ScriptLanguage.Native < ScriptLanguage.PowerShell);
        Assert.True(ScriptLanguage.PowerShell < ScriptLanguage.CSharp);
        Assert.False(ScriptLanguage.CSharp < ScriptLanguage.PowerShell);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var lang1 = ScriptLanguage.PowerShell;
        var lang2 = ScriptLanguage.PowerShell;

        // Assert
        Assert.Equal(lang1, lang2);
        Assert.True(lang1 == lang2);
        Assert.False(lang1 != lang2);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var lang1 = ScriptLanguage.PowerShell;
        var lang2 = ScriptLanguage.CSharp;

        // Assert
        Assert.NotEqual(lang1, lang2);
        Assert.False(lang1 == lang2);
        Assert.True(lang1 != lang2);
    }

    [Fact]
    public void GetValues_ReturnsAllLanguages()
    {
        // Act
        var values = Enum.GetValues<ScriptLanguage>();

        // Assert
        Assert.Equal(7, values.Length);
        Assert.Contains(ScriptLanguage.Native, values);
        Assert.Contains(ScriptLanguage.PowerShell, values);
        Assert.Contains(ScriptLanguage.CSharp, values);
        Assert.Contains(ScriptLanguage.FSharp, values);
        Assert.Contains(ScriptLanguage.Python, values);
        Assert.Contains(ScriptLanguage.JavaScript, values);
        Assert.Contains(ScriptLanguage.VBNet, values);
    }

    [Fact]
    public void GetNames_ReturnsAllLanguageNames()
    {
        // Act
        var names = Enum.GetNames<ScriptLanguage>();

        // Assert
        Assert.Equal(7, names.Length);
        Assert.Contains("Native", names);
        Assert.Contains("PowerShell", names);
        Assert.Contains("CSharp", names);
        Assert.Contains("FSharp", names);
        Assert.Contains("Python", names);
        Assert.Contains("JavaScript", names);
        Assert.Contains("VBNet", names);
    }

    [Fact]
    public void TryParse_ValidName_ReturnsTrue()
    {
        // Act
        var success = Enum.TryParse<ScriptLanguage>("PowerShell", out var result);

        // Assert
        Assert.True(success);
        Assert.Equal(ScriptLanguage.PowerShell, result);
    }

    [Fact]
    public void TryParse_InvalidName_ReturnsFalse()
    {
        // Act
        var success = Enum.TryParse<ScriptLanguage>("InvalidLanguage", out var result);

        // Assert
        Assert.False(success);
        Assert.Equal(default, result);
    }

    [Fact]
    public void Switch_AllLanguages_CanBeMatched()
    {
        // Arrange
        var matchedLanguages = new List<ScriptLanguage>();

        // Act
        foreach (var language in Enum.GetValues<ScriptLanguage>())
        {
            switch (language)
            {
                case ScriptLanguage.Native:
                case ScriptLanguage.PowerShell:
                case ScriptLanguage.CSharp:
                case ScriptLanguage.FSharp:
                case ScriptLanguage.Python:
                case ScriptLanguage.JavaScript:
                case ScriptLanguage.VBNet:
                    matchedLanguages.Add(language);
                    break;
            }
        }

        // Assert
        Assert.Equal(7, matchedLanguages.Count);
    }
}
