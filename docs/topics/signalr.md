---
title: SignalR Real-time Communication
parent: Guides
nav_order: 40
---

# SignalR Real-time Communication

Kestrun provides native SignalR integration for real-time, bidirectional communication
between the server and connected clients. This enables live event streaming, push
notifications, and instant updates—perfect for dashboards, log viewers, long‑running task
progress, and collaborative apps.

## Architecture Overview

| Component | Purpose | Notes |
|-----------|---------|-------|
| `KestrunHub` | Default SignalR hub for Kestrun | Handles client connections, disconnections, and group management. |
| `IRealtimeBroadcaster` | Service interface for broadcasting messages | Allows PowerShell and C# routes to broadcast logs, events, and messages to connected clients. |
| `RealtimeBroadcaster` | Default implementation of `IRealtimeBroadcaster` | Registered as singleton when the hub middleware is added. |
| `Send-KrSignalRLog` | PowerShell cmdlet for broadcasting log messages | Sends log messages to all connected SignalR clients. |
| `Send-KrSignalREvent` | PowerShell cmdlet for broadcasting custom events | Sends custom events with data to all connected clients. |
| `Send-KrSignalRGroupMessage` | PowerShell cmdlet for group targeting | Sends a method invocation with a payload to a specific group. |

## Adding SignalR to Your Server

### PowerShell

```powershell
# Create server
New-KrServer -Name 'My SignalR Server'

# Add endpoint (HTTP/1.1 or HTTP/2 are both supported)
Add-KrEndpoint -Port 5000 -IPAddress ([IPAddress]::Loopback)

# Add SignalR hub at /hubs/kestrun (default hub type)
Add-KrSignalRHubMiddleware -Path '/hubs/kestrun'

# Enable configuration
Enable-KrConfiguration
```

### C# (C Sharp)

```csharp
using Kestrun.Hosting;
using Kestrun.SignalR;

var server = new KestrunHost("My SignalR Server", Directory.GetCurrentDirectory());

server.ConfigureListener(
    port: 5000,
    ipAddress: IPAddress.Any,
    protocols: HttpProtocols.Http1AndHttp2  // HTTP/1.1 and HTTP/2 are supported
);

// Add SignalR with KestrunHub
server.AddSignalR<KestrunHub>("/hubs/kestrun");

server.EnableConfiguration();
await server.RunUntilShutdownAsync();
```

## Broadcasting Messages

### From PowerShell Routes

```powershell
# Broadcast a log message
Add-KrMapRoute -Verbs Get -Pattern '/api/ps/log/{level}' {
    $level = Get-KrRequestRouteParam -Name 'level'
    Send-KrSignalRLog -Level $level -Message "Event occurred at $(Get-Date -Format 'HH:mm:ss')"
    Write-KrTextResponse -InputObject "Log broadcasted" -StatusCode 200
}

# Broadcast a custom event
Add-KrMapRoute -Verbs Get -Pattern '/api/ps/event' {
    Send-KrSignalREvent -EventName 'PowerShellEvent' -Data @{
        Title = 'System Update'
        Message = 'New features available'
        Timestamp = (Get-Date)
    }
    Write-KrTextResponse -InputObject "Event broadcasted" -StatusCode 200
}
```

### From C# Routes

```csharp
server.AddMapRoute("/api/log", HttpVerb.Get, """
    var broadcaster = Context.RequestServices.GetService(typeof(Kestrun.SignalR.IRealtimeBroadcaster))
        as Kestrun.SignalR.IRealtimeBroadcaster;

    if (broadcaster != null)
    {
        await broadcaster.BroadcastLogAsync("Information", "Hello from C#", Context.RequestAborted);
        Context.Response.WriteTextResponse("Broadcasted", 200);
    }
""", ScriptLanguage.CSharp);
```

## Client-Side Integration

### JavaScript/Browser Client

