using System.Globalization;
using System.Net;
using Kestrun.Hosting;
using Kestrun.Scripting;
using Kestrun.Logging;
using Kestrun.Utilities;
using Microsoft.AspNetCore.Localization;
using Serilog;

var currentDir = Directory.GetCurrentDirectory();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/RequestLocalization.log", rollingInterval: RollingInterval.Hour)
    .Register("DefaultLogger", setAsDefault: true);

var server = new KestrunHost("Kestrun RequestLocalization Demo", currentDir);
server.Options.ServerOptions.AllowSynchronousIO = true;
server.Options.ServerOptions.AddServerHeader = false;

server.ConfigureListener(
    port: 5000,
    ipAddress: IPAddress.Loopback,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1
);

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

server.AddPowerShellRuntime();

var greetingsData = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
{
    ["en-US"] = new() {
        ["Welcome"] = "Welcome to Kestrun!",
        ["CurrentCulture"] = "Current culture"
    },
    ["fr-FR"] = new() {
        ["Welcome"] = "Bienvenue à Kestrun!",
        ["CurrentCulture"] = "Culture actuelle"
    },
    ["es-ES"] = new() {
        ["Welcome"] = "¡Bienvenido a Kestrun!",
        ["CurrentCulture"] = "Cultura actual"
    }
});

if (!server.SharedState.Set("Greetings", greetingsData))
{
    Console.WriteLine("Failed to set shared state 'Greetings'.");
    Environment.Exit(1);
}

server.EnableConfiguration();

server.AddMapRoute("/", HttpVerb.Get, 
"$culture = [System.Globalization.CultureInfo]::CurrentCulture.Name; $greetingsData = $Greetings | ConvertFrom-Json -AsHashtable; if (-not $greetingsData.ContainsKey($culture)) { $culture = 'en-US' }; $messages = $greetingsData[$culture]; $html = \"<html><body><h1>$($messages.Welcome)</h1><p>$($messages.CurrentCulture): $culture</p><a href='/?culture=en-US'>English</a> | <a href='/?culture=fr-FR'>Français</a> | <a href='/?culture=es-ES'>Español</a></body></html>\"; Write-KrHtmlResponse -Html $html -StatusCode 200", 
ScriptLanguage.PowerShell);

server.AddMapRoute("/api/culture", HttpVerb.Get,
"$cultureInfo = @{ CurrentCulture = [System.Globalization.CultureInfo]::CurrentCulture.Name; CurrentUICulture = [System.Globalization.CultureInfo]::CurrentUICulture.Name }; Write-KrJsonResponse -InputObject $cultureInfo -StatusCode 200",
ScriptLanguage.PowerShell);

Console.WriteLine();
Console.WriteLine("Server starting on http://localhost:5000");
Console.WriteLine("Try: http://localhost:5000/?culture=fr-FR");
Console.WriteLine();

server.Run();
