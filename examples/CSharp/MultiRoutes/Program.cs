using System.Net;
using System.Security.Cryptography.X509Certificates;
using Serilog;
using Microsoft.AspNetCore.ResponseCompression;
using System.Text;
using Kestrun.Utilities;
using Kestrun.Scripting;
using Kestrun.Certificates;
using Kestrun.Logging;
using Kestrun.Hosting;   // Only for writing the CSR key

var currentDir = Directory.GetCurrentDirectory();

// 1️⃣  Audit log: only warnings and above, writes JSON files
Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.File("logs/multiroute.log", rollingInterval: RollingInterval.Day)
        .Register("Audit", setAsDefault: true);

// 1. Create server 
var server = new KestrunHost("Kestrun MultiRoutes", currentDir);
// Set Kestrel options
server.Options.ServerOptions.AllowSynchronousIO = false;
server.Options.ServerOptions.AddServerHeader = false; // DenyServerHeader

server.Options.ServerLimits.MaxRequestBodySize = 10485760;
server.Options.ServerLimits.MaxConcurrentConnections = 100;
server.Options.ServerLimits.MaxRequestHeaderCount = 100;
server.Options.ServerLimits.KeepAliveTimeout = TimeSpan.FromSeconds(120);

server.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
    [
        "application/json",
        "application/bson",
        "application/cbor",
        "application/yaml",
        "application/xml",
        "text/plain",
        "text/html"
    ]);
    options.Providers.Add<BrotliCompressionProvider>();
}).AddFavicon().AddPowerShellRuntime();



X509Certificate2? x509Certificate = null;

if (File.Exists("./devcert.pfx"))
{
    // Import existing certificate
    x509Certificate = CertificateManager.Import(
      "./devcert.pfx",
      "p@ss".AsSpan()
  );
}
else
{
    // Create a new self-signed certificate

    x509Certificate = CertificateManager.NewSelfSigned(
      new CertificateManager.SelfSignedOptions(
          DnsNames: ["localhost", "127.0.0.1"],
          KeyType: CertificateManager.KeyType.Rsa,
          KeyLength: 2048,
          ValidDays: 30,
          Exportable: true
      )
  );
    // Export the certificate to a file
    CertificateManager.Export(
        x509Certificate,
        "./devcert.pfx",
        CertificateManager.ExportFormat.Pfx,
        "p@ss".AsSpan()
    );
}

if (!CertificateManager.Validate(
    x509Certificate,
    checkRevocation: false,
    allowWeakAlgorithms: false,
    denySelfSigned: false,
    strictPurpose: true
))
{
    Console.WriteLine("Certificate validation failed.");
    Environment.Exit(1);
}



// 3. Add listeners
server.ConfigureListener(
    port: 5001,
    ipAddress: IPAddress.Loopback,
    x509Certificate: x509Certificate,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2AndHttp3
);
server.ConfigureListener(
    port: 5000,
    ipAddress: IPAddress.Loopback,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1
);

server.EnableConfiguration();
/*string pattern,
                                    IEnumerable<HttpMethod> httpMethods,
                                    string scriptBlock,
                                    ScriptLanguage language = ScriptLanguage.PowerShell,
                                    string[]? extraImports = null,
                                    Assembly[]? extraRefs = null)*/
// 4. Add routes
server.AddMapRoute("/ps/json",
            HttpVerb.Get,
            """
            Write-Output "Hello from PowerShell script!" 
            $payload = @{
                Body           = "Hello from PowerShell script! - Json Response(From C#)"
                RequestQuery   = $Context.Request.Query
                RequestHeaders = $Context.Request.Headers
                RequestMethod  = $Context.Request.Method
                RequestPath    = $Context.Request.Path
                # If you want to return the request body, uncomment the next line
                RequestBody    = $Context.Request.Body 
            }
            Write-KrWarningLog -Name "audit" -PropertyValues $payload  -Message "This is a warning log from PowerShell script"
            Write-KrJsonResponse -InputObject $payload -StatusCode 200
            """,
            ScriptLanguage.PowerShell);



server.AddMapRoute("/ps/bson",
            HttpVerb.Get,
            """
            Write-Output "Hello from PowerShell script! - Bson Response"
            # Payload
            $payload = @{
                Body           = "Hello from PowerShell script! - Bson Response"
                RequestQuery   = $Context.Request.Query
                RequestHeaders = $Context.Request.Headers
                RequestMethod  = $Context.Request.Method
                RequestPath    = $Context.Request.Path
                # If you want to return the request body, uncomment the next line
                RequestBody    = $Context.Request.Body
            }
            Write-KrBsonResponse -inputObject $payload -statusCode 200
            """,
            ScriptLanguage.PowerShell);

