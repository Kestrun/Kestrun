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

| Component                                | Purpose                                                                                                                                                                                               |
|------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Kestrun.Logging.LoggerManager`          | Static manager to add/register/get/clear named Serilog loggers; can set Serilog’s default logger.                                                                                                     |
| `LoggerConfigurationExtensions.Register` | Extension to create a Serilog logger from a configuration and register it under a name.                                                                                                               |
| PowerShell cmdlets                       | `New-KrLogger`, `Set-KrMinimumLevel`, `New-KrLevelSwitch`, `Set-KrLevelSwitch`, enrichers, sinks, `Register-KrLogger`, `Write-KrLog`, `Close-KrLogger`, `Get-KrDefaultLogger`, `Set-KrDefaultLogger`. |

---

## 1. Configuring a logger (C#)

Using the manager (convenient):

```csharp
using Kestrun.Logging;
using Serilog.Events;

var api = LoggerManager.Add(
  name: "api",
  config: cfg => cfg
    .MinimumLevel.Information()
    .Enrich.WithProperty("Subsystem", "API")
    .WriteTo.Console(),
  setAsDefault: true);
```

Using a LoggerConfiguration + extension:

```csharp
using Kestrun.Logging;
using Serilog.Events;

var api = new LoggerConfiguration()
  .MinimumLevel.Information()
  .Enrich.WithProperty("Subsystem", "API")
  .WriteTo.Console()
  .Register(name: "api", setAsDefault: true);
```

Tip: `LoggerManager.DefaultLogger` controls `Serilog.Log.Logger`.
Setting `setAsDefault: true` (or PowerShell’s `-SetAsDefault`) makes framework/server logs
go to your logger.

## 2. PowerShell builder

```powershell
# Create and register a named logger
New-KrLogger |
  Set-KrMinimumLevel -Value Information |
  Add-KrEnrichWithProperty -Name 'Subsystem' -Value 'API' |
  Add-KrSinkConsole |
  Register-KrLogger -Name 'ps-api' -SetAsDefault

# Write a log event
Write-KrLog -Name 'ps-api' -Level Information -Message 'Started' -Properties @{ Host = $env:COMPUTERNAME }
```

Framework logs vs your logs:

* Use `-SetAsDefault` on `Register-KrLogger`, or
* Pass `-LoggerName 'ps-api'` (or `-Logger`) to `New-KrServer`.

Otherwise Kestrun uses Serilog’s default (silent) logger for server logs.

## 3. Enrichment & sinks

* Enrichers: `Add-KrEnrichWithProperty`, `Add-KrEnrichWithProcessId`, `Add-KrEnrichWithProcessName`,
  `Add-KrEnrichWithEnvironment`, `Add-KrEnrichFromLogContext`, `Add-KrEnrichWithExceptionDetail`.
* Sinks: `Add-KrSinkConsole`, `Add-KrSinkPowerShell`, `Add-KrSinkFile`, `Add-KrSinkHttp`,
  `Add-KrSinkEventLog`, `Add-KrSinkSyslogUdp|Tcp|Local`.
* JSON formatting: `Get-KrJsonFormatter` for `Add-KrSinkFile -Formatter`.

Any Serilog sink/enricher you reference via packages can be used in C#; PowerShell exposes common ones.

## 4. Updating / hot reload (supported patterns)

There is no “Update-KrLogger” cmdlet. Use one of these patterns:

* Level switches (PowerShell):

  ```powershell
  $switch = New-KrLevelSwitch -MinimumLevel Information
  New-KrLogger |
    Set-KrMinimumLevel -ControlledBy $switch |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'app'

  # Later, raise/lower the level at runtime
  Set-KrLevelSwitch -LevelSwitch $switch -MinimumLevel Debug
  ```

* Level switches (C#):

  ```csharp
  using Serilog.Core;
  using Serilog.Events;
  using Kestrun.Logging;

  var sw = new LoggingLevelSwitch(LogEventLevel.Information);
  var app = new LoggerConfiguration()
      .MinimumLevel.ControlledBy(sw)
      .WriteTo.Console()
      .Register("app");

  // Later
  sw.MinimumLevel = LogEventLevel.Debug;
  ```

* Re-register the same name (replace logger):

  * PowerShell:

    ```powershell
    Register-KrLogger -Name 'app' -SetAsDefault -LoggerConfig (
      New-KrLogger | Set-KrMinimumLevel -Value Debug | Add-KrSinkConsole
    )
    ```

  * C#:

    ```csharp
    LoggerManager.Add("app", cfg => cfg.MinimumLevel.Debug().WriteTo.Console(), setAsDefault: true);
    ```

`LoggerManager` disposes the previous instance when replacing a logger with the same name.

## 5. Defaults and cleanup

* C#:
  * `LoggerManager.DefaultLogger` / `LoggerManager.DefaultLoggerName`
  * `LoggerManager.Clear()` — remove and dispose all registered loggers
* PowerShell:
  * `Get-KrDefaultLogger`, `Set-KrDefaultLogger`
  * `Close-KrLogger` — close a specific logger or `Serilog.Log.Logger`

## 6. Cmdlet / API reference (summary)

PowerShell

* Builders: `New-KrLogger`, `Set-KrMinimumLevel`, `New-KrLevelSwitch`, `Set-KrLevelSwitch`
* Enrichers: `Add-KrEnrichWithProperty`, `Add-KrEnrichWithProcessId`, `Add-KrEnrichWithProcessName`,
  `Add-KrEnrichWithEnvironment`, `Add-KrEnrichFromLogContext`, `Add-KrEnrichWithExceptionDetail`
* Sinks: `Add-KrSinkConsole`, `Add-KrSinkPowerShell`, `Add-KrSinkFile`, `Add-KrSinkHttp`,
  `Add-KrSinkEventLog`, `Add-KrSinkSyslogUdp`, `Add-KrSinkSyslogTcp`, `Add-KrSinkSyslogLocal`,
  `Get-KrJsonFormatter`
* Register/use: `Register-KrLogger`, `Write-KrLog`, `Get-KrDefaultLogger`, `Set-KrDefaultLogger`,
  `Close-KrLogger`

C#

* `Kestrun.Logging.LoggerManager` — `Add()`, `Register()`, `Get()`, `List()`, `Remove()`,
  `Clear()`, `DefaultLogger`, `DefaultLoggerName`
* `LoggerConfigurationExtensions.Register()` — register a created logger under a name

---

## Next

Return to the [home](/) or the [tutorial index](/pwsh/tutorial/index).

---

_For full examples (multi-logger, global default, advanced sinks) see the PowerShell tutorial under Logging, or the C# examples in the repo._