```html
<!DOCTYPE html>
<html>
<head>
    <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.7/dist/browser/signalr.min.js"></script>
</head>
<body>
    <div id="messages"></div>

    <script>
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/kestrun")
            .withAutomaticReconnect()
            .build();

        // Listen for log messages
        connection.on("ReceiveLog", (data) => {
            console.log(`[${data.level}] ${data.message}`, data.timestamp);
            document.getElementById("messages").innerHTML +=
                `<div>${data.level}: ${data.message}</div>`;
        });

        // Listen for custom events
        connection.on("ReceiveEvent", (data) => {
            console.log(`Event: ${data.eventName}`, data.data, data.timestamp);
            document.getElementById("messages").innerHTML +=
                `<div>Event: ${data.eventName} - ${JSON.stringify(data.data)}</div>`;
        });

        // Start connection
        connection.start()
            .then(() => console.log("Connected to SignalR hub"))
            .catch(err => console.error("Connection error:", err));
    </script>
</body>
</html>
```

### PowerShell Client

```powershell
# Install SignalR client (if not already installed)
Install-Package Microsoft.AspNetCore.SignalR.Client

# Connect to hub
$connection = [Microsoft.AspNetCore.SignalR.Client.HubConnectionBuilder]::new()
    .WithUrl("http://localhost:5000/hubs/kestrun")
    .WithAutomaticReconnect()
    .Build()

# Register event handlers
$connection.On("ReceiveLog", [Action[object]]{
    param($data)
    Write-Host "[$($data.level)] $($data.message)" -ForegroundColor Cyan
})

$connection.On("ReceiveEvent", [Action[object]]{
    param($data)
    Write-Host "Event: $($data.eventName)" -ForegroundColor Green
    $data.data | ConvertTo-Json | Write-Host
})

# Start connection
$connection.StartAsync().Wait()
Write-Host "Connected to SignalR hub"

# Keep alive
Read-Host "Press Enter to disconnect"
$connection.StopAsync().Wait()
```

## Hub Methods

The `KestrunHub` provides several built-in methods that clients can invoke:

### JoinGroup / LeaveGroup

```javascript
// Join a specific group
await connection.invoke("JoinGroup", "admins");

// Leave a group
await connection.invoke("LeaveGroup", "admins");
```

### Echo

```javascript
// Echo test
connection.on("ReceiveEcho", (message) => {
    console.log("Echo:", message);
});

await connection.invoke("Echo", "Hello, server!");
```

## Broadcasting to Groups

```powershell
# PowerShell: Broadcast to specific group
Add-KrMapRoute -Verbs Post -Pattern '/api/group/broadcast/{group}' {
    $groupName = Get-KrRequestRouteParam -Name 'group'
    $broadcaster = $Server.App.Services.GetService([Kestrun.SignalR.IRealtimeBroadcaster])

    if ($broadcaster) {
        Send-KrSignalRGroupMessage -GroupName $groupName -Method 'ReceiveGroupMessage' -Message @{ Message = 'Group notification' }
        Write-KrTextResponse -InputObject "Sent to group: $groupName" -StatusCode 200
    }
}
```

## Message Formats

### ReceiveLog

Clients receive log messages in this format:

```json
{
  "level": "Information",
  "message": "Server started successfully",
  "timestamp": "2025-10-11T21:30:00.123Z"
}
```

### ReceiveEvent

Clients receive custom events in this format:

```json
{
  "eventName": "UserLoggedIn",
  "data": {
    "username": "admin",
    "timestamp": "2025-10-11T21:30:00.123Z"
  },
  "timestamp": "2025-10-11T21:30:00.123Z"
}
```

## Use Cases

### Real-time Log Streaming

Stream server logs to a web dashboard:

```powershell
# Configure Serilog to broadcast logs via SignalR
$logger = New-KrLogger |
    Add-KrSinkConsole |
    Add-KrSinkSignalR |  # Custom sink that uses Send-KrSignalRLog
    Register-KrLogger -SetAsDefault

# Now all logs are automatically broadcast to connected clients
Write-KrLog -Level Information -Message "This will appear in real-time on the dashboard"
```

### Progress Monitoring

Monitor long-running operations:

