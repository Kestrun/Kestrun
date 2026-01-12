---
layout: default
title: Real-time (SSE & SignalR)
parent: Guides
nav_order: 140
---

# Real-time (SSE & SignalR)

Understand what **Server-Sent Events (SSE)** and **SignalR** are, how to use them in Kestrun, and how Kestrun represents them in OpenAPI.

## Overview

Kestrun supports two “push” models:

- **SSE**: server → client stream over HTTP (`text/event-stream`). Best for *broadcasts* and “live dashboards”.
- **SignalR**: bidirectional real-time messaging using WebSockets (with fallbacks). Best for *interactive apps* and *client → server messages*.

### Choosing the right tool

| Requirement | Prefer | Why |
|---|---|---|
| Server pushes events to browsers (one-way) | SSE | Simple, native browser `EventSource`, HTTP-friendly |
| Client and server both send messages | SignalR | Bidirectional hub with robust reconnect patterns |
| Needs binary/complex messaging patterns, groups, acking | SignalR | Built-in hub concepts and targeting |
| Simple fan-out / notifications | SSE Broadcast | Lightweight and easy to debug |
| You must support older proxies/load balancers | SSE | Often easier than WebSockets in constrained environments |

> **Tip:** It’s common to use **SignalR for app UX** (interactive pages) and **SSE for operational/telemetry streams**.

---

## 1. SSE fundamentals

SSE is a long-lived HTTP response where the server writes a sequence of **events**:

- Response `Content-Type`: `text/event-stream`
- Each event is line-based:
  - Optional: `event: name`
  - Optional: `id: 123`
  - Optional: `retry: 2000`
  - One or more: `data: ...`
  - Blank line terminates the event

Browsers reconnect automatically. If you send `id`, the browser will include `Last-Event-ID` on reconnects.

### 1.1 Client-side SSE (browser)

```javascript
const es = new EventSource('/sse');

es.addEventListener('connected', (e) => {
  console.log('connected', e.data);
});

es.addEventListener('progress', (e) => {
  console.log('progress', JSON.parse(e.data));
});

es.onerror = (e) => {
  console.warn('SSE error', e);
};
```

Notes:

- `EventSource` can’t set custom headers. If you need headers for auth, use cookies (same-site) or design a token-in-query approach carefully.
- SSE is **text**. If you’re sending structured payloads, prefer JSON strings.

---

## 2. Per-connection SSE in Kestrun

A *per-connection SSE endpoint* is a route that owns the stream and writes events for that single request.

### 2.1 PowerShell pattern

```powershell
Add-KrMapRoute -Verbs Get -Pattern '/sse' {
  Start-KrSseResponse

  Write-KrSseEvent -Event 'connected' -Data '{"ok":true}' -retryMs 2000

  1..5 | ForEach-Object {
    Write-KrSseEvent -Event 'tick' -Data (@{ i = $_ } | ConvertTo-Json -Compress) -id "$_"
    Start-Sleep -Milliseconds 250
  }

  Write-KrSseEvent -Event 'complete' -Data '{"done":true}'
}
```

Key points:

- Call `Start-KrSseResponse` first.
- Always emit JSON as a **string** (use `ConvertTo-Json -Compress`) so payload formatting is stable.
- Keep loops bounded (or cooperative-cancel) to avoid holding the connection forever during tests.

### 2.2 C# pattern

```csharp
server.AddMapRoute("/sse", HttpVerb.Get, async ctx =>
{
    ctx.StartSse();

    await ctx.WriteSseEventAsync("connected", "{\"ok\":true}", retryMs: 2000, ct: ctx.HttpContext.RequestAborted);
    await ctx.WriteSseEventAsync("tick", "{\"i\":1}");
});
```

Under the hood, `StartSse()` sets `Cache-Control: no-cache`, `Connection: keep-alive`, disables buffering for some proxies, and uses `FlushAsync()` after each event.

---

## 3. Broadcast SSE in Kestrun

Broadcast SSE is for the case where the server **maintains a connection registry** and you broadcast to all connected clients.

You typically configure:

1. A **GET** endpoint that keeps a stream open
2. One or more **trigger** routes (often `POST`) that broadcast events

### 3.1 PowerShell: broadcast stream + trigger

```powershell
# Keep broadcast connections open
Add-KrSseBroadcastMiddleware -Path '/sse/broadcast' -KeepAliveSeconds 15

# Broadcast from any route
Add-KrMapRoute -Verbs Post -Pattern '/api/broadcast' {
  $body = Get-KrRequestBody
  $eventName = [string]($body.event ?? 'message')

  $dataJson = ($body.data ?? @{ text = 'empty' }) | ConvertTo-Json -Compress
  Send-KrSseBroadcastEvent -Event $eventName -Data $dataJson

  Write-KrJsonResponse @{ ok = $true; event = $eventName; connected = (Get-KrSseConnectedClientCount) }
}
```

### 3.2 Multiple broadcast streams (different OpenAPI schema)

You can expose multiple broadcast endpoints. This is useful when you want **different OpenAPI response schemas** for different streams.

Example idea:

- `/sse/broadcast` → generic messages (`string` schema)
- `/sse/broadcast/progress` → progress events (`OperationProgressEvent` schema)

