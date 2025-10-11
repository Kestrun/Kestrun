using System.Net;
using Serilog;
using Kestrun.Logging;
using Kestrun.Hosting;
using Kestrun.Scripting;
using Kestrun.SignalR;
using Kestrun.Utilities;
using System.Text;

var currentDir = Directory.GetCurrentDirectory();

// Configure logging
new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/signalr.log", rollingInterval: RollingInterval.Day)
    .Register("SignalRDemo", setAsDefault: true);

// 1. Create server
var server = new KestrunHost("Kestrun SignalR Demo", currentDir);

// Set Kestrel options
server.Options.ServerOptions.AllowSynchronousIO = false;
server.Options.ServerOptions.AddServerHeader = false;
server.Options.ServerLimits.MaxRequestBodySize = 10485760;
server.Options.ServerLimits.MaxConcurrentConnections = 100;

// 2. Configure listener
server.ConfigureListener(
    port: 5000,
    ipAddress: IPAddress.Any,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2
);

// 3. Add SignalR with KestrunHub
server.AddSignalR<KestrunHub>("/runtime");

// 4. Add PowerShell runtime
server.AddPowerShellRuntime();

// 5. Enable configuration
server.EnableConfiguration();

// 6. Add routes that demonstrate SignalR broadcasting

// Home page route with HTML (using script language)
server.AddMapRoute("/", HttpVerb.Get, """
    var html = @"
<!DOCTYPE html>
<html>
<head>
    <title>Kestrun SignalR Demo</title>
    <script src=\""https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.7/dist/browser/signalr.min.js\""></script>
    <style>
        body { font-family: Arial, sans-serif; max-width: 800px; margin: 50px auto; padding: 20px; }
        h1 { color: #333; }
        #messages { border: 1px solid #ddd; padding: 10px; min-height: 300px; max-height: 500px; overflow-y: auto; background: #f9f9f9; }
        .log-entry { margin: 5px 0; padding: 5px; border-left: 3px solid #ccc; }
        .log-Information { border-left-color: #2196F3; }
        .log-Warning { border-left-color: #FF9800; }
        .log-Error { border-left-color: #F44336; }
        .event-entry { margin: 5px 0; padding: 5px; background: #E8F5E9; border-left: 3px solid #4CAF50; }
        button { padding: 10px 20px; margin: 5px; cursor: pointer; }
        .timestamp { color: #666; font-size: 0.85em; }
    </style>
</head>
<body>
    <h1>🚀 Kestrun SignalR Demo</h1>
    <p>Connected to SignalR hub at <code>/runtime</code></p>
    
    <div>
        <button onclick=\""sendLog('Information')\"">Send Info Log</button>
        <button onclick=\""sendLog('Warning')\"">Send Warning Log</button>
        <button onclick=\""sendLog('Error')\"">Send Error Log</button>
        <button onclick=\""sendEvent()\"">Send Custom Event</button>
        <button onclick=\""clearMessages()\"">Clear Messages</button>
    </div>
    
    <h2>Real-time Messages:</h2>
    <div id=\""messages\""></div>
    
    <script>
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(\""/runtime\"")
            .withAutomaticReconnect()
            .build();
        
        const messagesDiv = document.getElementById(\""messages\"");
        
        function addMessage(message, cssClass = \""\"") {
            const entry = document.createElement(\""div\"");
            entry.className = cssClass;
            entry.innerHTML = message;
            messagesDiv.appendChild(entry);
            messagesDiv.scrollTop = messagesDiv.scrollHeight;
        }
        
        connection.on(\""ReceiveLog\"", (data) => {
            const timestamp = new Date(data.timestamp).toLocaleTimeString();
            const logClass = `log-entry log-${data.level}`;
            addMessage(`<span class=\""timestamp\"">[$\{timestamp}]</span> <strong>$\{data.level}:</strong> $\{data.message}`, logClass);
        });
        
        connection.on(\""ReceiveEvent\"", (data) => {
            const timestamp = new Date(data.timestamp).toLocaleTimeString();
            addMessage(`<span class=\""timestamp\"">[$\{timestamp}]</span> <strong>Event:</strong> $\{data.eventName} - $\{JSON.stringify(data.data)}`, \""event-entry\"");
        });
        
        connection.start()
            .then(() => addMessage(\""✅ Connected to SignalR hub!\"", \""log-entry log-Information\""))
            .catch(err => addMessage(`❌ Connection error: $\{err}`, \""log-entry log-Error\""));
        
        async function sendLog(level) {
            const response = await fetch(`/api/ps/log/$\{level}`);
            const text = await response.text();
            console.log(text);
        }
        
        async function sendEvent() {
            const response = await fetch(\""/api/ps/event\"");
            const text = await response.text();
            console.log(text);
        }
        
        function clearMessages() {
            messagesDiv.innerHTML = \""\"";
        }
    </script>
</body>
</html>
";
    Context.Response.WriteHtmlResponse(html, 200);
""", ScriptLanguage.CSharp);

// PowerShell route that broadcasts logs
server.AddMapRoute("/api/ps/log/{level}", HttpVerb.Get, """
    $level = Get-KrRequestRouteParam -Name 'level'
    Send-KrLog -Level $level -Message "Test $level message from PowerShell at $(Get-Date -Format 'HH:mm:ss')"
    Write-KrTextResponse -InputObject "Broadcasted $level log message from PowerShell" -StatusCode 200
""", ScriptLanguage.PowerShell);

// PowerShell route that broadcasts custom events
server.AddMapRoute("/api/ps/event", HttpVerb.Get, """
    Send-KrEvent -EventName 'PowerShellEvent' -Data @{ Message = 'Hello from PowerShell'; Timestamp = (Get-Date) }
    Write-KrTextResponse -InputObject 'Broadcasted custom event from PowerShell' -StatusCode 200
""", ScriptLanguage.PowerShell);

// 7. Start server
await server.RunUntilShutdownAsync(
    consoleEncoding: Encoding.UTF8,
    onStarted: () =>
    {
        Console.WriteLine("🟢 Kestrun SignalR Demo Server Started");
        Console.WriteLine("📍 Navigate to http://localhost:5000 to see the demo");
        Console.WriteLine("🔌 SignalR Hub available at: http://localhost:5000/runtime");
    },
    onShutdownError: ex => Console.WriteLine($"❌ Shutdown error: {ex.Message}")
);