```powershell
Add-KrMapRoute -Verbs Post -Pattern '/api/operation/start' {
    $seconds = Get-KrRequestQuery -Name 'seconds' -AsInt
    if ($seconds -le 0) { $seconds = 2 }

    $id = New-KrTask -ScriptBlock {
        for ($i = 1; $i -le $seconds; $i++) {
            Start-Sleep -Milliseconds 1000
            $TaskProgress.StatusMessage = "Sleeping ($i/$seconds)"
            $TaskProgress.PercentComplete = ($i * 100/$seconds)
            Send-KrSignalREvent -EventName 'OperationProgress' -Data @{ TaskId = $TaskId; Progress = $TaskProgress.PercentComplete; Step = $i }
        }
        Send-KrSignalREvent -EventName 'OperationComplete' -Data @{ TaskId = $TaskId; Progress = $TaskProgress.PercentComplete; Status = $TaskProgress.StatusMessage }
    } -Arguments @{ seconds = $seconds } -AutoStart

    Write-KrJsonResponse -InputObject @{ Success = $true; TaskId = $id } -StatusCode 200
}
```

### Live System Metrics

Broadcast system metrics periodically:

```powershell
# Background task that sends metrics every 5 seconds
$timer = New-Object System.Timers.Timer
$timer.Interval = 5000
$timer.AutoReset = $true
$timer.Add_Elapsed({
    $metrics = @{
        CpuPercent = (Get-Counter '\Processor(_Total)\% Processor Time').CounterSamples.CookedValue
        MemoryMB = [Math]::Round((Get-Process -Id $PID).WorkingSet64 / 1MB, 2)
        Timestamp = (Get-Date)
    }
    Send-KrSignalREvent -EventName 'SystemMetrics' -Data $metrics
})
$timer.Start()
```

## Security Considerations

### Authentication

SignalR hubs can be secured with authentication:

```csharp
// Add authentication to the hub
[Authorize]
public class SecureKestrunHub : Hub
{
    // Hub implementation
}

// Register secured hub
server.AddSignalR<SecureKestrunHub>("/secure-runtime");
```

### Authorization Policies

Apply authorization policies:

```csharp
[Authorize(Policy = "AdminOnly")]
public class AdminHub : Hub
{
    // Only accessible to users with AdminOnly policy
}
```

## External References

- SignalR overview (Microsoft Docs):
    <https://learn.microsoft.com/aspnet/core/signalr/introduction>
- SignalR JavaScript client guide:
    <https://learn.microsoft.com/aspnet/core/signalr/javascript-client>
- Server-Sent Events (SSE) overview:
    <https://developer.mozilla.org/docs/Web/API/Server-sent_events/Using_server-sent_events>
- SSE vs WebSockets:
    <https://ably.com/topic/sse-vs-websockets>

## Troubleshooting

### Connection Issues

1. **Ensure HTTP/2 is enabled**: SignalR requires HTTP/1.1 or HTTP/2
2. **Check CORS settings**: If connecting from different origins, configure CORS
3. **Verify hub path**: Ensure the client connects to the correct hub path (e.g., `/hubs/kestrun`)
4. **Check firewall rules**: Ensure the port is accessible

### Message Not Received

1. **Verify IRealtimeBroadcaster is registered**: Only registered when using `KestrunHub`
2. **Check client event handlers**: Ensure client is listening for the correct event names
3. **Review server logs**: Check for errors during broadcast

## Examples

Complete working examples are available in the repository:

- Tutorial: [Middleware > SignalR](/pwsh/tutorial/10.middleware/5.SignalR)
- PowerShell sample: [`docs/_includes/examples/pwsh/10.5-SignalR.ps1`](https://github.com/Kestrun/Kestrun/blob/main/docs/_includes/examples/pwsh/10.5-SignalR.ps1)

Both examples include a full HTML/JavaScript client with real-time updates.

## API Reference

### PowerShell Cmdlets

- `Add-KrSignalRHubMiddleware`: Registers a SignalR hub at the specified path
- `Send-KrSignalRLog`: Broadcasts a log message to all connected clients
- `Send-KrSignalREvent`: Broadcasts a custom event to all connected clients

### C# Classes

- `KestrunHub`: Default SignalR hub implementation
- `IRealtimeBroadcaster`: Interface for broadcasting messages
- `RealtimeBroadcaster`: Default broadcaster implementation
