using System.Globalization;
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

    [Fact]
    [Trait("Category", "Localization")]
    public async Task Middleware_Uses_AcceptLanguage_Quality_Weights()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            WriteStringTable(temp.FullName, "i18n", "en-US", "@{\nHello = \"Hello\"\n}");
            WriteStringTable(temp.FullName, "i18n", "fr-FR", "@{\nHello = \"Bonjour\"\n}");

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
            _ = client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US;q=0.1, fr-FR;q=0.9");

            var response = await client.GetStringAsync("/hello");
            Assert.Equal("fr-FR:Bonjour", response);

            await app.StopAsync();
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Localization")]
    public async Task Middleware_Falls_Back_When_Culture_Is_Invalid()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            WriteStringTable(temp.FullName, "i18n", "en-US", "@{\nHello = \"Hello\"\n}");

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

            var response = await client.GetStringAsync("/hello?lang=not-a-culture");
            Assert.Equal("en-US:Hello", response);

            await app.StopAsync();
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Localization")]
    public async Task Middleware_Prefers_Query_Over_Cookie_And_AcceptLanguage()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            WriteStringTable(temp.FullName, "i18n", "en-US", "@{\nHello = \"Hello\"\n}");
            WriteStringTable(temp.FullName, "i18n", "it-IT", "@{\nHello = \"Ciao\"\n}");
            WriteStringTable(temp.FullName, "i18n", "fr-FR", "@{\nHello = \"Bonjour\"\n}");
            WriteStringTable(temp.FullName, "i18n", "es-ES", "@{\nHello = \"Hola\"\n}");

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = temp.FullName
            });
            _ = builder.WebHost.UseTestServer();

            var app = builder.Build();

            var options = new KestrunLocalizationOptions
            {
                DefaultCulture = "en-US",
                ResourcesBasePath = "i18n",
                EnableQuery = true,
                EnableCookie = true
            };

            _ = app.UseKestrunLocalization(options);

            _ = app.MapGet("/hello", (HttpContext ctx) =>
            {
                var culture = ctx.Items["KrCulture"] as string;
                return Results.Text(culture ?? string.Empty);
            });

            await app.StartAsync();
            var client = app.GetTestClient();
            _ = client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "es-ES");
            _ = client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", "lang=fr-FR");

            var response = await client.GetStringAsync("/hello?lang=it-IT");
            Assert.Equal("it-IT", response);

            await app.StopAsync();
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Localization")]
    public async Task Middleware_Uses_Cookie_When_Query_Disabled()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            WriteStringTable(temp.FullName, "i18n", "en-US", "@{\nHello = \"Hello\"\n}");
            WriteStringTable(temp.FullName, "i18n", "fr-FR", "@{\nHello = \"Bonjour\"\n}");

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = temp.FullName
            });
            _ = builder.WebHost.UseTestServer();

            var app = builder.Build();

            var options = new KestrunLocalizationOptions
            {
                DefaultCulture = "en-US",
                ResourcesBasePath = "i18n",
                EnableQuery = false,
                EnableCookie = true
            };

            _ = app.UseKestrunLocalization(options);

            _ = app.MapGet("/hello", (HttpContext ctx) =>
            {
                var culture = ctx.Items["KrCulture"] as string;
                return Results.Text(culture ?? string.Empty);
            });

            await app.StartAsync();
            var client = app.GetTestClient();
            _ = client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", "lang=fr-FR");

            var response = await client.GetStringAsync("/hello?lang=it-IT");
            Assert.Equal("fr-FR", response);

            await app.StopAsync();
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Localization")]
    public async Task Middleware_Disables_AcceptLanguage_When_Configured()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            WriteStringTable(temp.FullName, "i18n", "en-US", "@{\nHello = \"Hello\"\n}");
            WriteStringTable(temp.FullName, "i18n", "fr-FR", "@{\nHello = \"Bonjour\"\n}");

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = temp.FullName
            });
            _ = builder.WebHost.UseTestServer();

            var app = builder.Build();

            var options = new KestrunLocalizationOptions
            {
                DefaultCulture = "en-US",
                ResourcesBasePath = "i18n",
                EnableAcceptLanguage = false
            };

            _ = app.UseKestrunLocalization(options);

            _ = app.MapGet("/hello", (HttpContext ctx) =>
            {
                var culture = ctx.Items["KrCulture"] as string;
                return Results.Text(culture ?? string.Empty);
            });

            await app.StartAsync();
            var client = app.GetTestClient();
            _ = client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "fr-FR");

            var response = await client.GetStringAsync("/hello");
            Assert.Equal("en-US", response);

            await app.StopAsync();
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Localization")]
    public async Task Middleware_Ignores_Wildcards_And_Invalid_Quality_Values()
    {
        var temp = Directory.CreateTempSubdirectory();
        try
        {
            WriteStringTable(temp.FullName, "i18n", "en-US", "@{\nHello = \"Hello\"\n}");
            WriteStringTable(temp.FullName, "i18n", "fr-FR", "@{\nHello = \"Bonjour\"\n}");

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
                return Results.Text(culture ?? string.Empty);
            });

            await app.StartAsync();
            var client = app.GetTestClient();
            _ = client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "*;q=0.9, fr-FR;q=2.0, en-US;q=0.3");

            var response = await client.GetStringAsync("/hello");
            Assert.Equal("en-US", response);

            await app.StopAsync();
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Localization")]
    public async Task Middleware_Restores_Thread_Culture_After_Request()
    {
        var temp = Directory.CreateTempSubdirectory();
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");

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
                ResourcesBasePath = "i18n",
                EnableQuery = true
            };

            _ = app.UseKestrunLocalization(options);

            _ = app.MapGet("/hello", () => Results.Text(CultureInfo.CurrentCulture.Name));

            await app.StartAsync();
            var client = app.GetTestClient();

            _ = await client.GetStringAsync("/hello?lang=it-IT");

            Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
            Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);

            await app.StopAsync();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
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
