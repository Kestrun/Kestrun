<#
    Create a broadcast SSE demo server with Kestrun in PowerShell.
    FileName: 15.10-SseBroadcast.ps1

    This demo shows server-side broadcasting to all connected SSE clients.

    Broadcast SSE pieces:
    - Add-KrSseBroadcastMiddleware (registers broadcaster + maps /sse/broadcast)
    - Send-KrSseBroadcastEvent (broadcasts to all connected clients)

    Per-request SSE helpers (still available):
    - Start-KrSseResponse
    - Write-KrSseEvent
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

if (-not (Get-Module Kestrun)) { Import-Module Kestrun }

Initialize-KrRoot -Path $PSScriptRoot

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole | Register-KrLogger -Name 'SseBroadcastDemo' -SetAsDefault

New-KrServer -Name 'Kestrun SSE Broadcast Demo'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress

 

# Add the broadcast SSE endpoints (implemented in C#; keeps connections open)
# 1) Default broadcast SSE stream (schema defaults to string in OpenAPI)
Add-KrSseBroadcastMiddleware -Path '/sse/broadcast' -KeepAliveSeconds 15

# 2) Progress broadcast SSE stream (OpenAPI payload schema: OperationProgressEvent)
Add-KrSseBroadcastMiddleware -Path '/sse/broadcast/progress' -KeepAliveSeconds 15


Enable-KrConfiguration

# Home page
Add-KrHtmlTemplateRoute -Pattern '/' -HtmlTemplatePath 'Assets/wwwroot/sseBroadcast.html'

# Broadcast API
Add-KrMapRoute -Verbs Post -Pattern '/api/broadcast' {
    try {
        $body = Get-KrRequestBody
        $eventName = [string]($body.event ?? 'message')

        $dataObj = $body.data
        if ($null -eq $dataObj) { $dataObj = @{ text = 'empty' } }

        $dataJson = $dataObj | ConvertTo-Json -Compress

        Send-KrSseBroadcastEvent -Event $eventName -Data $dataJson

        $count = Get-KrSseConnectedClientCount

        Write-KrJsonResponse -InputObject @{ ok = $true; event = $eventName; connected = $count } -StatusCode 200
    } catch {
        Write-KrLog -Level Error -Exception $_.Exception -Message 'Send-KrSseBroadcastEvent failed: {error}' -Values $_.ToString()
        # Generate error response
        $err = [SseBroadcastErrorResponse]::new()
        $err.ok = $false
        $err.error = 'Sse Broadcast failed. See server logs for details.'
        Write-KrJsonResponse -InputObject $err -StatusCode 500
    }
}

# Progress Broadcast API
Add-KrMapRoute -Verbs Post -Pattern '/api/broadcast/progress' {
    try {
        $body = Get-KrRequestBody

        $payload = @{
            taskId = [string]($body.taskId ?? 'task-1')
            progress = [double]($body.progress ?? 0)
            status = [string]($body.status ?? 'Working...')
            state = [string]($body.state ?? 'running')
            ts = (Get-Date).ToUniversalTime()
        }

        $dataJson = $payload | ConvertTo-Json -Compress
        Send-KrSseBroadcastEvent -Event 'progress' -Data $dataJson

        $count = Get-KrSseConnectedClientCount

        Write-KrJsonResponse -InputObject @{ ok = $true; event = 'progress'; connected = $count } -StatusCode 200
    } catch {
        Write-KrLog -Level Error -Exception $_.Exception -Message 'Send-KrSseBroadcastEvent failed: {error}' -Values $_.ToString()
        # Generate error response
        $err = [SseBroadcastErrorResponse]::new()
        $err.ok = $false
        $err.error = 'Sse Broadcast failed. See server logs for details.'
        Write-KrJsonResponse -InputObject $err -StatusCode 500
    }
}

Write-Host '🟢 Kestrun SSE Broadcast Demo Server Started' -ForegroundColor Green
Write-Host "📍 Navigate to http://localhost:$Port" -ForegroundColor Cyan
Write-Host "📡 Broadcast SSE endpoint: http://localhost:$Port/sse/broadcast" -ForegroundColor Cyan
Write-Host "📈 Progress SSE endpoint: http://localhost:$Port/sse/broadcast/progress" -ForegroundColor Cyan

Start-KrServer -CloseLogsOnExit
