using System.Text.Json;
using Kestrun.Localization;
using Microsoft.Extensions.Logging;
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

            var store = new KestrunLocalizationStore(options, temp.FullName, NullLogger<KestrunLocalizationStore>.Instance);

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
    public void GetStringsForCulture_Logs_Error_When_No_File_Found()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            var logger = new TestLogger<KestrunLocalizationStore>();
            var options = new KestrunLocalizationOptions
            {
                DefaultCulture = "en-US",
                ResourcesBasePath = "i18n"
            };

            _ = Directory.CreateDirectory(Path.Combine(temp.FullName, "i18n", "en-US"));

            var store = new KestrunLocalizationStore(options, temp.FullName, logger);

            var strings = store.GetStringsForCulture("en-US");
            _ = strings.TryGetValue("Hello", out _);

            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("Localization file missing", StringComparison.Ordinal));
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

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
