using System.Text.Json;
using Kestrun.Localization;
using Serilog;
using Serilog.Core;
using Serilog.Events;
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

            var store = new KestrunLocalizationStore(options, temp.FullName, Logger.None);

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

            var store = new KestrunLocalizationStore(options, temp.FullName, Logger.None);

            var strings = store.GetStringsForCulture("fr-FR");
            Assert.Empty(strings);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Localization")]
    public void GetStringsForCulture_Uses_Json_Fallback_When_Psd1_Missing()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            var payload = new { Hello = "Hello", Labels = new { Save = "Save" } };
            WriteJsonStringTable(temp.FullName, "i18n", "en-US", payload);

            var options = new KestrunLocalizationOptions
            {
                DefaultCulture = "en-US",
                ResourcesBasePath = "i18n"
            };

            var store = new KestrunLocalizationStore(options, temp.FullName, Logger.None);

            var strings = store.GetStringsForCulture("en-US");
            Assert.Equal("Hello", strings["Hello"]);
            Assert.Equal("Save", strings["Labels.Save"]);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Localization")]
    public void GetStringsForCulture_Uses_Sibling_Before_Parent_And_Default()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            WriteStringTable(temp.FullName, "i18n", "en-US", "@{\nLabels = @{\nSave = \"Save\"\nCancel = \"Cancel\"\n}\n}");
            WriteStringTable(temp.FullName, "i18n", "fr", "@{\nLabels = @{\nSave = \"Sauvegarder\"\nCancel = \"Annuler\"\n}\n}");
            WriteStringTable(temp.FullName, "i18n", "fr-FR", "@{\nLabels = @{\nSave = \"Enregistrer\"\n}\n}");
            WriteStringTable(temp.FullName, "i18n", "fr-CA", "@{\nHello = \"Bonjour du Canada\"\n}");

            var options = new KestrunLocalizationOptions
            {
                DefaultCulture = "en-US",
                ResourcesBasePath = "i18n"
            };

            var store = new KestrunLocalizationStore(options, temp.FullName, Logger.None);

            var strings = store.GetStringsForCulture("fr-CA");

            Assert.Equal("Bonjour du Canada", strings["Hello"]);
            Assert.Equal("Enregistrer", strings["Labels.Save"]); // sibling fr-FR overrides parent fr
            Assert.Equal("Annuler", strings["Labels.Cancel"]); // parent fr fills missing key
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Localization")]
    public void AvailableCultures_Includes_Json_Fallback_Cultures()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            var payload = new { Hello = "Hello" };
            WriteJsonStringTable(temp.FullName, "i18n", "en-US", payload);

            var options = new KestrunLocalizationOptions
            {
                DefaultCulture = "en-US",
                ResourcesBasePath = "i18n",
                FileName = "Messages.psd1"
            };

            var store = new KestrunLocalizationStore(options, temp.FullName, Logger.None);

            Assert.Contains("en-US", store.AvailableCultures);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Localization")]
    public void GetStringsForResolvedCulture_Returns_Empty_For_Unknown_Culture()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            WriteStringTable(temp.FullName, "i18n", "en-US", "@{\nHello = \"Hello\"\n}");

            var options = new KestrunLocalizationOptions
            {
                DefaultCulture = "en-US",
                ResourcesBasePath = "i18n"
            };

            var store = new KestrunLocalizationStore(options, temp.FullName, Logger.None);

            var strings = store.GetStringsForResolvedCulture("fr-FR");
            Assert.Empty(strings);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Localization")]
    public void GetStringsForCulture_Keys_Return_Union_Across_Candidates()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            WriteStringTable(temp.FullName, "i18n", "en-US", "@{\nHello = \"Hello\"\nLabels = @{\nSave = \"Save\"\n}\n}");
            WriteStringTable(temp.FullName, "i18n", "fr-FR", "@{\nBonjour = \"Bonjour\"\nLabels = @{\nCancel = \"Annuler\"\n}\n}");

            var options = new KestrunLocalizationOptions
            {
                DefaultCulture = "en-US",
                ResourcesBasePath = "i18n"
            };

            var store = new KestrunLocalizationStore(options, temp.FullName, Logger.None);

            var strings = store.GetStringsForCulture("fr-FR");
            var keys = strings.Keys.ToArray();

            Assert.Contains("Hello", keys);
            Assert.Contains("Bonjour", keys);
            Assert.Contains("Labels.Save", keys);
            Assert.Contains("Labels.Cancel", keys);
            Assert.Equal(4, strings.Count);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Localization")]
    public void GetStringsForCulture_Logs_Error_When_No_File_Found()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            var sink = new CollectingSink();
            using var logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(sink)
                .CreateLogger();

            var options = new KestrunLocalizationOptions
            {
                DefaultCulture = "en-US",
                ResourcesBasePath = "i18n"
            };

            _ = Directory.CreateDirectory(Path.Combine(temp.FullName, "i18n", "en-US"));

            var store = new KestrunLocalizationStore(options, temp.FullName, logger);

            var strings = store.GetStringsForCulture("en-US");
            _ = strings.TryGetValue("Hello", out _);

            Assert.Contains(
                sink.Events,
                e => e.Level == LogEventLevel.Debug && e.RenderMessage().Contains("Localization file missing", StringComparison.Ordinal));
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

    private static void WriteJsonStringTable(string root, string basePath, string culture, object content)
    {
        var dir = Path.Combine(root, basePath, culture);
        _ = Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "Messages.json");
        File.WriteAllText(path, JsonSerializer.Serialize(content, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
