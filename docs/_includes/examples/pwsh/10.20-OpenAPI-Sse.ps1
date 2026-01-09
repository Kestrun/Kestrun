<#
    Create an SSE demo server with Kestrun in PowerShell (with OpenAPI).
    FileName: 10.20-OpenAPI-Sse.ps1

    SSE helpers used by the streaming route:
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
    Add-KrSinkConsole | Register-KrLogger -Name 'SseDemoOpenApi' -SetAsDefault

## 2. Create Server
New-KrServer -Name 'Kestrun SSE Demo with OpenAPI'

## 3. Configure Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Kestrun SSE Demo API' `
    -Version '1.0.0' `
    -Description 'Demonstrates documenting an SSE (text/event-stream) endpoint with OpenAPI.'

# =========================================================
#           OPENAPI COMPONENT DEFINITIONS
# =========================================================

[OpenApiParameterComponent(In = 'Query', Description = 'Number of SSE events to send before closing the connection', Example = 30, Minimum = 1)]
[ValidateRange(1, 1000)]
[int]$count = 30

[OpenApiParameterComponent(In = 'Query', Description = 'Delay between events in milliseconds', Example = 1000, Minimum = 100)]
[ValidateRange(100, 60000)]
[int]$intervalMs = 1000

## 4. Enable Configuration
Enable-KrConfiguration

# Swagger / Redoc UI routes
Add-KrApiDocumentationRoute -DocumentType Swagger -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Redoc -OpenApiEndpoint '/openapi/v3.1/openapi.json'

## 5. Non-OpenAPI routes

# Simple home page with an EventSource client
Add-KrMapRoute -Verbs Get -Pattern '/' {
    $html = @'
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Kestrun SSE Demo (OpenAPI)</title>
  <style>
    body { font-family: system-ui, -apple-system, Segoe UI, Roboto, Arial, sans-serif; margin: 24px; }
    code { background: #f5f5f5; padding: 2px 6px; border-radius: 4px; }
        .row { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; margin: 12px 0; }
        input { padding: 6px 8px; width: 110px; }
        button { padding: 8px 10px; }
    pre { background: #0b1020; color: #e6edf3; padding: 12px; border-radius: 8px; overflow: auto; height: 320px; }
  </style>
</head>
<body>
  <h1>Kestrun SSE Demo (OpenAPI)</h1>
  <p>SSE stream: <code>/sse</code> | OpenAPI: <code>/openapi/v3.2/openapi.json</code> | Swagger: <code>/docs/swagger</code></p>

    <div class="row">
        <label>Count <input id="count" type="number" min="1" max="1000" value="30" /></label>
        <label>Interval (ms) <input id="intervalMs" type="number" min="100" max="60000" value="1000" /></label>
        <button id="btnStart">Start</button>
        <button id="btnPause" disabled>Pause</button>
        <button id="btnStop" disabled>Stop</button>
    </div>

  <pre id="log"></pre>

  <script>
        const logEl = document.getElementById('log');
        const countEl = document.getElementById('count');
        const intervalEl = document.getElementById('intervalMs');
        const btnStart = document.getElementById('btnStart');
        const btnPause = document.getElementById('btnPause');
        const btnStop = document.getElementById('btnStop');

        let es = null;
        let paused = false;

        const append = (line) => { logEl.textContent += line + "\n"; logEl.scrollTop = logEl.scrollHeight; };

        function start() {
            if (es) {
                append('Already connected');
                return;
            }

            const count = Number(countEl.value || 30);
            const intervalMs = Number(intervalEl.value || 1000);
            const url = `/sse?count=${encodeURIComponent(count)}&intervalMs=${encodeURIComponent(intervalMs)}`;
            append(`Connecting to ${url} ...`);

            es = new EventSource(url);

            es.onopen = () => {
                append('SSE connected');
                btnStart.disabled = true;
                btnStop.disabled = false;
                btnPause.disabled = false;
                paused = false;
                btnPause.textContent = 'Pause';
            };

            es.addEventListener('connected', (evt) => append(`connected: ${evt.data}`));
            es.addEventListener('tick', (evt) => {
                if (!paused) {
                    append(`tick: ${evt.data}`);
                }
            });
            es.addEventListener('complete', (evt) => {
                append(`complete: ${evt.data}`);
                stop();
            });

            es.onerror = () => {
                // EventSource reports "error" when the server closes the stream.
                // Close to avoid retry spam when the server ended the stream.
                append('SSE error (stream closed)');
                stop();
            };
        }

        function togglePause() {
            if (!es) {
                return;
            }
            paused = !paused;
            btnPause.textContent = paused ? 'Resume' : 'Pause';
            append(paused ? 'Paused (still connected)' : 'Resumed');
        }

        function stop() {
            if (es) {
                es.close();
                es = null;
            }
            paused = false;
            btnStart.disabled = false;
            btnStop.disabled = true;
            btnPause.disabled = true;
            btnPause.textContent = 'Pause';
            append('SSE disconnected');
        }

        btnStart.addEventListener('click', start);
        btnPause.addEventListener('click', togglePause);
        btnStop.addEventListener('click', stop);
  </script>
</body>
</html>
'@

    Write-KrHtmlResponse -Template $html -StatusCode 200
}

## 6. OpenAPI-annotated SSE route

<#
.SYNOPSIS
    Streams a Server-Sent Events (SSE) response.
.DESCRIPTION
    Sets Content-Type to text/event-stream and writes a series of SSE events ("connected" and "tick").
.PARAMETER count
    Number of events to send.
.PARAMETER intervalMs
    Delay between events.
#>
function GetSseStream {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/sse', Tags = 'SSE')]
    [OpenApiResponse(StatusCode = '200', Description = 'SSE stream (text/event-stream)', Schema = [string], ContentType = 'text/event-stream')]
    param(
        [OpenApiParameterRef(ReferenceId = 'count')]
        [int]$count,

        [OpenApiParameterRef(ReferenceId = 'intervalMs')]
        [int]$intervalMs
    )

    if ($count -le 0) { $count = 30 }
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

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute  # Default pattern '/openapi/{version}/openapi.{format}'

Build-KrOpenApiDocument
Test-KrOpenApiDocument

## 7. Start Server

Write-Host '🟢 Kestrun SSE Demo Server Started' -ForegroundColor Green
Write-Host "📍 Navigate to http://localhost:$Port to see the demo" -ForegroundColor Cyan
Write-Host "📡 SSE stream endpoint: http://localhost:$Port/sse" -ForegroundColor Cyan
Write-Host "📄 OpenAPI JSON: http://localhost:$Port/openapi/v3.1/openapi.json" -ForegroundColor Cyan
Write-Host "🧭 Swagger UI: http://localhost:$Port/swagger" -ForegroundColor Cyan

Start-KrServer -CloseLogsOnExit
