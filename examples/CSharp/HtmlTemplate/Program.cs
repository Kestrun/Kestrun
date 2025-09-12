using System.Collections;
using System.Net;
using Serilog;
using Kestrun.Logging;
using Kestrun.Utilities;
using Kestrun.SharedState;
using System.Text;
using Kestrun.Hosting;   // Only for writing the CSR key


var currentDir = Directory.GetCurrentDirectory();
new LoggerConfiguration()
      .MinimumLevel.Debug()
      .WriteTo.File("logs/HtmlTemplate.log", rollingInterval: RollingInterval.Day)
      .Register("Audit", setAsDefault: true);

// 1. Create server 

var server = new KestrunHost("Kestrun HtmlTemplate", currentDir);
// Set Kestrel options
server.Options.ServerOptions.AllowSynchronousIO = false;
server.Options.ServerOptions.AddServerHeader = false; // DenyServerHeader

server.Options.ServerLimits.MaxRequestBodySize = 10485760;
server.Options.ServerLimits.MaxConcurrentConnections = 100;
server.Options.ServerLimits.MaxRequestHeaderCount = 100;
server.Options.ServerLimits.KeepAliveTimeout = TimeSpan.FromSeconds(120);


// 3. Configure listeners
server.ConfigureListener(
    port: 5000,
    ipAddress: IPAddress.Any,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1
);

// Create a shared state variable to track visits
var sharedVisits = new Hashtable
{
    ["Count"] = 0
};
// 3.1 Inject global variable
if (!SharedStateStore.Set("Visits", sharedVisits))
{
    Console.WriteLine("Failed to define global variable 'Visits'.");
    Environment.Exit(1);
}

server.EnableConfiguration();
// 4. Add routes

server.AddHtmlTemplateRoute(
            pattern: "/status",
            htmlFilePath: Path.Combine(currentDir ?? ".", "Pages", "status.html")
        );



server.AddMapRoute("/visit", HttpVerb.Get, async (ctx) =>
{
    _ = SharedStateStore.TryGet("Visits", out Hashtable? visits);

    // Increment visit count and return response
    if (visits != null && visits.ContainsKey("Count"))
    {
        visits["Count"] = ((visits["Count"] as int?) ?? 0) + 1; // Increment the visit count
        await ctx.Response.WriteTextResponseAsync($"Visits so far: {visits["Count"]}", 200);
    }
    else
    {
        await ctx.Response.WriteErrorResponseAsync("Visits variable not found.", 500, "text/plain");
    }
});

await server.RunUntilShutdownAsync(
    consoleEncoding: Encoding.UTF8,
    onStarted: () => Console.WriteLine("Server ready 🟢"),
    onShutdownError: ex => Console.WriteLine($"Shutdown error: {ex.Message}"

    )
);
