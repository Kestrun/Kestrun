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

# Add the broadcast SSE endpoint (implemented in C#; keeps connections open)
Add-KrSseBroadcastMiddleware -Path '/sse/broadcast' -KeepAliveSeconds 15

Enable-KrConfiguration

# Home page
Add-KrMapRoute -Verbs Get -Pattern '/' {
    $html = @'
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Kestrun SSE Broadcast Demo</title>
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
  <h1>Kestrun SSE Broadcast Demo</h1>
  <p>
    Connect to <code>/sse/broadcast</code> and broadcast events from <code>/api/broadcast</code>.
    <span class="pill" id="status">disconnected</span>
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

      es.addEventListener('connected', (evt) => {
        append(`connected: ${evt.data}`);
      });

      es.addEventListener('ping', (evt) => {
        append(`ping: ${evt.data}`);
      });

      es.addEventListener('message', (evt) => {
        append(`message: ${evt.data}`);
      });

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
        Write-KrJsonResponse -InputObject @{ ok = $false; error = $_.ToString() } -StatusCode 500
    }
}

Write-Host '🟢 Kestrun SSE Broadcast Demo Server Started' -ForegroundColor Green
Write-Host "📍 Navigate to http://localhost:$Port" -ForegroundColor Cyan
Write-Host "📡 Broadcast SSE endpoint: http://localhost:$Port/sse/broadcast" -ForegroundColor Cyan

Start-KrServer -CloseLogsOnExit
