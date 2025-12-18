---
title: Razor Pages
parent: Guides
nav_order: 145
---

# Razor Pages

Render HTML with ASP.NET Core Razor Pages while building per-request models in PowerShell.

Kestrun adds an opt-in runtime where each `.cshtml` page may have an optional sibling PowerShell script (`.cshtml.ps1`).
If the script exists, Kestrun runs it first and passes the resulting model into Razor.

## What is Razor?

Razor is the view engine used by ASP.NET Core to render HTML using `.cshtml` templates.
Razor Pages is the “page-centric” programming model on top of Razor: each URL maps to a page, and the page renders HTML.

In plain terms:

- A `.cshtml` file is a template that can mix HTML and C# expressions (for example `@DateTime.UtcNow`).
- The Razor runtime compiles templates into executable code (at build time in typical apps, or at runtime when using runtime compilation).
- When a request hits a page endpoint, Razor executes the compiled template and writes HTML to the response.

## How it works

During a single HTTP request the flow looks like this:

```text
┌───────────────┐
│  Browser      │  GET /About
└────┬──────────┘
     │
┌────▼──────────┐
│ PS-Razor MW   │  ① runs About.cshtml.ps1
│ (Kestrun)     │     – builds $Model
└────┬──────────┘
     │  attaches model for Razor
┌────▼──────────┐
│ Razor engine  │  ② renders About.cshtml
│ (Razor Pages) │    – reads Model.Data
└────┬──────────┘
     │
┌────▼──────────┐
│   Response    │  HTML sent back
└───────────────┘
```

Key ideas:

- **Naming convention**: `Page.cshtml` + `Page.cshtml.ps1`.
- **Model handoff**: the script assigns `$Model`; Razor reads it as `Model.Data`.
- **No compile step for the model**: editing the `.ps1` changes behavior immediately.

## How Kestrun integrates PowerShell

Kestrun’s PowerShell-backed Razor Pages pattern separates responsibilities:

- The `.cshtml` page focuses on markup and presentation.
- The sibling `.cshtml.ps1` script focuses on request-aware data preparation.

The important behavior is that the PowerShell script runs *before* Razor renders.
It assigns `$Model` (any object, commonly a `PSCustomObject`), and the Razor view reads it through the page model wrapper (for example `Model.Data`).

This gives you:

- A “live” model provider with no rebuild cycle for the model.
- Access to the current request context when preparing the model.
- A clean boundary: keep complex logic in PowerShell (or in DI services) and keep views readable.

> Note: The exact .NET type used by `@model ...` is a project implementation detail.
> In the sample pages, the model is `Kestrun.Razor.PwshKestrunModel` and the PowerShell data is exposed as `Model.Data`.

## Quick start

Use the tutorial sample scripts as a working reference:

- Tutorial: [Razor Pages Quickstart](/pwsh/tutorial/11.razor/1.RazorPages-Quickstart)
- Tutorial: [Razor Pages with Antiforgery](/pwsh/tutorial/11.razor/2.RazorPages-Antiforgery)
- Reference: [Pages Reference](/pwsh/tutorial/11.razor/Pages/index)

## Folder structure and URLs

Typical layout:

```text
Assets/
  Pages/
    _ViewImports.cshtml
    _ViewStart.cshtml
    Index.cshtml
    Index.cshtml.ps1
    About.cshtml
    About.cshtml.ps1
    Shared/
      _Layout.cshtml
```

Routing basics:

- `Index.cshtml` maps to `/`.
- `About.cshtml` maps to `/About`.
- Subfolders become path segments (for example `Admin/Users.cshtml` → `/Admin/Users`).

## What you can do in the PowerShell page script

The sibling page script is the place to gather request data, call services, and shape an object for Razor.
Common patterns:

- Read request data via `$Context` (method, path, headers, query, form).
- Resolve services from DI (if your runtime injects `$Services`).
- Use shared variables defined in the server script (for example `$AppInfo`, `$FeatureFlags`, `$Motd`).
- Short-circuit by writing directly to the response when appropriate.

## Lifecycle notes

- **GET requests**: commonly prepare a view model and render markup.
- **POST requests**: you can still render a page, but you’ll typically read `$Context.Request.Form` and shape the model as “submitted/failed”.
- **Short-circuiting**: if you write directly to the response (JSON, redirect, file) from the script, treat it as an API-like endpoint.

If you need background work or long-running operations, prefer the Tasks feature and report progress separately.

## Layout, imports, and shared view files

Razor Pages uses the standard ASP.NET Core conventions:

- `_ViewImports.cshtml` sets `@using` imports and enables tag helpers.
- `_ViewStart.cshtml` sets a default `Layout` for all pages.
- `Shared/_Layout.cshtml` defines the common page chrome.

In the sample, `_ViewStart.cshtml` uses `Shared/_Layout`, and the layout uses `@Context.Request.PathBase` when generating navigation links.

## Forms and antiforgery

For unsafe verbs (POST/PUT/PATCH/DELETE), protect endpoints with antiforgery.
The Razor antiforgery sample uses a cookie + header pattern:

- Cookie: `.Kestrun.AntiXSRF`
- Header: `X-CSRF-TOKEN`
- Token endpoint: `/csrf-token`

See:

- Guide: [Antiforgery](./antiforgery)
- Tutorial: [Razor Pages with Antiforgery](/pwsh/tutorial/11.razor/2.RazorPages-Antiforgery)

## Troubleshooting

| Symptom | Explanation | Action |
|---------|-------------|--------|
| Pages return 404 | Razor runtime not configured | Ensure `Add-KrPowerShellRazorPagesRuntime` is configured before `Enable-KrConfiguration` |
| Model is empty | Sibling script missing or not setting `$Model` | Add `Page.cshtml.ps1` and assign `$Model` |
| Layout not applied | `_ViewStart.cshtml` not discovered | Keep `_ViewStart.cshtml` in the pages root configured by the runtime |
| POST fails with 400/403 | Antiforgery enabled and token missing | Call `/csrf-token` first and send cookie + `X-CSRF-TOKEN` header |

## References

- Cmdlet: [Add-KrPowerShellRazorPagesRuntime](/pwsh/cmdlets/Add-KrPowerShellRazorPagesRuntime)
- Guide: [Antiforgery](./antiforgery)
- Tutorial: [Razor Pages Tutorials](/pwsh/tutorial/11.razor/)

## External references

- Microsoft Docs: [Introduction to Razor Pages](https://learn.microsoft.com/aspnet/core/razor-pages/)
- Microsoft Docs: [Razor syntax reference](https://learn.microsoft.com/aspnet/core/mvc/views/razor)
- Microsoft Docs: [Layouts in ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/views/layout)
- Microsoft Docs: [`_ViewImports.cshtml`](https://learn.microsoft.com/aspnet/core/mvc/views/layout#viewimports)
- Microsoft Docs: [`_ViewStart.cshtml`](https://learn.microsoft.com/aspnet/core/mvc/views/layout#viewstart)

Return to the [Guides index](./index).