server.AddMapRoute("/ps/cbor",
            HttpVerb.Get,
            """            
            Write-Output "Hello from PowerShell script! - Cbor Response"
            $payload = @{
                Body           = "Hello from PowerShell script! - Cbor Response"
                RequestQuery   = $Context.Request.Query
                RequestHeaders = $Context.Request.Headers
                RequestMethod  = $Context.Request.Method
                RequestPath    = $Context.Request.Path
                # If you want to return the request body, uncomment the next line
                RequestBody    = $Context.Request.Body
            }
            Write-KrCborResponse -inputObject $payload -statusCode 200
            """,
            ScriptLanguage.PowerShell);

server.AddMapRoute("/ps/yaml", HttpVerb.Get, """
            Write-Output "Hello from PowerShell script! - Yaml Response(From C#)" 
            $payload = @{
                Body           = "Hello from PowerShell script!"
                RequestQuery   = $Context.Request.Query
                RequestHeaders = $Context.Request.Headers
                RequestMethod  = $Context.Request.Method
                RequestPath    = $Context.Request.Path
                # If you want to return the request body, uncomment the next line
                RequestBody    = $Context.Request.Body 
                
            } 
            Write-KrYamlResponse -inputObject $payload -statusCode 200
        """, ScriptLanguage.PowerShell);

server.AddMapRoute("/ps/xml", HttpVerb.Get, """
            Write-Output "Hello from PowerShell script! - Xml Response(From C#)" 
            $payload = @{
                Body           = "Hello from PowerShell script!"
                RequestQuery   = $Context.Request.Query
                RequestHeaders = $Context.Request.Headers
                RequestMethod  = $Context.Request.Method
                RequestPath    = $Context.Request.Path
                # If you want to return the request body, uncomment the next line
                RequestBody    = $Context.Request.Body 
                
            } 
            Write-KrXmlResponse -inputObject $payload -statusCode 200
        """, ScriptLanguage.PowerShell);

server.AddMapRoute("/ps/csv", HttpVerb.Get, """
            Write-Output "Hello from PowerShell script! - Csv Response(From C#)" 
            $payload = @{
                Body           = "Hello from PowerShell script!"
                RequestQuery   = $Context.Request.Query
                RequestHeaders = $Context.Request.Headers
                RequestMethod  = $Context.Request.Method
                RequestPath    = $Context.Request.Path
                # If you want to return the request body, uncomment the next line
                RequestBody    = $Context.Request.Body 
                
            } 
            Write-KrCsvResponse -inputObject $payload -statusCode 200
        """, ScriptLanguage.PowerShell);

server.AddMapRoute("/ps/text", HttpVerb.Get, """        
            Write-Output "Hello from PowerShell script! - Text Response(From C#)" 
            $payload = @{
                Body           = "Hello from PowerShell script!"
                RequestQuery   = $Context.Request.Query
                RequestHeaders = $Context.Request.Headers
                RequestMethod  = $Context.Request.Method
                RequestPath    = $Context.Request.Path
                # If you want to return the request body, uncomment the next line
                RequestBody    = $Context.Request.Body 
                
            } |Format-Table| Out-String
            Write-KrTextResponse -inputObject $payload -statusCode 200        
        """, ScriptLanguage.PowerShell);

server.AddMapRoute("/ps/stream/binary", HttpVerb.Get, """        
                Write-Output 'Hello from PowerShell script! - stream Binary file Response'
                Write-KrFileResponse -FilePath '../Files/LargeFiles/2GB.bin' -statusCode 200      
            """, ScriptLanguage.PowerShell);


server.AddMapRoute("/ps/stream/text", HttpVerb.Get, """        
                Write-Output 'Hello from PowerShell script! - stream Text file Response'
                Write-KrFileResponse -FilePath '../Files/LargeFiles/2GB.txt' -statusCode 200      
            """, ScriptLanguage.PowerShell);

server.AddMapRoute("/hello-ps", HttpVerb.Get, """
            $Context.Response.ContentType = 'text/plain'
            $Context.Response.Body = "Hello from PowerShell at $(Get-Date -Format o)"
        """, ScriptLanguage.PowerShell);

server.AddMapRoute("/hello-cs", HttpVerb.Get, """
            Context.Response.ContentType = "text/plain";
            Context.Response.Body = $"Hello from C# at {DateTime.Now:O}";
        """, ScriptLanguage.CSharp);


