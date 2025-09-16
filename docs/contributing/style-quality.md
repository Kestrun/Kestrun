---
layout: default
title: Style & Quality
parent: Contributing
nav_order: 3
---

# Style & Quality

Consistency keeps Kestrun approachable. Follow these guidelines for C# and PowerShell.

## C# Guidelines

| Area        | Rule                                                           |
|-------------|----------------------------------------------------------------|
| Public APIs | Explicit types, nullable enabled                               |
| Async       | Use `ConfigureAwait(false)` in library code                    |
| Naming      | PascalCase for public members, `_camelCase` for private fields |
| Exceptions  | Throw specific types; avoid swallowing errors silently         |
| Logging     | Use provided logging abstractions / Serilog integration        |

Prefer small, composable classes. Avoid static mutable state.

## PowerShell Guidelines

| Area           | Rule                                                        |
|----------------|-------------------------------------------------------------|
| Cmdlet Verbs   | Use approved verbs (Get, Set, New, Remove, Test, Invoke)    |
| Function Help  | Comment-based help required for public functions            |
| Pipeline       | Support pipeline input where natural                        |
| Parameters     | Use parameter attributes for validation (ValidateSet, etc.) |
| Error Handling | Throw for unrecoverable issues; otherwise Write-Error       |

Prefer idempotent operations and fast fail semantics.

### Encoding Policy

All PowerShell source files (.ps1/.psm1/.psd1) SHOULD be saved as UTF-8 *without* BOM.

Rationale:

- PowerShell 7+ defaults to UTF-8 (no BOM) on all platforms.
- Avoids noisy diffs and issues with tooling that interprets BOM differently.
- Keeps repository consistent with cross-platform editors (VS Code, JetBrains, vim, etc.).

Implementation details:

- ScriptAnalyzer rule `PSUseBOMForUnicodeEncodedFile` is excluded in `PSScriptAnalyzerSettings.psd1`.
- Use `Utility/Remove-BomFromScripts.ps1` to strip BOMs from legacy files if needed.
- The normalization script (`Utility/Normalize-Files.ps1`) writes with a noBOM encoding by default.

Example:

```powershell
pwsh ./Utility/Normalize-Files.ps1 -Root ./src/PowerShell/Kestrun 
pwsh ./Utility/Remove-BomFromScripts.ps1 -Path ./src/PowerShell/Kestrun -Recurse -WhatIf
```

If you contribute from Windows and your editor adds a BOM automatically, please disable that setting for this repository.

## Testing Principles

| Topic       | Guidance                               |
|-------------|----------------------------------------|
| Granularity | One assertion concept per test         |
| Determinism | No external network/timezone surprises |
| Coverage    | Focus on critical logic paths first    |
| Naming      | `Method_Scenario_Expectation`          |
| Isolation   | Avoid shared mutable global state      |

## Logging Style

Follow the emoji/status conventions in the [Logging & Console Output](logging-style) guide.

## Documentation Quality

- Keep introductions concise.
- Use active voice.
- Provide runnable code where possible.
- Cross-link related pages.

## Pull Request Expectations

| Requirement         | Reason                    |
|---------------------|---------------------------|
| Tests updated/added | Guard against regressions |
| Docs aligned        | User discoverability      |
| Build passes        | Baseline confidence       |
| Style followed      | Consistency               |
| No dead code        | Reduce maintenance burden |

## Anti-Patterns (Avoid)

| Anti-Pattern                  | Why to Avoid                 |
|-------------------------------|------------------------------|
| Large God classes             | Hard to test and extend      |
| Hidden side effects           | Surprising consumer behavior |
| Catch-all `try { } catch { }` | Masks real failures          |
| Premature optimization        | Obscures readability         |
| Overusing dynamic             | Hurts analyzability + perf   |

---
Last updated: {{ site.time | date: '%Y-%m-%d' }}
