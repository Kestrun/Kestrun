---
title: Razor
parent: Tutorials
nav_order: 11
---

# PowerShell-backed Razor Pages

> 🚧 **Work in Progress**
>
> This page is currently under development. Content will be expanded with guides, examples, and best practices soon.  
> Thank you for your patience while we build it out.
> *Dynamic ASP.NET Core UI powered by PowerShell scripts — all inside **Kestrun***
> Plus: how to collect form data both from PowerShell and C# Razor Pages

---

## 1. Overview

Kestrun lets you pair a regular **`.cshtml`** Razor view with a sibling **PowerShell script**.
During a single HTTP request the pipeline looks like this:

```text
┌───────────────┐
│  Browser      │  GET /Hello
└────┬──────────┘
     │
┌────▼──────────┐
│ PS-Razor MW   │  ① runs Hello.cshtml.ps1
│ (UsePower… )  │     – builds $Model
└────┬──────────┘
     │  sets HttpContext.Items["PageModel"]
┌────▼──────────┐
│ Razor engine  │  ② renders Hello.cshtml
│ (MapRazorPages)│    – @model PowerShellPageModel
└────┬──────────┘
     │
┌────▼──────────┐
│   Response    │  HTML sent back
└───────────────┘
```

### Advantages

* **Zero compile step** — change the `.ps1` file, hit *F5*, refresh.
* **Full access to Kestrun abstractions** (`$Context.Request`, `$Context.Response`, loggers, DI).
* **Razor tooling** — syntax highlighting, IntelliSense, TagHelpers, layout views, etc.

---

## 2. Folder & naming convention

```folder
MyApp/
└─ Pages/
   ├─ Hello.cshtml         ← Razor markup
   ├─ Hello.cshtml.ps1     ← PowerShell executed first
   ├─ ps/
   │  └─ Form.cshtml       ← PS form view
   │  └─ Form.cshtml.ps1   ← PS form handler
   ├─ cs/
   │  └─ Form.cshtml       ← C# form view
   │  └─ Form.cshtml.cs    ← C# form PageModel
   └─ _Layout.cshtml       ← optional shared layout
```

* URL rule: `/Pages/Hello.cshtml` → **`/Hello`**
* Sub-folders map to path segments (`/ps/Form` → `/ps/Form`, `/cs/Form` → `/cs/Form`).

---

## 3. Enabling the middleware

```csharp
var server = new KestrunHost("MySite", kestRunRoot, [modulePath]);

server.ConfigureKestrel(opts => { /* … */ });
server.ApplyConfiguration();   // wires middleware

/*
  Inside ApplyConfiguration():
    app.UseStaticFiles();
    app.UsePowerShellRazorPages(runspacePool);  // 👈 must come before
    app.UseRouting();
    app.MapRazorPages();
*/
```

> **Important:** `UsePowerShellRazorPages()` **must appear before** `MapRazorPages()` so `$Model` is ready when Razor runs.

---

## 4. Writing your first page

### 4.1 `Pages/Hello.cshtml`

```razor
@page
@model Kestrun.PowerShellPageModel

@{
    Layout = null;
    var data = Model.Data
              ?? new { Title = "Fallback", UserName = "Guest" };
}

<!DOCTYPE html>
<h1>@data.Title</h1>
<p>Welcome, @data.UserName!</p>
<p>Served at @DateTime.UtcNow:u</p>
```

### 4.2 `Pages/Hello.cshtml.ps1`

```powershell
<# Executed **before** Razor renders #>

param($Context)

# Build your model however you like:
$Model = [pscustomobject]@{
    Title    = 'PowerShell-backed Razor Page'
    UserName = 'Alice'
}

# Helpers available in script:
#   $Context.Request   – KestrunRequest
#   $Context.Response  – KestrunResponse
#   $Services  – IServiceProvider
#   $Log       – Serilog.ILogger
```

---

### 4.2 What variables are available in the script?

| Name                    | Type                     | Purpose                                               |
|-------------------------|--------------------------|-------------------------------------------------------|
| **`$Context.Request`**  | `KestrunRequest`         | Strong-typed wrapper over `HttpRequest` with helpers. |
| **`$Context.Response`** | `KestrunResponse`        | Convenience builder (status, headers, cookies…).      |
| **`$Services`**         | `IServiceProvider`       | Resolve any DI singleton/scoped service.              |
| **`$Log`**              | `Serilog.ILogger`        | Logger scoped to the current request.                 |
| **`$Model`**            | `object` (you create it) | Anything serialisable / anonymous / PSCustomObject.   |

Return values are ignored; simply assign to `$Model`.

---

## 5. Working with Forms

### 5.1 Add a `_ViewImports.cshtml`

Create **`Pages/_ViewImports.cshtml`** (alongside `ps/` and `cs/`):

```razor
@using RazorSample.Pages
@using Kestrun
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@namespace RazorSample.Pages
```

* **`@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`** enables `<form asp-for>` and auto-injects antiforgery tokens.
* **`@using`** brings your PageModels and `PowerShellPageModel` into scope.
* **`@namespace`** sets the default C# namespace for views.

### 5.2 PowerShell-backed form example

#### `Pages/ps/Form.cshtml`

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

* **Strong typing helps** – cast `$Model` to a real .NET class for IntelliSense in Razor (`@model Person`).
* **Keep business logic out of `.ps1`** – call C# services from DI instead.
* **One script = one request** – avoid long-running background work; offload to hosted services.
* **Case matters on Linux** – name files and hit URLs with matching case.
  `Hello.cshtml` → `/Hello` (not `/hello`) if deploying to Linux containers.
* **Hot reload** – edit `.ps1` or `.cshtml`, save, refresh; no rebuild required.
* **Logging** – use `$Log` or the `[Serilog.Log]` static to record diagnostics.
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
