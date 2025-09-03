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

Planned additions:

- Caching patterns (LRU / TTL wrappers)
- Periodic persistence & snapshotting

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

---

## Next

Proceed to: [Using Shared Variables](./1.Shared-Variables) → then [Logging](../5.logging/index)

---

Contributions & ideas welcome — open an issue or PR.
