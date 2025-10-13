param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
<#
    .SYNOPSIS
        Kestrun PowerShell Example: Enhanced SignalR Real-time Communication
    .DESCRIPTION
        This script demonstrates advanced SignalR features with Kestrun including:
        - Real-time log broadcasting and custom events
        - Group management (join/leave groups, group-specific broadcasting)
        - Long-running operations with real-time progress updates
        - Scheduled background tasks with automated broadcasting
        - Multi-hub architecture with comprehensive client integration
    .EXAMPLE
        .\SignalRDemo.ps1

        # Then open http://localhost:5000 in your browser to see the enhanced demo

        # Test basic API endpoints:
        Invoke-RestMethod -Uri "http://localhost:5000/api/ps/log/Information"
        Invoke-RestMethod -Uri "http://localhost:5000/api/ps/event"

        # Test group features:
        Invoke-RestMethod -Uri "http://localhost:5000/api/group/join/Admins" -Method Post
        Invoke-RestMethod -Uri "http://localhost:5000/api/group/broadcast/Admins" -Method Post

        # Test long operations:
        Invoke-RestMethod -Uri "http://localhost:5000/api/operation/start" -Method Post
#>

## 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'SignalRDemo' -SetAsDefault

## 2. Create Server
New-KrServer -Name 'Kestrun SignalR Demo'

## 3. Configure Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -Protocol Http1AndHttp2

## 4. Add SignalR with KestrunHub
Add-KrSignalRHubMiddleware -HubType ([Kestrun.SignalR.KestrunHub]) -Path '/hubs/kestrun'

## 5. Enable Configuration
Enable-KrConfiguration

## 6. Add Routes

# Home page with enhanced SignalR client
Add-KrMapRoute -Verbs Get -Pattern '/' {
    # Read the enhanced HTML file from the docs directory
    $htmlPath = Join-Path (Get-KrRoot) 'docs\_includes\examples\pwsh\Assets\wwwroot\signal-r.html'

    if (Test-Path $htmlPath) {
        $html = Get-Content $htmlPath -Raw
        Write-KrHtmlResponse -Template $html -StatusCode 200
    } else {
        # Fallback simple HTML if file not found
        $html = @'
<!DOCTYPE html>
<html>
<head>
    <title>Kestrun Enhanced SignalR Demo</title>
    <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.7/dist/browser/signalr.min.js"></script>
</head>
<body>
    <h1>Enhanced SignalR Demo</h1>
    <p>HTML file not found at expected location. Please check the installation.</p>
    <p>Expected path: docs\_includes\examples\pwsh\Assets\wwwroot\signal-r.html</p>
</body>
</html>
'@
        Write-KrHtmlResponse -Template $html -StatusCode 500
    }
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
        Message = 'Hello from PowerShell'
        Timestamp = (Get-Date)
        Random = Get-Random -Minimum 1 -Maximum 100
    }
    Write-KrTextResponse -InputObject 'Broadcasted custom event from PowerShell' -StatusCode 200
}

# Background task that broadcasts periodic updates
Add-KrMapRoute -Verbs Get -Pattern '/api/start-monitor' {
    # This route starts a background monitoring task
    $monitorJob = Start-Job -ScriptBlock {
        for ($i = 1; $i -le 10; $i++) {
            Start-Sleep -Seconds 5
            # Note: This is just an example. In production, you'd need proper access to the server instance.
            Write-Host "Monitor tick $i"
        }
    }

    Write-KrJsonResponse -InputObject @{
        Message = 'Background monitor started'
        JobId = $monitorJob.Id
    } -StatusCode 200
}

## 7. Start Server
Write-Host '🟢 Kestrun SignalR Demo Server Starting...' -ForegroundColor Green
Write-Host '📍 Navigate to http://localhost:5000 to see the demo' -ForegroundColor Cyan
Write-Host '🔌 SignalR Hub available at: http://localhost:5000/hubs/kestrun' -ForegroundColor Cyan
Start-KrServer
