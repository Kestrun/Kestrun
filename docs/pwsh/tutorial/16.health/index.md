---
title: Health
parent: Tutorials
nav_order: 16
---

# Health Monitoring Tutorials

This section covers creating and extending a health endpoint, adding probes (script, HTTP, process,
C# inline code, disk), tagging, filtering, and operational options (timeouts, parallelism, degraded
handling).

## Chapters

| # | Topic | Summary |
|---|-------|---------|
| 1 | [Quickstart](./1.Health-Quickstart) | Basic endpoint + script & HTTP probes |
| 2 | [Script Probe](./2.Health-Script-Probe) | Custom latency script returning metrics |
| 3 | [HTTP Probe](./3.Health-Http-Probe) | Check internal route availability |
| 4 | [Process Probe](./4.Health-Process-Probe) | Verify external tooling presence |
| 5 | [C# Inline Probe](./5.Health-CSharp-Probe) | Randomized status via inline C# |
| 6 | [Disk Probe](./6.Health-Disk-Probe) | Override auto-registered disk thresholds |

Planned:

- Advanced: multi-probe dashboard, auth-protected health, OpenAPI metadata
- Scheduling remediation patterns (auto remediation after unhealthy state)

Return to the [Tutorial Index](../index).