server.AddMapRoute("/cs/json", HttpVerb.Get, """
            Console.WriteLine("Hello from C# script! - Json Response(From C#)");
            var payload = new
            {
                Body = "Hello from C# script! - Json Response",
                RequestQuery = Context.Request.Query,
                RequestHeaders = Context.Request.Headers,
                RequestMethod = Context.Request.Method,
                RequestPath = Context.Request.Path,
                RequestBody = Context.Request.Body
            };

            Context.Response.WriteJsonResponse( payload,  200);

        """, ScriptLanguage.CSharp);

server.AddMapRoute("/cs/bson",
            HttpVerb.Get,
            """
            Console.WriteLine("Hello from C# script! - Bson Response(From C#)");
            var payload = new
            {
                Body = "Hello from C# script! - Bson Response",
                RequestQuery = Context.Request.Query,
                RequestHeaders = Context.Request.Headers,
                RequestMethod = Context.Request.Method,
                RequestPath = Context.Request.Path,
                RequestBody = Context.Request.Body
            };
            Context.Response.WriteBsonResponse( payload,  200);
            """,
            ScriptLanguage.CSharp);

server.AddMapRoute("/cs/csv",
            HttpVerb.Get,
            """
            Console.WriteLine("Hello from C# script! - Csv Response(From C#)");
            var payload = new
            {
                Body = "Hello from C# script! - Bson Response",
                RequestQuery = Context.Request.Query,
                RequestHeaders = Context.Request.Headers,
                RequestMethod = Context.Request.Method,
                RequestPath = Context.Request.Path,
                RequestBody = Context.Request.Body
            };
            Context.Response.WriteCsvResponse( payload,  200);
            """,
            ScriptLanguage.CSharp);


server.AddMapRoute("/cs/cbor",
            HttpVerb.Get,
            """ 
            Console.WriteLine("Hello from C# script! - Cbor Response(From PowerShell)");
            var payload = new
            {
                Body = "Hello from C# script! - Cbor Response",
                RequestQuery = Context.Request.Query,
                RequestHeaders = Context.Request.Headers,
                RequestMethod = Context.Request.Method,
                RequestPath = Context.Request.Path,
                RequestBody = Context.Request.Body
            };
            Context.Response.WriteCborResponse( payload,  200);
            """,
            ScriptLanguage.CSharp);

server.AddMapRoute("/cs/yaml", HttpVerb.Get, """
            Console.WriteLine("Hello from C# script! - Yaml Response(From C#)");
            var payload = new
            {
                Body = "Hello from C# script! - Yaml Response",
                RequestQuery = Context.Request.Query,
                RequestHeaders = Context.Request.Headers,
                RequestMethod = Context.Request.Method,
                RequestPath = Context.Request.Path,
                RequestBody = Context.Request.Body
            };

            Context.Response.WriteYamlResponse( payload,  200,
                contentType: "text/yaml" );
            Context.Response.ContentDisposition.Type = ContentDispositionType.Inline;
            Context.Response.ContentDisposition.FileName = "response.yaml";
        """, ScriptLanguage.CSharp);

server.AddMapRoute("/cs/xml", HttpVerb.Get, """
            Console.WriteLine("Hello from C# script! - Xml Response(From C#)");
            var payload = new
            {
                Body = "Hello from C# script! - Yaml Response",
                RequestQuery = Context.Request.Query,
                RequestHeaders = Context.Request.Headers,
                RequestMethod = Context.Request.Method,
                RequestPath = Context.Request.Path,
                RequestBody = Context.Request.Body
            };
            Context.Response.WriteXmlResponse( payload,  200);
        """, ScriptLanguage.CSharp);

server.AddMapRoute("/cs/text", HttpVerb.Get, """
            Console.WriteLine("Hello from C# script! - Text Response(From C#)");
            var payload = new
            {
                Body = "Hello from C# script! - Text Response",
                RequestQuery = Context.Request.Query,
                RequestHeaders = Context.Request.Headers,
                RequestMethod = Context.Request.Method,
                RequestPath = Context.Request.Path,
                RequestBody = Context.Request.Body
            };

            Context.Response.WriteTextResponse( payload,  200);

        """, ScriptLanguage.CSharp);


server.AddMapRoute("/cs/stream/binary", HttpVerb.Get, """        
                Console.WriteLine("Hello from C# script! - stream Binaryfile Response(From C#)");
              Context.Response.WriteFileResponse(filePath: "../Files/LargeFiles/2GB.bin", contentType: "application/octet-stream", statusCode: 200);
            """, ScriptLanguage.CSharp);

server.AddMapRoute("/cs/stream/text", HttpVerb.Get, """        
                Console.WriteLine("Hello from C# script! - stream Text file Response(From C#)");
               Context.Response.WriteFileResponse(filePath: "../Files/LargeFiles/2GB.txt", contentType: "text/plain", statusCode: 200);
            """, ScriptLanguage.CSharp);

