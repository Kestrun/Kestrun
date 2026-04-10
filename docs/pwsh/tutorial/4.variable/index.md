---
title: Shared Variable
parent: Tutorials
nav_order: 4
---

# Shared Variables Overview

Maintain lightweight in-memory state across requests and routes. This section covers how to declare and safely use
variables defined prior to configuration inside PowerShell route script blocks.

---

## Chapter

| Order | Chapter                                        | Focus                                                      |
|-------|------------------------------------------------|------------------------------------------------------------|
| 1     | [Using Shared Variables](./1.Shared-Variables) | Share thread-safe objects (counters, caches) across routes |
| 2     | [Shared State](./2.Shared-State)               | Register, query, and update explicit shared-state entries  |
| 3     | [Shared State Snapshots](./3.Shared-State-Snapshots) | Export, reset, and restore shared in-memory state |

---

## Quick Start

Run the sample:

```powershell
pwsh .\docs\pwsh\tutorial\examples\4.1-Shared-Variables.ps1
```

Then:

```powershell
curl http://127.0.0.1:5000/visit
curl http://127.0.0.1:5000/show
```

For explicit shared-state cmdlets, run:

```powershell
pwsh .\docs\pwsh\tutorial\examples\4.2-Shared-State.ps1
```

For snapshot export/import, run:

```powershell
pwsh .\docs\pwsh\tutorial\examples\4.3-Shared-State-Snapshots.ps1
```

---

## Next

Proceed to: [Using Shared Variables](./1.Shared-Variables) → [Shared State](./2.Shared-State) → [Shared State Snapshots](./3.Shared-State-Snapshots) → [Logging](../5.logging/index)

---

Contributions & ideas welcome — open an issue or PR.
