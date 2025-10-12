<#
    Create a SignalR demo server with Kestrun in PowerShell.
    FileName: 10.5-SignalR.ps1
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Initialize Kestrun root directory
Initialize-KrRoot -Path $PSScriptRoot

## 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'SignalRDemo' -SetAsDefault

## 2. Create Server
New-KrServer -Name 'Kestrun SignalR Demo'

## 3. Configure Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -Protocol Http1AndHttp2

## 4. Add SignalR with KestrunHub
Add-KrSignalRHubMiddleware -Path '/runtime'

## 5. Enable Configuration
Enable-KrConfiguration

## 6. Add Routes

# Home page with SignalR client
Add-KrHtmlTemplateRoute -Pattern '/' -HtmlTemplatePath "Assets/wwwroot/signal-r.html"

# Route to broadcast logs via PowerShell
Add-KrMapRoute -Verbs Get -Pattern '/api/ps/log/{level}' {
    $level = Get-KrRequestRouteParam -Name 'level'
    Write-KrLog -Level $level -Message "Test $level message from PowerShell at $(Get-Date -Format 'HH:mm:ss')"
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
        param($Server)
        for ($i = 1; $i -le 10; $i++) {
            Start-Sleep -Seconds 5
            # Note: This is just an example. In production, you'd need proper access to the server instance.
            Write-Host "Monitor tick $i"
        }
    } -ArgumentList ($KrServer)

    Write-KrJsonResponse -InputObject @{
        Message = 'Background monitor started'
        JobId = $monitorJob.Id
    } -StatusCode 200
}

## 7. Start Server

Write-Host '🟢 Kestrun SignalR Demo Server Started' -ForegroundColor Green
Write-Host '📍 Navigate to http://localhost:5000 to see the demo' -ForegroundColor Cyan
Write-Host '🔌 SignalR Hub available at: http://localhost:5000/runtime' -ForegroundColor Cyan

Start-KrServer -CloseLogsOnExit
