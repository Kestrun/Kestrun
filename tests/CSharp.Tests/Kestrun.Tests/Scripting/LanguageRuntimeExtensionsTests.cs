using Kestrun.Scripting;
using Xunit;

namespace KestrunTests.Scripting;

[Trait("Category", "Scripting")]
public class LanguageRuntimeExtensionsTests
{
    [Fact]
    public void ScriptLanguageAttribute_SetsLanguageProperty()
    {
        // Arrange & Act
        var attr = new ScriptLanguageAttribute(ScriptLanguage.CSharp);

        // Assert
        Assert.Equal(ScriptLanguage.CSharp, attr.Language);
    }

    [Theory]
    [InlineData(ScriptLanguage.Native)]
    [InlineData(ScriptLanguage.PowerShell)]
    [InlineData(ScriptLanguage.CSharp)]
    [InlineData(ScriptLanguage.FSharp)]
    [InlineData(ScriptLanguage.Python)]
    [InlineData(ScriptLanguage.JavaScript)]
    [InlineData(ScriptLanguage.VBNet)]
    public void ScriptLanguageAttribute_ConstructorWithAllLanguages_SetsCorrectly(ScriptLanguage language)
    {
        // Arrange & Act
        var attr = new ScriptLanguageAttribute(language);

        // Assert
        Assert.Equal(language, attr.Language);
    }

    [Fact]
    public void ScriptLanguageAttribute_Equality_SameLanguage_AreEqual()
    {
        // Arrange
        var attr1 = new ScriptLanguageAttribute(ScriptLanguage.PowerShell);
        var attr2 = new ScriptLanguageAttribute(ScriptLanguage.PowerShell);

        // Assert
        Assert.Equal(attr1.Language, attr2.Language);
    }

    [Fact]
    public void ScriptLanguageAttribute_Equality_DifferentLanguage_AreNotEqual()
    {
        // Arrange
        var attr1 = new ScriptLanguageAttribute(ScriptLanguage.PowerShell);
        var attr2 = new ScriptLanguageAttribute(ScriptLanguage.CSharp);

        // Assert
        Assert.NotEqual(attr1.Language, attr2.Language);
    }

    [Fact]
    public void ScriptLanguageAttribute_CanBeUsedAsMetadata()
    {
        // Arrange
        var attr = new ScriptLanguageAttribute(ScriptLanguage.FSharp);
        var metadata = new List<object> { attr };

        // Act
        var retrieved = metadata.OfType<ScriptLanguageAttribute>().FirstOrDefault();

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(ScriptLanguage.FSharp, retrieved.Language);
    }

    [Fact]
    public void ScriptLanguageAttribute_MultipleAttributes_CanBeStored()
    {
        // Arrange
        var attr1 = new ScriptLanguageAttribute(ScriptLanguage.PowerShell);
        var attr2 = new ScriptLanguageAttribute(ScriptLanguage.CSharp);
        var metadata = new List<object> { attr1, attr2 };

        // Act
        var retrieved = metadata.OfType<ScriptLanguageAttribute>().ToList();

        // Assert
        Assert.Equal(2, retrieved.Count);
        Assert.Contains(retrieved, a => a.Language == ScriptLanguage.PowerShell);
        Assert.Contains(retrieved, a => a.Language == ScriptLanguage.CSharp);
    }
}
