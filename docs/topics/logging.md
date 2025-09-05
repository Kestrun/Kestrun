---
title: Logging
parent: Guides
nav_order: 30
---

# Kestrun Logging (C# & PowerShell)

This topic provides an in-depth guide to configuring and using Kestrun's Serilog-based logging pipeline
from both C# and PowerShell. PowerShell cmdlets wrap the same builder APIs used directly in C#.

<!-- markdownlint-disable MD033 -->
## Overview

Kestrun’s logging system builds on Serilog to give you:

* Multiple named loggers — each with its own minimum level, enrichers, and sinks.
* Full Serilog flexibility — use any sink (console, file, Seq, HTTP, syslog…), any enricher, any output template.
* Global fallback — framework + scripts without a named logger write to `Serilog.Log`.
* PowerShell support — configure and invoke named loggers from PS scripts.

| Component                | Purpose                                                                                                     |
|--------------------------|-------------------------------------------------------------------------------------------------------------|
| `KestrunLogConfigurator` | Static registry; create, store, update loggers.                                                             |
| `KestrunLoggerBuilder`   | Fluent builder wrapping `LoggerConfiguration` (Minimum, WithProperty, With<T>, Sink, Register).             |
| PowerShell cmdlets       | `New-KrLogger`, `Add-KrSink`, `Add-KrProperty`, `Register-KrLogger`, `Write-KrLog`, `Update-KrLogger`, etc. |

---

## 1. Configuring a Logger (C#)

```csharp
var api = KestrunLogConfigurator.Configure("api")
    .Minimum(LogEventLevel.Information)
    .WithProperty("Subsystem", "API")
    .Sink(w => w.Console())
    .Register();
```

## 2. PowerShell Builder

```powershell
New-KrLogger -Name 'ps' -Level Information |
  Add-KrSink -Type Console -Options @{} |
  Register-KrLogger

Write-KrLog -Name 'ps' -Level Information -Message 'Started' -Properties @{ Host = $env:COMPUTERNAME }
```

## 3. Enrichment & Sinks

Use `.WithProperty()`, `.With<Enricher>()`, and multiple `.Sink(...)` calls. Any Serilog extension methods you reference are available.

## 4. Updating / Hot Reload

`Update-KrLogger -Name api -ScriptBlock { param($b) $b.Minimum([Serilog.Events.LogEventLevel]::Debug) }`

## 5. Reset

```csharp
KestrunLogConfigurator.Reset();
```

```powershell
ReSet-KrDefaultLogger -Name 'ps'
```

## 6. Cmdlet / API Reference (Summary)

`New-KrLogger`, `Add-KrProperty`, `Add-KrEnricher`, `Add-KrSink`, `Register-KrLogger`, `Write-KrLog`,
`Update-KrLogger`, `Get-KrLogger`, `Test-KrLogger`, `ReSet-KrDefaultLogger`.

---

## Next

Return to the [home](/) or the [tutorial index](/docs/pwsh/tutorial/index).

---

_For full examples (multi-logger, global default, advanced sinks) see repository samples or extend this topic with a PR._
