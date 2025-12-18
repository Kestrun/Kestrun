---
title: Razor
parent: Tutorials
nav_order: 12
---

# Razor Pages Tutorials

Render ASP.NET Core Razor Pages while building per-request models in PowerShell.

Kestrun can run a sibling PowerShell script (for example, `Index.cshtml.ps1`) before Razor renders the matching `.cshtml` file.
Use `Add-KrPowerShellRazorPagesRuntime` to enable the runtime and point it at your pages folder.

## How it works

During a single HTTP request the flow looks like this:

```text
┌───────────────┐
│  Browser      │  GET /Hello
└────┬──────────┘
    │
┌────▼──────────┐
│ PS-Razor MW   │  ① runs Hello.cshtml.ps1
│ (Kestrun)     │     – builds $Model
└────┬──────────┘
    │  attaches model for Razor
┌────▼──────────┐
│ Razor engine  │  ② renders Hello.cshtml
│ (Razor Pages) │    – reads Model.Data
└────┬──────────┘
    │
┌────▼──────────┐
│   Response    │  HTML sent back
└───────────────┘
```

## Chapters

| # | Topic | Summary |
|---|-------|---------|
| 1 | [Razor Pages Quickstart](./1.RazorPages-Quickstart) | Serve a small set of pages from a folder and share app-wide state across page scripts |
| 2 | [Razor Pages with Antiforgery](./2.RazorPages-Antiforgery) | Protect unsafe endpoints using cookie + header antiforgery tokens, and expose a token endpoint |
| 3 | [Pages Reference](./Pages/index) | Walk through each `.cshtml` and `.cshtml.ps1` file included in the sample site |

Return to the [Tutorial Index](../index).

### `Pages/ps/Form.cshtml`

```razor
@page
@model Kestrun.PowerShellPageModel
@{
    Layout = null;
    dynamic data = Model.Data;
}

<!DOCTYPE html>
<html>
<head><meta charset="utf-8" /><title>PS Form</title></head>
<body>
  <h1>Contact Form (PowerShell)</h1>

  @if (data.Submitted) {
      <p>Thank you, <strong>@data.Name</strong>! We’ll email you at <em>@data.Email</em>.</p>
  }
  else {
      <form method="post">
          <label>Name:  <input name="Name" /></label><br />
          <label>Email: <input name="Email" /></label><br />
          <button type="submit">Submit</button>
      </form>
  }
</body>
</html>
```

#### `Pages/ps/Form.cshtml.ps1`

```powershell
param($Context)

# On POST, collect form fields; on GET, show blank form
if ($Context.Request.Method -eq 'POST') {
    $form = $Context.Request.Form
    $Model = [pscustomobject]@{
        Submitted = $true
        Name      = $form['Name']
        Email     = $form['Email']
    }
} else {
    $Model = [pscustomobject]@{
        Submitted = $false
        Name      = $null
        Email     = $null
    }
}
```

---

### 5.3 C# Razor-Page form example

#### `Pages/cs/Form.cshtml`

```razor
@page
@model RazorSample.Pages.CSharpFormModel
@{
    Layout = null;
}

<!DOCTYPE html>
<html>
<head><meta charset="utf-8" /><title>C# Form</title></head>
<body>
  <h1>Contact Form (C#)</h1>

  @if (Model.Submitted) {
      <p>Thank you, <strong>@Model.Name</strong>! We’ll email you at <em>@Model.Email</em>.</p>
  }
  else {
      <form method="post">
          <div>
            <label asp-for="Name"></label>
            <input asp-for="Name" />
          </div>
          <div>
            <label asp-for="Email"></label>
            <input asp-for="Email" />
          </div>
          <button type="submit">Submit</button>
      </form>
  }
</body>
</html>
```

#### `Pages/cs/Form.cshtml.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RazorSample.Pages;

public class CSharpFormModel : PageModel
{
    [BindProperty]
    public string? Name { get; set; }

    [BindProperty]
    public string? Email { get; set; }

    public bool Submitted { get; private set; }

    public void OnGet()
    {
        // renders the form
    }

    public void OnPost()
    {
        // called on POST, with antiforgery token validated
        Submitted = true;
    }
}
```

---

## 6. Advanced examples

### 6.1 Query a database and cache

```powershell
param($id)   # accepts route & query parameters

$cache = $Services.GetService([IMemoryCache])
if (-not $cache.TryGetValue($id, [ref]$Model)) {
    $db = $Services.GetService([MyApp.Data.PersonRepository])
    $Model = $db.GetPerson($id)
    $cache.Set($id, $Model, [TimeSpan]::FromMinutes(10))
}

$Log.Information("Served person {Id}", $id)
```

### 6.2 Custom 404

```powershell
if (-not (Test-Path "data/$($Context.Request.RouteValues.id).json")) {
    $Context.Response.Status(404)
             .WriteText("No such record")
    return   # skip Razor entirely
}
```

---

## 7. Tips & best practices

- **Strong typing helps** – cast `$Model` to a real .NET class for IntelliSense in Razor (`@model Person`).
- **Keep business logic out of `.ps1`** – call C# services from DI instead.
- **One script = one request** – avoid long-running background work; offload to hosted services.
- **Case matters on Linux** – name files and hit URLs with matching case.
  `Hello.cshtml` → `/Hello` (not `/hello`) if deploying to Linux containers.
- **Hot reload** – edit `.ps1` or `.cshtml`, save, refresh; no rebuild required.
- **Logging** – use `$Log` or the `[Serilog.Log]` static to record diagnostics.
  (See **Logging** for full details.)

---

## 8. FAQ

| Question                                    | Answer                                                                                                                                   |
|---------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------|
| *Can I share code between scripts?*         | Yes. Using the SharedState feature                                                                                                       |
| *How do I inject DI services?*              | Resolve them from `$Services` or add them as parameters and decorate the script with `param($mySvc)` — Kestrun binds params from DI too. |
| *Can the script short-circuit the request?* | Absolutely. Return, or set `$Context.Response.Status() / Redirect()` **and** `return`, and Razor won’t run.                              |
| *Layout / partials?*                        | Works exactly as in normal ASP.NET Core Razor; place `_Layout.cshtml`, use `@{ Layout = "_Layout"; }`.                                   |
| *Why do I get “endpoint not found”?*        | Ensure `UseRouting()` and `MapRazorPages()` are in the pipeline **after** `UsePowerShellRazorPages()`.                                   |

---

## 9. Reference snippets

### Register the middleware manually

```csharp
app.UsePowerShellRazorPages(runspacePool,
    pagesRoot: Path.Combine(env.ContentRootPath, "Pages"),
    pattern: "**/*.cshtml.ps1");   // glob optional
```

### Build a runspace pool yourself

```csharp
var pool = new KestrunRunspacePoolManager(min: 2, max: 32);
app.UsePowerShellRazorPages(pool);
```
