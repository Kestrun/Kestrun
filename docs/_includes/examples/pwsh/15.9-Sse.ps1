<#
    Create an SSE demo server with Kestrun in PowerShell.
    FileName: 15.9-Sse.ps1

    This demo focuses on the per-connection SSE helpers:
    - Start-KrSseResponse
    - Write-KrSseEvent
#>
param(
  [int]$Port = 5000,
  [IPAddress]$IPAddress = [IPAddress]::Loopback
)

if (-not (Get-Module Kestrun)) { Import-Module Kestrun }

# Initialize Kestrun root directory
Initialize-KrRoot -Path $PSScriptRoot

## 1. Logging
New-KrLogger |
  Set-KrLoggerLevel -Value Debug |
  Add-KrSinkConsole | Register-KrLogger -Name 'SseDemo' -SetAsDefault

## 2. Create Server
New-KrServer -Name 'Kestrun SSE Demo'

## 3. Configure Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

## 4. Enable Configuration
Enable-KrConfiguration

## 5. Routes
# Simple home page with an EventSource client
Add-KrHtmlTemplateRoute -Pattern '/' -HtmlTemplatePath 'Assets/wwwroot/sse.html'

# SSE stream endpoint (per connection)
Add-KrMapRoute -Verbs Get -Pattern '/sse' {
  $count = Get-KrRequestQuery -Name 'count' -AsInt
  if ($count -le 0) { $count = 30 }

  $intervalMs = Get-KrRequestQuery -Name 'intervalMs' -AsInt
  if ($intervalMs -le 0) { $intervalMs = 1000 }

  Start-KrSseResponse

  $connected = @{
    message = 'Connected to Kestrun SSE stream'
    serverTime = (Get-Date)
    count = $count
    intervalMs = $intervalMs
  } | ConvertTo-Json -Compress

  Write-KrSseEvent -Event 'connected' -Data $connected -retryMs 2000

  for ($i = 1; $i -le $count; $i++) {
    $payload = @{
      i = $i
      total = $count
      timestamp = (Get-Date)
    } | ConvertTo-Json -Compress

    try {
      Write-KrSseEvent -Event 'tick' -Data $payload -id "$i"
    } catch {
      # Most common cause: client disconnected.
      Write-KrLog -Level Debug -Message 'SSE write failed (client disconnected?): {Error}' -Values $_
      break
    }

    Start-Sleep -Milliseconds $intervalMs
  }

  $complete = @{
    message = 'Stream complete'
    total = $count
    serverTime = (Get-Date)
  } | ConvertTo-Json -Compress

  try {
    Write-KrSseEvent -Event 'complete' -Data $complete
  } catch {
    # Client may have disconnected; ignore.
  }
}

## 6. Start Server

Write-Host '🟢 Kestrun SSE Demo Server Started' -ForegroundColor Green
Write-Host "📍 Navigate to http://localhost:$Port to see the demo" -ForegroundColor Cyan
Write-Host "📡 SSE stream endpoint: http://localhost:$Port/sse" -ForegroundColor Cyan

Start-KrServer -CloseLogsOnExit
