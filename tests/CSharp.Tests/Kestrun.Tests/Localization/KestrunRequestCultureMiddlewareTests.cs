using Kestrun.Localization;
using Kestrun.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace KestrunTests.Localization;

public class KestrunRequestCultureMiddlewareTests
{
    [Fact]
    [Trait("Category", "Localization")]
    public async Task Middleware_Uses_AcceptLanguage_And_Sets_Strings()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            WriteStringTable(temp.FullName, "i18n", "en-US", "@{\nHello = \"Hello\"\n}");
            WriteStringTable(temp.FullName, "i18n", "it-IT", "@{\nHello = \"Ciao\"\n}");

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = temp.FullName
            });
            _ = builder.WebHost.UseTestServer();

            var app = builder.Build();

            var options = new KestrunLocalizationOptions
            {
                DefaultCulture = "en-US",
                ResourcesBasePath = "i18n"
            };

            _ = app.UseKestrunLocalization(options);

            _ = app.MapGet("/hello", (HttpContext ctx) =>
            {
                var culture = ctx.Items["KrCulture"] as string;
                var hello = ctx.Items["KrStrings"] is IReadOnlyDictionary<string, string> strings &&
                            strings.TryGetValue("Hello", out var value)
                    ? value
                    : string.Empty;
                return Results.Text($"{culture}:{hello}");
            });

            await app.StartAsync();
            var client = app.GetTestClient();
            _ = client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "it-IT");

            var response = await client.GetStringAsync("/hello");
            Assert.Equal("it-IT:Ciao", response);

            await app.StopAsync();
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
