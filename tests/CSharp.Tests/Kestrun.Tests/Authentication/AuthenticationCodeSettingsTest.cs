using Kestrun.Authentication;
using Kestrun.Scripting;
using Xunit;

namespace KestrunTests.Authentication;

public class AuthenticationCodeSettingsTest
{
    [Fact]
    public void Default_Values_Are_Correct()
    {
        var settings = new AuthenticationCodeSettings();

        Assert.Equal(ScriptLanguage.Native, settings.Language);
        Assert.Null(settings.Code);
        Assert.Null(settings.ExtraImports);
        Assert.Null(settings.ExtraRefs);
        Assert.Equal(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp12, settings.CSharpVersion);
        Assert.Equal(Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic12, settings.VisualBasicVersion);
    }

    [Fact]
    public void Can_Set_All_Properties()
    {
        var imports = new[] { "System", "System.Linq" };
        var refs = new[] { typeof(object).Assembly, typeof(Enumerable).Assembly };

        var settings = new AuthenticationCodeSettings
        {
            Language = ScriptLanguage.CSharp,
            Code = "return true;",
            ExtraImports = imports,
            ExtraRefs = refs,
            CSharpVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp11,
            VisualBasicVersion = Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16
        };

        Assert.Equal(ScriptLanguage.CSharp, settings.Language);
        Assert.Equal("return true;", settings.Code);
        Assert.Equal(imports, settings.ExtraImports);
        Assert.Equal(refs, settings.ExtraRefs);
        Assert.Equal(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp11, settings.CSharpVersion);
        Assert.Equal(Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16, settings.VisualBasicVersion);
    }

    [Fact]
    public void Record_Equality_Works()
    {
        var settings1 = new AuthenticationCodeSettings
        {
            Language = ScriptLanguage.CSharp,
            Code = "return true;"
        };

        var settings2 = new AuthenticationCodeSettings
        {
            Language = ScriptLanguage.CSharp,
            Code = "return true;"
        };

        Assert.Equal(settings1, settings2);
        Assert.True(settings1 == settings2);
    }
}
