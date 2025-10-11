param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
<#
    .SYNOPSIS
        Kestrun PowerShell Example: SignalR Real-time Communication
    .DESCRIPTION
        This script demonstrates how to use SignalR with Kestrun for real-time bidirectional communication.
        It shows how to broadcast log messages and custom events to connected clients.
    .EXAMPLE
        .\SignalRDemo.ps1
        
        # Then open http://localhost:5000 in your browser to see the real-time demo
        
        # Or test the API endpoints:
        Invoke-RestMethod -Uri "http://localhost:5000/api/ps/log/Information"
        Invoke-RestMethod -Uri "http://localhost:5000/api/ps/log/Warning"
        Invoke-RestMethod -Uri "http://localhost:5000/api/ps/log/Error"
        Invoke-RestMethod -Uri "http://localhost:5000/api/ps/event"
#>

## 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'SignalRDemo' -SetAsDefault

## 2. Create Server
New-KrServer -Name 'Kestrun SignalR Demo'

## 3. Configure Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -Protocol Http1AndHttp2

## 4. Add SignalR with KestrunHub
Add-KrSignalRHubMiddleware -HubType ([Kestrun.SignalR.KestrunHub]) -Path '/runtime'

## 5. Enable Configuration
Enable-KrConfiguration

## 6. Add Routes

# Home page with SignalR client
Add-KrMapRoute -Verbs Get -Pattern '/' {
    $html = @'
<!DOCTYPE html>
<html>
<head>
    <title>Kestrun SignalR Demo - PowerShell</title>
    <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.7/dist/browser/signalr.min.js"></script>
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
    <h1>🚀 Kestrun SignalR Demo (PowerShell)</h1>
    <p>Connected to SignalR hub at <code>/runtime</code></p>
    
    <div>
        <button onclick="sendLog('Information')">Send Info Log</button>
        <button onclick="sendLog('Warning')">Send Warning Log</button>
        <button onclick="sendLog('Error')">Send Error Log</button>
        <button onclick="sendEvent()">Send Custom Event</button>
        <button onclick="clearMessages()">Clear Messages</button>
    </div>
    
    <h2>Real-time Messages:</h2>
    <div id="messages"></div>
    
    <script>
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/runtime")
            .withAutomaticReconnect()
            .build();
        
        const messagesDiv = document.getElementById("messages");
        
        function addMessage(message, cssClass = "") {
            const entry = document.createElement("div");
            entry.className = cssClass;
            entry.innerHTML = message;
            messagesDiv.appendChild(entry);
            messagesDiv.scrollTop = messagesDiv.scrollHeight;
        }
        
        connection.on("ReceiveLog", (data) => {
            const timestamp = new Date(data.timestamp).toLocaleTimeString();
            const logClass = `log-entry log-${data.level}`;
            addMessage(`<span class="timestamp">[${timestamp}]</span> <strong>${data.level}:</strong> ${data.message}`, logClass);
        });
        
        connection.on("ReceiveEvent", (data) => {
            const timestamp = new Date(data.timestamp).toLocaleTimeString();
            addMessage(`<span class="timestamp">[${timestamp}]</span> <strong>Event:</strong> ${data.eventName} - ${JSON.stringify(data.data)}`, "event-entry");
        });
        
        connection.start()
            .then(() => addMessage("✅ Connected to SignalR hub!", "log-entry log-Information"))
            .catch(err => addMessage(`❌ Connection error: ${err}`, "log-entry log-Error"));
        
        async function sendLog(level) {
            const response = await fetch(`/api/ps/log/${level}`);
            const text = await response.text();
            console.log(text);
        }
        
        async function sendEvent() {
            const response = await fetch("/api/ps/event");
            const text = await response.text();
            console.log(text);
        }
        
        function clearMessages() {
            messagesDiv.innerHTML = "";
        }
    </script>
</body>
</html>
'@
    Write-KrHtmlResponse -InputObject $html -StatusCode 200
}

# Route to broadcast logs via PowerShell
Add-KrMapRoute -Verbs Get -Pattern '/api/ps/log/{level}' {
    $level = Get-KrRequestRouteParam -Name 'level'
    Send-KrLog -Level $level -Message "Test $level message from PowerShell at $(Get-Date -Format 'HH:mm:ss')"
    Write-KrTextResponse -InputObject "Broadcasted $level log message from PowerShell" -StatusCode 200
}

# Route to broadcast custom events via PowerShell
Add-KrMapRoute -Verbs Get -Pattern '/api/ps/event' {
    Send-KrEvent -EventName 'PowerShellEvent' -Data @{
        Message   = 'Hello from PowerShell'
        Timestamp = (Get-Date)
        Random    = Get-Random -Minimum 1 -Maximum 100
    }
    Write-KrTextResponse -InputObject 'Broadcasted custom event from PowerShell' -StatusCode 200
}

# Background task that broadcasts periodic updates
Add-KrMapRoute -Verbs Get -Pattern '/api/start-monitor' {
    # This route starts a background monitoring task
    $monitorJob = Start-Job -ScriptBlock {
        param($Server)
        for ($i = 1; $i -le 10; $i++) {
            Start-Sleep -Seconds 5
            # Note: This is just an example. In production, you'd need proper access to the server instance.
            Write-Host "Monitor tick $i"
        }
    } -ArgumentList (Get-KrServer)
    
    Write-KrJsonResponse -InputObject @{
        Message = 'Background monitor started'
        JobId   = $monitorJob.Id
    } -StatusCode 200
}

## 7. Start Server
Start-KrServer -OnStarted {
    Write-Host '🟢 Kestrun SignalR Demo Server Started' -ForegroundColor Green
    Write-Host '📍 Navigate to http://localhost:5000 to see the demo' -ForegroundColor Cyan
    Write-Host '🔌 SignalR Hub available at: http://localhost:5000/runtime' -ForegroundColor Cyan
}