server.AddMapRoute("/cs/file", HttpVerb.Get, """
                Console.WriteLine("Hello from C# script! - file Response(From C#)");
                Context.Response.WriteFileResponse("..\\..\\..\\README.md", null, 200);
""", ScriptLanguage.CSharp);



server.AddMapRoute("/compiled", HttpVerb.Get, async (ctx) =>
{
    await ctx.Response.WriteJsonResponseAsync(new { ok = true, message = "Native C# works!" });
});

server.AddMapRoute("/compiled/stream", HttpVerb.Get, async (ctx) =>
{
    ctx.Response.WriteFileResponse(filePath: "../Files/LargeFiles/2GB.bin", contentType: "application/octet-stream", statusCode: 200);
    await Task.CompletedTask; // Ensure the method is async
});



server.AddMapRoute("/status", HttpVerb.Get, """
    $html = @'
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Server Status</title>
</head>
<body>
  <h1>Status for {{ApplicationName}}</h1>
  <ul>
    <li>Path: {{Request.Path}}</li>
    <li>Method: {{Request.Method}}</li>
    <li>Time: {{Timestamp}}</li>
    <li>Hits: {{Visits}}</li>
  </ul>
</body>
</html>
'@

    Expand-KrObject -inputObject $Context  -Label "Context" 

    Expand-KrObject -inputObject $Context.Request  -Label "Request" 

    Write-KrHtmlResponse -Template $html -Variables @{
        ApplicationName = $server.ApplicationName
        Request         = $Context.Request
        Timestamp       = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
        Visits          = Get-Random
    } 
""", ScriptLanguage.PowerShell);


/* VB.NET Route */
server.AddMapRoute("/vb/hello", HttpVerb.Get, """
     
     Await Response.WriteTextResponseAsync(
     "Hello from VB.NET!" & vbCrLf & 
     "Time: " & Now.ToString(), 200)
""", ScriptLanguage.VBNet);

server.AddMapRoute("/vb/text", HttpVerb.Get, """
    Console.WriteLine("Hello from VB.NET script! - Text Response (From VB.NET)")

    Dim payload = New With {
        .Body = "Hello from VB.NET script! - Text Response",
        .RequestQuery = Context.Request.Query,
        .RequestHeaders = Context.Request.Headers,
        .RequestMethod = Context.Request.Method,
        .RequestPath = Context.Request.Path,
        .RequestBody = Context.Request.Body
    }

    Await Context.Response.WriteTextResponseAsync(payload, 200)
""", ScriptLanguage.VBNet);


server.AddMapRoute("/vb/xml", HttpVerb.Get, """
    Console.WriteLine("Hello from VB.NET script! - Xml Response(From VB.NET)")

    Dim payload = New With {
        .Body = "Hello from VB.NET script! - Xml Response",
        .RequestQuery = Context.Request.Query,
        .RequestHeaders = Context.Request.Headers,
        .RequestMethod = Context.Request.Method,
        .RequestPath = Context.Request.Path,
        .RequestBody = Context.Request.Body
    }
    
    Await Context.Response.WriteXmlResponseAsync(payload, 200)
""", ScriptLanguage.VBNet);

server.AddMapRoute("/vb/yaml", HttpVerb.Get, """
    Console.WriteLine("Hello from VB.NET script! - Yaml Response(From VB.NET)")

    Dim payload = New With {
        .Body = "Hello from VB.NET script! - Yaml Response",
        .RequestQuery = Context.Request.Query,
        .RequestHeaders = Context.Request.Headers,
        .RequestMethod = Context.Request.Method,
        .RequestPath = Context.Request.Path,
        .RequestBody = Context.Request.Body
    }

    Await Context.Response.WriteYamlResponseAsync(payload, 200)
""", ScriptLanguage.VBNet);

server.AddMapRoute("/vb/json", HttpVerb.Get, """
    Console.WriteLine("Hello from VB.NET script! - Json Response(From VB.NET)")

    Dim payload = New With {
        .Body = "Hello from VB.NET script! - Json Response",
        .RequestQuery = Context.Request.Query,
        .RequestHeaders = Context.Request.Headers,
        .RequestMethod = Context.Request.Method,
        .RequestPath = Context.Request.Path,
        .RequestBody = Context.Request.Body
    }

    Await Context.Response.WriteJsonResponseAsync(payload, 200)
""", ScriptLanguage.VBNet);

await server.RunUntilShutdownAsync(
    consoleEncoding: Encoding.UTF8,
    onStarted: () => Console.WriteLine("Server ready 🟢"),
    onShutdownError: ex => Console.WriteLine($"Shutdown error: {ex.Message}")
);
