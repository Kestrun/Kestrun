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
Add-KrMapRoute -Verbs Get -Pattern '/' {
    $html = @'
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Kestrun SSE Demo</title>
  <style>
    body { font-family: system-ui, -apple-system, Segoe UI, Roboto, Arial, sans-serif; margin: 24px; }
    code { background: #f5f5f5; padding: 2px 6px; border-radius: 4px; }
    .row { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; }
    input { padding: 6px 8px; width: 110px; }
    button { padding: 8px 10px; }
    pre { background: #0b1020; color: #e6edf3; padding: 12px; border-radius: 8px; overflow: auto; height: 320px; }
  </style>
</head>
<body>
  <h1>Kestrun SSE Demo</h1>
  <p>Open an SSE stream from <code>/sse</code> and watch events arrive.</p>

  <div class="row">
    <label>Count <input id="count" type="number" min="1" max="1000" value="30" /></label>
    <label>Interval (ms) <input id="intervalMs" type="number" min="100" max="60000" value="1000" /></label>
    <button id="btnConnect">Connect</button>
    <button id="btnDisconnect" disabled>Disconnect</button>
  </div>

  <p>Tip: you can also test with curl:</p>
  <pre><code>curl -N "http://127.0.0.1:5000/sse?count=10&amp;intervalMs=500"</code></pre>

  <h3>Events</h3>
  <pre id="log"></pre>

  <script>
    const logEl = document.getElementById('log');
    const countEl = document.getElementById('count');
    const intervalEl = document.getElementById('intervalMs');
    const btnConnect = document.getElementById('btnConnect');
    const btnDisconnect = document.getElementById('btnDisconnect');

    let es = null;

    function append(line) {
      logEl.textContent += line + "\n";
      logEl.scrollTop = logEl.scrollHeight;
    }

    function connect() {
      const count = Number(countEl.value || 30);
      const intervalMs = Number(intervalEl.value || 1000);
      const url = `/sse?count=${encodeURIComponent(count)}&intervalMs=${encodeURIComponent(intervalMs)}`;

      append(`Connecting to ${url} ...`);
      es = new EventSource(url);

      es.onopen = () => {
        append('SSE connected');
        btnConnect.disabled = true;
        btnDisconnect.disabled = false;
      };

      es.onmessage = (evt) => {
        append(`message: ${evt.data}`);
      };

      es.addEventListener('tick', (evt) => {
        append(`tick: ${evt.data}`);
      });

      es.addEventListener('connected', (evt) => {
        append(`connected: ${evt.data}`);
      });

      es.onerror = () => {
        append('SSE error (server closed connection?)');
        disconnect();
      };
    }

    function disconnect() {
      if (es) {
        es.close();
        es = null;
      }
      btnConnect.disabled = false;
      btnDisconnect.disabled = true;
      append('SSE disconnected');
    }

    btnConnect.addEventListener('click', connect);
    btnDisconnect.addEventListener('click', disconnect);
  </script>
</body>
</html>
'@

    Write-KrHtmlResponse -Template $html -StatusCode 200
}

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
}

## 6. Start Server

Write-Host '🟢 Kestrun SSE Demo Server Started' -ForegroundColor Green
Write-Host "📍 Navigate to http://localhost:$Port to see the demo" -ForegroundColor Cyan
Write-Host "📡 SSE stream endpoint: http://localhost:$Port/sse" -ForegroundColor Cyan

Start-KrServer -CloseLogsOnExit
