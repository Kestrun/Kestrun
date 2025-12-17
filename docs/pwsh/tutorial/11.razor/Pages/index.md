---
title: Pages
parent: Razor
nav_order: 3
has_children: true
---

# Pages

Understand what each sample `.cshtml` view (and optional `.cshtml.ps1` sibling script) does in the Razor Pages demo.

## Included pages

| Page | URL | Model source |
|------|-----|--------------|
| Index | `/` | `Index.cshtml.ps1` sets `$Model` |
| About | `/About` | `About.cshtml.ps1` sets `$Model` |
| Status | `/Status` | `Status.cshtml.ps1` reads `$Context` and sets `$Model` |
| Quotes | `/Quotes` | `Quotes.cshtml.ps1` generates a quote and sets `$Model` |
| Contact | `/Contact` | `Contact.cshtml.ps1` handles GET/POST form data |
| AppInfo | `/AppInfo` | `AppInfo.cshtml.ps1` reads `$AppInfo`, `$FeatureFlags`, `$Motd` |
| Cancel | `/Cancel` | `Cancel.cshtml.ps1` exposes feature flags; `.cshtml` hosts SignalR + antiforgery JS |

## Layout and shared files

| File | Purpose |
|------|---------|
| `_ViewImports.cshtml` | Imports namespaces and enables built-in tag helpers |
| `_ViewStart.cshtml` | Sets the layout for all pages |
| `Shared/_Layout.cshtml` | Defines the site shell (nav/header/footer) |

## References

- [Add-KrPowerShellRazorPagesRuntime][Add-KrPowerShellRazorPagesRuntime]

---

### Previous / Next

{: .fs-4 .fw-500}

Previous: [Razor](../index)
Next: [Index](./1.Index)

[Add-KrPowerShellRazorPagesRuntime]: /pwsh/cmdlets/Add-KrPowerShellRazorPagesRuntime
