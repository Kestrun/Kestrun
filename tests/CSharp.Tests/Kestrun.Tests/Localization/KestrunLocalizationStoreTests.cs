using Kestrun.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KestrunTests.Localization;

public class KestrunLocalizationStoreTests
{
    [Fact]
    [Trait("Category", "Localization")]
    public void ResolveCulture_Uses_Exact_Parent_And_Default_Fallbacks()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            WriteStringTable(temp.FullName, "i18n", "en-US", "@{\nHello = \"Hello\"\n}");
            WriteStringTable(temp.FullName, "i18n", "it", "@{\nHello = \"Ciao\"\n}");

            var options = new KestrunLocalizationOptions
            {
                DefaultCulture = "en-US",
                ResourcesBasePath = "i18n"
            };

            var store = new KestrunLocalizationStore(options, temp.FullName, NullLogger<KestrunLocalizationStore>.Instance);

            Assert.Equal("it", store.ResolveCulture("it-CH"));
            Assert.Equal("en-US", store.ResolveCulture("fr-FR"));
            Assert.Equal("en-US", store.ResolveCulture("not-a-culture"));
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Localization")]
    public void GetStringsForCulture_Returns_Empty_When_Default_Missing()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            WriteStringTable(temp.FullName, "i18n", "it-IT", "@{\nHello = \"Ciao\"\n}");

            var options = new KestrunLocalizationOptions
            {
                DefaultCulture = "en-US",
                ResourcesBasePath = "i18n"
            };

            var store = new KestrunLocalizationStore(options, temp.FullName, NullLogger<KestrunLocalizationStore>.Instance);

            var strings = store.GetStringsForCulture("fr-FR");
            Assert.Empty(strings);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    private static void WriteStringTable(string root, string basePath, string culture, string content)
    {
        var dir = Path.Combine(root, basePath, culture);
        _ = Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "Messages.psd1");
        File.WriteAllText(path, content);
    }
}
