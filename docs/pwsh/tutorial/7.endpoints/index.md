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

See also: [Routes Overview](../2.routes/index) · [Simple Logging](../5.logging/1.Simple-Logging)

---

Previous: [Certificates](../6.certificates/index)
Next: [Server Configuration](../13.server-configuration/index) (advanced) or [Authentication](../8.authentication/index) when available.
