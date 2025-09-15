using System.Net;
using Microsoft.AspNetCore.ResponseCompression;
using Kestrun.Utilities;
// Add the namespace that contains HttpVerb
using System.Text;
using Serilog;
using Kestrun.Scripting;
using Kestrun.Logging;
using Kestrun.Hosting;
using Kestrun.Authentication;


Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.File("logs/razor.log", rollingInterval: RollingInterval.Day)
        .Register("Audit", setAsDefault: true);

// Define a constant for the basic authentication scheme
const string BasicPowershellScheme = "PowershellBasic";
var currentDir = Directory.GetCurrentDirectory();
// 1. Create server
var server = new KestrunHost("MyKestrunServer", currentDir);

// Set Kestrel options
server.Options.ServerOptions.AllowSynchronousIO = false;
server.Options.ServerOptions.AddServerHeader = false; // DenyServerHeader

server.Options.ServerLimits.MaxRequestBodySize = 10485760;
server.Options.ServerLimits.MaxConcurrentConnections = 100;
server.Options.ServerLimits.MaxRequestHeaderCount = 100;
server.Options.ServerLimits.KeepAliveTimeout = TimeSpan.FromSeconds(120);
server.Options.ServerLimits.MinRequestBodyDataRate = new Microsoft.AspNetCore.Server.Kestrel.Core.MinDataRate(100, TimeSpan.FromSeconds(10));

// 3. Configure listeners
server.ConfigureListener(
    port: 5000,
    ipAddress: IPAddress.Any,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1
);

server.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
    [
        "text/plain",
        "text/css",
        "application/javascript",
        "application/json",
        "application/xml",
        "text/html"
    ]);
    options.Providers.Add<BrotliCompressionProvider>();
}).AddPowerShellRuntime()

.AddBasicAuthentication(BasicPowershellScheme, opts =>
{
    opts.Realm = "Power-Kestrun";

    opts.ValidateCodeSettings = new AuthenticationCodeSettings
    {
        Language = ScriptLanguage.PowerShell,
        Code = """
            param(
                [string]$Username,
                [string]$Password
            )
            if ($Username -eq 'admin' -and $Password -eq 'password') {
                return $true
            } else {
                return $false
            }
        """
    };
    opts.Base64Encoded = true;            // default anyway
    opts.RequireHttps = false;           // example
})
.AddStaticMapOverride("/assets/report", async ctx =>
{
    await ctx.Response.WriteJsonResponseAsync(new { ok = true, message = "Static override works!" });
})
.AddStaticMapOverride(
   pattern: "/assets/ps-report", code: """

  $Payload = @{
    ok    = $true
    lang  = 'PowerShell'
    time  = (Get-Date)
}
Write-KrJsonResponse -inputObject $Payload
""", language: ScriptLanguage.PowerShell, requireSchemes: [BasicPowershellScheme])

.AddStaticMapOverride(
    "/assets/vb-report",
    """
    Dim payload = New With {.ok=True, .lang="VB.NET", .time=DateTime.UtcNow}
    Await Context.Response.WriteJsonResponseAsync(payload)
""",
    ScriptLanguage.VBNet)

.AddStaticMapOverride(
    "/assets/cs-report",
    """  
        var payload = new
        {
            ok = true,
            lang = "CSharp",
            time = DateTime.UtcNow
        };
        await Context.Response.WriteJsonResponseAsync(payload);
""",
    ScriptLanguage.CSharp)

.AddFileServer(options =>
{
    options.RequestPath = "/assets"; // Set the request path for static files 
    options.EnableDirectoryBrowsing = true;
})
.AddPowerShellRazorPages(routePrefix: "/pages");




server.EnableConfiguration();

server.AddMapRoute("/ps/json",
            HttpVerb.Get,
            """
            Write-Output "Hello from PowerShell script! - Json Response"
            # Payload
            $payload = @{
                Body           = "Hello from PowerShell script! - Json Response"
                RequestQuery   = $Context.Request.Query
                RequestHeaders = $Context.Request.Headers
                RequestMethod  = $Context.Request.Method
                RequestPath    = $Context.Request.Path
                # If you want to return the request body, uncomment the next line
                RequestBody    = $Context.Request.Body
            }
            Write-KrJsonResponse -inputObject $payload -statusCode 200
            """,
            ScriptLanguage.PowerShell);
// 5️⃣ Run!
Console.WriteLine("Open  http://localhost:5000/Hello");

await server.RunUntilShutdownAsync(
    consoleEncoding: Encoding.UTF8,
    onStarted: () => Console.WriteLine("Server ready 🟢"),
    onShutdownError: ex => Console.WriteLine($"Shutdown error: {ex.Message}"

    )
);
