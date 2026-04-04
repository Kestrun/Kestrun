---
title: Endpoints
parent: Tutorials
nav_order: 7
---

# Endpoints & Server Hosting

Create and host a Kestrun server: listeners (HTTP/HTTPS, pipes, sockets), runtime enablement, and lifecycle.

> Prerequisites: review [Routes](../2.routes/index) for mapping, and [Logging](../5.logging/1.Simple-Logging) if you want structured logs visible.

Core cmdlets:
`New-KrServer`, `Add-KrEndpoint`, `Add-KrNamedPipeListener`, `Add-KrListenUnixSocket`,
`Enable-KrConfiguration`, `Add-KrMapRoute`, `Start-KrServer`,
`Stop-KrServer`, `Remove-KrServer`.

> Tip: Stage listeners & runtime first, then call `Enable-KrConfiguration`. You can still add routes afterwards.

`Add-KrEndpoint` can now resolve its listener from environment variables when you do not pass an explicit
binding target. This is useful for containers, PaaS hosts, and local launch profiles.

`Add-KrEndpoint -PassThru` can also return endpoint spec strings that feed directly into
`Add-KrMapRoute -Endpoints`. This is the preferred way to bind routes to specific listeners without
hardcoding `host:port` strings that can drift from the actual listener configuration.

Binding precedence:

1. `-Uri`
2. `-HostName`
3. `-Port` / `-IPAddress`
4. `ASPNETCORE_URLS`
5. `PORT`
6. Built-in defaults

Examples:

```powershell
$env:PORT = '8080'
Add-KrEndpoint

$env:ASPNETCORE_URLS = 'http://localhost:5000;http://+:5001'
Add-KrEndpoint

$httpsEndpoints = Add-KrEndpoint -Port 5443 -SelfSignedCert -PassThru
Add-KrMapRoute -Pattern '/secure' -Verbs Get -Endpoints $httpsEndpoints -ScriptBlock {
    Write-KrTextResponse 'Secure hello'
}
```

`ASPNETCORE_URLS` uses the first non-empty entry. `PORT` binds to `0.0.0.0` on the specified port.

## Chapters

| #   | Chapter                                         | Focus                                           |
| --- | ----------------------------------------------- | ----------------------------------------------- |
| 1   | [Basic Server](./1.Basic-Server)                | Minimal loopback listener + hello route         |
| 2   | [Multiple Listeners](./2.Multiple-Listeners)    | Same app on several ports / interfaces          |
| 3   | [HTTPS & Certificates](./3.Https)               | Add TLS with `-CertPath` / provided cert object |
| 4   | [Named Pipes (Windows)](./4.Named-Pipes)        | Local IPC without a TCP port                    |
| 5   | [Unix Sockets (Linux/macOS)](./5.Unix-Sockets)  | Domain sockets for reverse proxy fronting       |
| 6   | [Mixed HTTP Protocols](./6.Mixed-HttpProtocols) | Separate listeners: HTTP/1.1, 2, 3              |
| 7   | [URI Endpoint](./7.Uri-Endpoint)                | Single listener from full URI                   |

Advanced server tuning, lifecycle, and demos have moved:

| Chapter             | New Location                                                        |
| ------------------- | ------------------------------------------------------------------- |
| Server Limits       | [Server Configuration](../13.server-configuration/1.Server-Limits)  |
| Server Options      | [Server Configuration](../13.server-configuration/2.Server-Options) |
| Full Demo           | [Demos](../15.demos/index)                                          |
| Start/Stop Patterns | [Lifecycle](../14.lifecycle/1.Start-Stop)                           |

## Quick start

```powershell
New-KrServer -Name 'demo'
Add-KrEndpoint -Port 5000 -IPAddress ([IPAddress]::Loopback)

Enable-KrConfiguration
Add-KrMapRoute -Pattern '/ping' -Verbs Get -ScriptBlock { Write-KrTextResponse 'pong' }
Start-KrServer
```

Browse: <http://127.0.0.1:5000/ping>

## Listener-specific routing

When you need a route on only one listener, capture the endpoint specs from `Add-KrEndpoint`:

```powershell
New-KrServer -Name 'demo'
$httpEndpoints = Add-KrEndpoint -Port 5000 -PassThru
$httpsEndpoints = Add-KrEndpoint -Port 5443 -SelfSignedCert -PassThru

Enable-KrConfiguration

Add-KrMapRoute -Pattern '/http-only' -Verbs Get -Endpoints $httpEndpoints -ScriptBlock {
    Write-KrTextResponse 'Only on HTTP'
}

Add-KrMapRoute -Pattern '/https-only' -Verbs Get -Endpoints $httpsEndpoints -ScriptBlock {
    Write-KrTextResponse 'Only on HTTPS'
}
```

This avoids hand-maintaining `"localhost:5000"` or `"127.0.0.1:5443"` strings in route definitions.

## Choosing a transport

| Scenario                   | Recommendation                                 |
| -------------------------- | ---------------------------------------------- |
| Local development          | Loopback HTTP listener                         |
| Add encryption quickly     | Add HTTPS listener with self‑signed PFX        |
| Local automation (Windows) | Named pipe                                     |
| Reverse proxy (Linux)      | Unix socket + nginx/Caddy                      |
| Graceful background task   | Use `Start-KrServer -NoWait` + `Stop-KrServer` |

## Troubleshooting

| Symptom            | Cause                               | Fix                                                           |
| ------------------ | ----------------------------------- | ------------------------------------------------------------- |
| 404 on basic route | Route added before enabling configuration | Call `Enable-KrConfiguration` before adding routes |
| No output/logs     | No default logger registered        | Register logger with `-SetAsDefault`                          |
| Port in use        | Another process bound               | Change `-Port` or free port                                   |
| Unexpected bind address | `ASPNETCORE_URLS` or `PORT` is set in the current environment | Clear the environment variable or pass an explicit binding target |

See also: [Routes Overview](../2.routes/index) · [Simple Logging](../5.logging/1.Simple-Logging)

---

Previous: [Certificates](../6.certificates/index)
Next: [Server Configuration](../13.server-configuration/index) (advanced) or [Authentication](../8.authentication/index) when available.
