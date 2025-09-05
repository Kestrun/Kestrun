---
title: Introduction
parent: Tutorials
nav_order: 1
---

# Introduction to Kestrun (PowerShell)

Kestrun is a lightweight, cross‑platform web server you can configure entirely from PowerShell
(and optionally mix with C# or VB.NET inline). It sits on ASP.NET Core Kestrel, so you get
performance, HTTP/1.1–HTTP/3 support, TLS, compression, and familiar middleware patterns — with
simple PowerShell cmdlets.

This chapter gives you a hands‑on start using three tiny samples you can run immediately:

- Sample-1: Hello World route returning plain text
- Sample-2: Multiple content types (text, JSON, XML, YAML)
- Sample-3: Multi-language routes (PowerShell, C#, VB.NET)

All sample scripts live in [examples/PowerShell/Tutorial][Tutorial].

## Next steps

- Dive into the other Tutorial chapters (Certificates, Logging, Razor Pages, Scheduling)
- Explore the richer example scripts under [examples/PowerShell][examples/PowerShell] (e.g., `MultiRoutes.ps1`)
- Browse the PowerShell cmdlet reference under [docs/pwsh/cmdlets][docs/pwsh/cmdlets]
- Explore the PowerShell module source at [src/PowerShell/Kestrun][src/PowerShell/Kestrun]

[Tutorial]: https://github.com/Kestrun/Kestrun/blob/main/examples/PowerShell/Tutorial
[src/PowerShell/Kestrun]: https://github.com/Kestrun/Kestrun/blob/main/src/PowerShell/Kestrun
[docs/pwsh/cmdlets]: /pwsh/cmdlets
[examples/PowerShell]: https://github.com/Kestrun/Kestrun/blob/main/examples/PowerShell