```powershell
[OpenApiSchemaComponent(Description = 'Operation progress SSE event payload')]
class OperationProgressEvent {
  [string]$taskId
  [double]$progress
  [string]$status
  [string]$state
  [datetime]$ts
}

Add-KrSseBroadcastMiddleware -Path '/sse/broadcast/progress' -KeepAliveSeconds 15 -OpenApiItemSchemaType ([OperationProgressEvent])
```

> **Note:** The runtime payload is still SSE text (`data: ...`). The schema is documentation for clients so they know what JSON shape to expect in `data`.

---

## 4. SignalR in Kestrun

SignalR is a higher-level real-time system built on persistent connections:

- Uses **WebSockets** when available
- Falls back to other transports when needed
- Supports hub methods, groups, and server→client + client→server messaging

### 4.1 Hosting the hub

PowerShell:

```powershell
Add-KrSignalRHubMiddleware -Path '/hubs/kestrun'
```

C#:

```csharp
host.AddSignalR(new SignalROptions { Path = "/hubs/kestrun" });
```

### 4.2 Broadcasting from routes

Kestrun exposes a set of helper cmdlets so *any route* can broadcast to connected SignalR clients.

```powershell
Send-KrSignalRLog -Level 'Information' -Message 'Hello clients'

Send-KrSignalREvent -EventName 'OperationProgress' -Data @{ taskId = 't-1'; progress = 0.5 }

Send-KrSignalRGroupMessage -GroupName 'Admins' -Method 'ReceiveGroupMessage' -Message @{ text = 'Hi Admins' }
```

### 4.3 Client usage (browser)

Your page typically loads the official SignalR client and connects to the hub path:

```javascript
const conn = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/kestrun')
  .withAutomaticReconnect()
  .build();

conn.on('OperationProgress', (payload) => console.log(payload));
await conn.start();
```

---

## 5. OpenAPI generation for SSE and SignalR

Kestrun can include “real-time” endpoints in OpenAPI documents so clients can discover them.

For the full OpenAPI pipeline (metadata, schemas, doc routes), see the [OpenAPI Generation guide](./openapi).

### 5.1 OpenAPI for SSE

OpenAPI can describe an SSE endpoint like:

- **Method**: `GET`
- **Path**: `/sse/...`
- **Response content type**: `text/event-stream`
- **Schema**: typically `string` (because SSE is text)

Kestrun’s broadcast SSE middleware registers the broadcast stream into the host route registry so it appears in generated OpenAPI.

When you configure broadcast SSE from PowerShell, you can also customize the OpenAPI metadata
(operation id, summary, tags) and control the documented item schema type via `-OpenApiItemSchemaType`.

Practical guidance:

- Use a `string` schema when the stream is mixed/opaque.
- Use a typed schema when the stream is “mostly JSON of one shape” (e.g. progress events).
- If the stream contains multiple event types, consider documenting `data` as `oneOf` (or document each stream separately).

### 5.2 OpenAPI for SignalR

SignalR hubs are not classic REST endpoints, but there are still HTTP operations involved:

- `GET /hubs/...` for the connection (including WebSocket upgrade: `101 Switching Protocols`)
- `POST /hubs/.../negotiate` for negotiation

Kestrun registers OpenAPI entries for:

- The hub connect path (`GET`)
- The negotiate endpoint (`POST`) when enabled

Kestrun also emits `x-signalr` and `x-signalr-role` OpenAPI extensions that describe hub metadata
(hub name, paths, transports). These extensions help tooling, but they are not part of the core OpenAPI
specification.

Important limitation:

- OpenAPI does **not** fully model hub message contracts (server→client method payloads).
  Document those separately (docs pages, readme, or JSON schema references in your own `x-` extensions).

---

## 6. Operational and security notes

### 6.1 Proxies and buffering

- SSE requires flushing. If a proxy buffers responses, clients will appear “stuck”.
- Kestrun disables buffering for some proxies via `X-Accel-Buffering: no`.

### 6.2 Authentication

- SSE: `EventSource` is easiest with cookie auth; custom headers are not supported.
- SignalR: supports bearer tokens and cookie auth; follow the ASP.NET Core guidance for your chosen auth scheme.

### 6.3 Scaling out

- In-memory broadcast patterns only reach clients connected to the same server instance.
- For multi-instance deployments you typically need sticky sessions or a shared backplane.

---

## References

External sources:

- MDN: [Server-sent events (SSE)](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events)
- MDN: [EventSource](https://developer.mozilla.org/en-US/docs/Web/API/EventSource)
- WHATWG: [HTML Living Standard (server-sent events)](https://html.spec.whatwg.org/multipage/server-sent-events.html)
- Microsoft: [ASP.NET Core SignalR (overview)](https://learn.microsoft.com/aspnet/core/signalr/)
- Microsoft: [SignalR introduction](https://learn.microsoft.com/aspnet/core/signalr/introduction)
- OpenAPI Initiative: [OpenAPI Specification](https://spec.openapis.org/)

Kestrun docs:

- Tutorials: SSE: [/pwsh/tutorial/15.middleware/9.Sse](/pwsh/tutorial/15.middleware/9.Sse)
- Tutorials: SSE Broadcast: [/pwsh/tutorial/15.middleware/10.SseBroadcast](/pwsh/tutorial/15.middleware/10.SseBroadcast)
- Tutorials: SignalR: [/pwsh/tutorial/15.middleware/5.SignalR](/pwsh/tutorial/15.middleware/5.SignalR)
- Guide: OpenAPI Generation: [./openapi](./openapi)
