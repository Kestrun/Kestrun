using System.Globalization;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Localization;
using Serilog;
using Serilog.Events;

/*
 * Kestrun C# Example: Request Localization
 * 
 * This example demonstrates how to use request localization in Kestrun to serve
 * content in multiple languages based on user preferences.
 */

// Configure Serilog logger
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/RequestLocalization.log", rollingInterval: RollingInterval.Hour)
    .CreateLogger();

// Create server
var server = new KestrunHost("Kestrun RequestLocalization Demo", Log.Logger);

// Configure server options
server.ServerOptions.AllowSynchronousIO = true;
server.DenyServerHeader();

// Add endpoint
server.AddEndpoint(System.Net.IPAddress.Loopback, 5000);

// Configure request localization
server.AddRequestLocalization(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en-US"),
        new CultureInfo("fr-FR"),
        new CultureInfo("es-ES"),
        new CultureInfo("de-DE"),
        new CultureInfo("ja-JP")
    };

    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.FallBackToParentCultures = true;
    options.FallBackToParentUICultures = true;
});

// Localized greetings
var greetings = new Dictionary<string, Dictionary<string, string>>
{
    ["en-US"] = new()
    {
        ["Welcome"] = "Welcome to Kestrun!",
        ["CurrentCulture"] = "Current culture",
        ["Instructions"] = "To change language, use one of these methods:",
        ["Method1"] = "Query string: ?culture=fr-FR",
        ["Method2"] = "Cookie: .AspNetCore.Culture=c=fr-FR|uic=fr-FR",
        ["Method3"] = "Accept-Language header: Accept-Language: fr-FR"
    },
    ["fr-FR"] = new()
    {
        ["Welcome"] = "Bienvenue à Kestrun!",
        ["CurrentCulture"] = "Culture actuelle",
        ["Instructions"] = "Pour changer de langue, utilisez l'une de ces méthodes:",
        ["Method1"] = "Chaîne de requête: ?culture=fr-FR",
        ["Method2"] = "Cookie: .AspNetCore.Culture=c=fr-FR|uic=fr-FR",
        ["Method3"] = "En-tête Accept-Language: Accept-Language: fr-FR"
    },
    ["es-ES"] = new()
    {
        ["Welcome"] = "¡Bienvenido a Kestrun!",
        ["CurrentCulture"] = "Cultura actual",
        ["Instructions"] = "Para cambiar el idioma, use uno de estos métodos:",
        ["Method1"] = "Cadena de consulta: ?culture=es-ES",
        ["Method2"] = "Cookie: .AspNetCore.Culture=c=es-ES|uic=es-ES",
        ["Method3"] = "Encabezado Accept-Language: Accept-Language: es-ES"
    },
    ["de-DE"] = new()
    {
        ["Welcome"] = "Willkommen bei Kestrun!",
        ["CurrentCulture"] = "Aktuelle Kultur",
        ["Instructions"] = "Um die Sprache zu ändern, verwenden Sie eine dieser Methoden:",
        ["Method1"] = "Abfragezeichenfolge: ?culture=de-DE",
        ["Method2"] = "Cookie: .AspNetCore.Culture=c=de-DE|uic=de-DE",
        ["Method3"] = "Accept-Language-Header: Accept-Language: de-DE"
    },
    ["ja-JP"] = new()
    {
        ["Welcome"] = "Kestrunへようこそ！",
        ["CurrentCulture"] = "現在のカルチャ",
        ["Instructions"] = "言語を変更するには、次のいずれかの方法を使用してください：",
        ["Method1"] = "クエリ文字列: ?culture=ja-JP",
        ["Method2"] = "クッキー: .AspNetCore.Culture=c=ja-JP|uic=ja-JP",
        ["Method3"] = "Accept-Languageヘッダー: Accept-Language: ja-JP"
    }
};

// Enable configuration
server.Apply();

