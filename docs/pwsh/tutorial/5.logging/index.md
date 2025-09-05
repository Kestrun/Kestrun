---
title: Logging
parent: Tutorials
nav_order: 5
---

# Logging (Tutorial Index)

This tutorial section will contain hands-on chapters for configuring logging in step with building a server.
The in-depth conceptual guide has moved to: [Logging Guide](/docs/guides/logging).

## Planned Chapters

| Order | Chapter (planned)             | Focus                                           |
|-------|-------------------------------|-------------------------------------------------|
| 1     | Basic Logger Setup            | Create a single named logger & write events     |
| 2     | Multiple Loggers & Levels     | Separate audit/debug pipelines                  |
| 3     | Enrichment & Correlation IDs  | Adding static & dynamic properties              |
| 4     | Sinks (Console/File/Seq/HTTP) | Attaching multiple sinks                        |
| 5     | Hot Reload / Update-KrLogger  | Reconfiguring without restart                   |
| 6     | PowerShell vs C# Interop      | Sharing configuration patterns across languages |
| 7     | Advanced Filtering            | Level overrides, conditional sinks (future)     |

---

## Quick Start (Preview)

```powershell
New-KrLogger -Name 'demo' -Level Information |
    Add-KrSink -Type Console -Options @{} |
    Register-KrLogger

Write-KrLog -Name 'demo' -Level Information -Message 'Hello {Name}' -Arguments 'World'
```

More examples: see the [Logging Guide](/docs/guides/logging).

---

### Next

Continue with: [Shared Variables](../4.variable/index) or return to the main [Tutorial Index](/docs/pwsh/tutorial/index).
