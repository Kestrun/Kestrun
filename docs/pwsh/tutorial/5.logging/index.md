---
title: Logging
parent: Tutorials
nav_order: 5
---

# Logging (Tutorial Index)

This tutorial section will contain hands-on chapters for configuring logging in step with building a server.
The in-depth conceptual guide has moved to: [Logging Guide](/guides/logging).

## Chapters

Order | Chapter | Focus
----- | ------- | -----
1 | [Simple Logging](./1.Simple-Logging) | Create a single named logger & write events
2 | [Multiple Loggers & Levels](./2.Multiple-Loggers-Levels) | Separate audit/debug pipelines
3 | [Enrichment & Correlation IDs](./3.Enrichment-Correlation-IDs) | Add static & dynamic properties
4 | [Sinks](./4.Sinks) | Console/File/Seq/HTTP sinks
5 | [Sinks (Advanced)](./5.Sinks-Advanced) | Additional sink options & patterns
6 | [Hot Reload](./6.Hot-Reload) | Reconfigure log levels without restart
7 | [Apache Common Access Log](./7.Apache-Common-Access-Log) | Classic Apache CLF request middleware

---

## Quick Start (Preview)

```powershell
New-KrLogger -Name 'demo' -Level Information |
    Add-KrSink -Type Console -Options @{} |
    Register-KrLogger

Write-KrLog -Name 'demo' -Level Information -Message 'Hello {Name}' -Arguments 'World'
```

More examples: see the [Logging Guide](/guides/logging).

---

### Next

Continue with: [Shared Variables](../4.variable/index) or return to the main [Tutorial Index](/pwsh/tutorial/index).