// Main route - displays localized content
server.AddMapRoute("/", Kestrun.Models.HttpVerb.Get, context =>
{
    // Get the current culture from the request
    var culture = CultureInfo.CurrentCulture.Name;

    // Fall back to parent culture if exact match not found
    if (!greetings.ContainsKey(culture))
    {
        var parentCulture = CultureInfo.CurrentCulture.Parent.Name;
        culture = greetings.ContainsKey(parentCulture) ? parentCulture : "en-US";
    }

    var messages = greetings[culture];

    var html = $@"
<!DOCTYPE html>
<html lang=""{culture}"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <title>Kestrun Request Localization Demo</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            max-width: 800px;
            margin: 50px auto;
            padding: 20px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }}
        .container {{
            background: rgba(255, 255, 255, 0.1);
            backdrop-filter: blur(10px);
            border-radius: 15px;
            padding: 30px;
            box-shadow: 0 8px 32px 0 rgba(31, 38, 135, 0.37);
        }}
        h1 {{
            margin-top: 0;
            font-size: 2.5em;
        }}
        .culture-info {{
            background: rgba(255, 255, 255, 0.2);
            padding: 15px;
            border-radius: 10px;
            margin: 20px 0;
        }}
        ul {{
            list-style-type: none;
            padding-left: 0;
        }}
        li {{
            padding: 8px 0;
        }}
        code {{
            background: rgba(0, 0, 0, 0.3);
            padding: 3px 8px;
            border-radius: 5px;
            font-family: 'Courier New', monospace;
        }}
        .languages {{
            margin-top: 30px;
        }}
        .language-btn {{
            display: inline-block;
            margin: 5px;
            padding: 10px 20px;
            background: rgba(255, 255, 255, 0.2);
            border: 2px solid rgba(255, 255, 255, 0.3);
            border-radius: 5px;
            color: white;
            text-decoration: none;
            transition: all 0.3s;
        }}
        .language-btn:hover {{
            background: rgba(255, 255, 255, 0.3);
            transform: translateY(-2px);
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <h1>{messages["Welcome"]}</h1>
        <div class=""culture-info"">
            <strong>{messages["CurrentCulture"]}:</strong> {culture}
        </div>
        <h2>{messages["Instructions"]}</h2>
        <ul>
            <li>1. {messages["Method1"]}</li>
            <li>2. {messages["Method2"]}</li>
            <li>3. {messages["Method3"]}</li>
        </ul>
        <div class=""languages"">
            <h3>Quick Links:</h3>
            <a href=""/?culture=en-US"" class=""language-btn"">English 🇺🇸</a>
            <a href=""/?culture=fr-FR"" class=""language-btn"">Français 🇫🇷</a>
            <a href=""/?culture=es-ES"" class=""language-btn"">Español 🇪🇸</a>
            <a href=""/?culture=de-DE"" class=""language-btn"">Deutsch 🇩🇪</a>
            <a href=""/?culture=ja-JP"" class=""language-btn"">日本語 🇯🇵</a>
        </div>
    </div>
</body>
</html>";

    context.Response.WriteHtmlResponse(html, 200);
});

// API route - returns current culture information as JSON
server.AddMapRoute("/api/culture", Kestrun.Models.HttpVerb.Get, context =>
{
    var cultureInfo = new
    {
        CurrentCulture = CultureInfo.CurrentCulture.Name,
        CurrentUICulture = CultureInfo.CurrentUICulture.Name,
        DisplayName = CultureInfo.CurrentCulture.DisplayName,
        NativeName = CultureInfo.CurrentCulture.NativeName,
        EnglishName = CultureInfo.CurrentCulture.EnglishName,
        DateFormat = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern,
        TimeFormat = CultureInfo.CurrentCulture.DateTimeFormat.LongTimePattern,
        NumberFormat = new
        {
            DecimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator,
            GroupSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator,
            CurrencySymbol = CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol
        },
        Timestamp = DateTime.UtcNow.ToString("o")
    };

    context.Response.WriteJsonResponse(cultureInfo, 200);
});

// Start the server
Console.WriteLine();
Console.WriteLine("================================================");
Console.WriteLine("  Kestrun Request Localization Demo");
Console.WriteLine("================================================");
Console.WriteLine();
Console.WriteLine("Server is starting on http://localhost:5000");
Console.WriteLine();
Console.WriteLine("Try these URLs:");
Console.WriteLine("  http://localhost:5000/?culture=en-US  (English)");
Console.WriteLine("  http://localhost:5000/?culture=fr-FR  (French)");
Console.WriteLine("  http://localhost:5000/?culture=es-ES  (Spanish)");
Console.WriteLine("  http://localhost:5000/?culture=de-DE  (German)");
Console.WriteLine("  http://localhost:5000/?culture=ja-JP  (Japanese)");
Console.WriteLine();
Console.WriteLine("API Endpoint:");
Console.WriteLine("  http://localhost:5000/api/culture");
Console.WriteLine();
Console.WriteLine("Press Ctrl+C to stop the server");
Console.WriteLine("================================================");
Console.WriteLine();

await server.RunAsync();
