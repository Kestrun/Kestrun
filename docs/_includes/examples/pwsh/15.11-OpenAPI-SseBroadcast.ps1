<#
    Create a broadcast SSE demo server with Kestrun in PowerShell (with OpenAPI).
    FileName: 15.11-OpenAPI-SseBroadcast.ps1

    This demo shows:
    - GET /sse/broadcast : server-side broadcast stream (text/event-stream)
    - POST /api/broadcast: broadcast an event to all connected SSE clients

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
    Add-KrSinkConsole | Register-KrLogger -Name 'SseBroadcastDemoOpenApi' -SetAsDefault

New-KrServer -Name 'Kestrun SSE Broadcast Demo (OpenAPI)'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Kestrun SSE Broadcast Demo API' `
    -Version '1.0.0' `
    -Description 'Demonstrates documenting an SSE broadcast endpoint (text/event-stream) and a broadcast trigger API.'

# =========================================================
#           OPENAPI SCHEMA COMPONENT DEFINITIONS
# =========================================================

[OpenApiSchemaComponent(RequiredProperties = ('event', 'data'))]
class SseBroadcastRequest {
    [OpenApiProperty(Description = 'SSE event name', Example = 'message')]
    [string]$event

    [OpenApiProperty(Description = 'Arbitrary payload object')]
    [object]$data
}

[OpenApiSchemaComponent(RequiredProperties = ('ok', 'event', 'connected'))]
class SseBroadcastOkResponse {
    [OpenApiProperty(Description = 'True when broadcast succeeded', Example = $true)]
    [bool]$ok

    [OpenApiProperty(Description = 'Event name that was broadcast', Example = 'message')]
    [string]$event

    [OpenApiProperty(Description = 'Connected SSE client count (null if unavailable)', Example = 3)]
    [Nullable[int]]$connected
}

[OpenApiSchemaComponent(RequiredProperties = ('ok', 'error'))]
class SseBroadcastErrorResponse {
    [OpenApiProperty(Description = 'False when broadcast failed', Example = $false)]
    [bool]$ok

    [OpenApiProperty(Description = 'Error message')]
    [string]$error
}

# Add the broadcast SSE endpoint (implemented in C#; keeps connections open)
# Note: the C# endpoint registers its own OpenAPI metadata so it will appear in the OpenAPI document.
Add-KrSseBroadcastMiddleware -Path '/sse/broadcast' -KeepAliveSeconds 15

## Enable Configuration
Enable-KrConfiguration

# Swagger / Redoc UI routes
Add-KrApiDocumentationRoute -DocumentType Swagger -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Redoc -OpenApiEndpoint '/openapi/v3.1/openapi.json'

# =========================================================
#                       HOME PAGE
# =========================================================

Add-KrMapRoute -Verbs Get -Pattern '/' {
    $html = @'
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Kestrun SSE Broadcast Demo (OpenAPI)</title>
  <style>
    body { font-family: system-ui, -apple-system, Segoe UI, Roboto, Arial, sans-serif; margin: 24px; }
    code { background: #f5f5f5; padding: 2px 6px; border-radius: 4px; }
    .row { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; margin: 12px 0; }
    input { padding: 6px 8px; width: 260px; }
    button { padding: 8px 10px; }
    pre { background: #0b1020; color: #e6edf3; padding: 12px; border-radius: 8px; overflow: auto; height: 320px; }
    .pill { display: inline-block; padding: 2px 8px; border-radius: 999px; background: #eef2ff; }
  </style>
</head>
<body>
  <h1>Kestrun SSE Broadcast Demo (OpenAPI)</h1>
  <p>
    Connect to <code>/sse/broadcast</code> and broadcast events from <code>/api/broadcast</code>.
    <span class="pill" id="status">disconnected</span>
  </p>
  <p>
    OpenAPI: <code>/openapi/v3.2/openapi.json</code> | Swagger: <code>/swagger</code> | ReDoc: <code>/redoc</code>
  </p>

  <div class="row">
    <button id="btnConnect">Connect</button>
    <button id="btnDisconnect" disabled>Disconnect</button>
    <button id="btnPing" disabled>Broadcast Ping</button>
    <label>Message <input id="message" value="Hello everyone" /></label>
    <button id="btnSend" disabled>Broadcast Message</button>
  </div>

  <pre id="log"></pre>

  <script>
    const logEl = document.getElementById('log');
    const statusEl = document.getElementById('status');
    const btnConnect = document.getElementById('btnConnect');
    const btnDisconnect = document.getElementById('btnDisconnect');
    const btnPing = document.getElementById('btnPing');
    const btnSend = document.getElementById('btnSend');
    const msgEl = document.getElementById('message');

    let es = null;

    const append = (line) => { logEl.textContent += line + "\n"; logEl.scrollTop = logEl.scrollHeight; };

    function setConnected(connected) {
      statusEl.textContent = connected ? 'connected' : 'disconnected';
      btnConnect.disabled = connected;
      btnDisconnect.disabled = !connected;
      btnPing.disabled = !connected;
      btnSend.disabled = !connected;
    }

    function connect() {
      if (es) return;
      append('Connecting to /sse/broadcast ...');
      es = new EventSource('/sse/broadcast');

      es.onopen = () => {
        append('SSE connected');
        setConnected(true);
      };

      es.addEventListener('connected', (evt) => append(`connected: ${evt.data}`));
      es.addEventListener('ping', (evt) => append(`ping: ${evt.data}`));
      es.addEventListener('message', (evt) => append(`message: ${evt.data}`));

      es.onerror = () => {
        append('SSE error (stream closed?)');
        disconnect();
      };
    }

    function disconnect() {
      if (es) {
        es.close();
        es = null;
      }
      setConnected(false);
      append('SSE disconnected');
    }

    async function broadcast(eventName, data) {
      const res = await fetch('/api/broadcast', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ event: eventName, data })
      });
      const txt = await res.text();
      append(`broadcast response (${res.status}): ${txt}`);
    }

    btnConnect.addEventListener('click', connect);
    btnDisconnect.addEventListener('click', disconnect);
    btnPing.addEventListener('click', () => broadcast('ping', { ts: new Date().toISOString() }));
    btnSend.addEventListener('click', () => broadcast('message', { text: msgEl.value, ts: new Date().toISOString() }));

    setConnected(false);
  </script>
</body>
</html>
'@

    Write-KrHtmlResponse -Template $html -StatusCode 200
}

# =========================================================
#                OPENAPI-ANNOTATED ROUTES
# =========================================================

<#{
.SYNOPSIS
    Broadcasts an SSE event to all connected clients.
.DESCRIPTION
    Accepts a JSON payload containing an SSE event name and an arbitrary data object.
    The server broadcasts the event to all connected clients of GET /sse/broadcast.
.PARAMETER body
    Broadcast request payload.
}#>
function InvokeSseBroadcast {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/api/broadcast', Tags = 'SSE')]
    [OpenApiResponse(StatusCode = '200', Description = 'Broadcast succeeded', Schema = [SseBroadcastOkResponse], ContentType = 'application/json')]
    [OpenApiResponse(StatusCode = '500', Description = 'Broadcast failed', Schema = [SseBroadcastErrorResponse], ContentType = 'application/json')]
    param(
        [OpenApiRequestBody(Description = 'Broadcast SSE event payload', Required = $true)]
        [SseBroadcastRequest]$body
    )

    try {
        $eventName = [string]($body.event ?? 'message')

        $dataObj = $body.data
        if ($null -eq $dataObj) { $dataObj = @{ text = 'empty' } }

        $dataJson = $dataObj | ConvertTo-Json -Compress

        Send-KrSseBroadcastEvent -Event $eventName -Data $dataJson

        $count = Get-KrSseConnectedClientCount

        $res = [SseBroadcastOkResponse]::new()
        $res.ok = $true
        $res.event = $eventName
        $res.connected = $count

        Write-KrJsonResponse -InputObject $res -StatusCode 200
    } catch {
        $err = [SseBroadcastErrorResponse]::new()
        $err.ok = $false
        $err.error = $_.ToString()

        Write-KrJsonResponse -InputObject $err -StatusCode 500
    }
}

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute  # Default pattern '/openapi/{version}/openapi.{format}'

Build-KrOpenApiDocument
Test-KrOpenApiDocument

Write-Host '🟢 Kestrun SSE Broadcast Demo (OpenAPI) Server Started' -ForegroundColor Green
Write-Host "📍 Navigate to http://localhost:$Port" -ForegroundColor Cyan
Write-Host "📡 Broadcast SSE endpoint: http://localhost:$Port/sse/broadcast" -ForegroundColor Cyan
Write-Host "📨 Broadcast API: http://localhost:$Port/api/broadcast" -ForegroundColor Cyan
Write-Host "📄 OpenAPI JSON: http://localhost:$Port/openapi/v3.1/openapi.json" -ForegroundColor Cyan

Start-KrServer -CloseLogsOnExit
