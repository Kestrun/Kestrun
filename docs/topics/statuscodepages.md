---
layout: default
parent: Topics
title: Status Code Pages
nav_order: 8
---

# Status Code Pages

Status Code Pages middleware provides custom error pages for HTTP status codes
between 400 and 599 that don't have a response body. This is essential for
providing user-friendly error pages instead of default browser error messages.

## Overview

Kestrun provides several ways to configure status code pages:

1. **Default Status Code Pages** - Basic error pages with minimal styling
2. **Custom Options** - Configure with `StatusCodePagesOptions` for more control
3. **Custom Handler** - Use a delegate function to handle status codes programmatically
4. **Script Handler** - Use PowerShell scripts to generate custom error responses
5. **Content Format** - Specify custom content type and body format with placeholders
6. **Redirects** - Redirect error responses to specific URLs
7. **Re-execute** - Re-execute the request pipeline with a different path

## When to Use Status Code Pages

- **404 Not Found** - Provide custom "page not found" messages
- **500 Internal Server Error** - Show friendly error pages instead of stack traces
- **403 Forbidden** - Display access denied pages
- **401 Unauthorized** - Redirect to login pages or show authentication required messages

## PowerShell Cmdlet

Use `Enable-KrStatusCodePage` to configure status code pages middleware with various options:

```powershell
# Default status code pages
Enable-KrStatusCodePage

# With PowerShell script handler (custom body)
Enable-KrStatusCodePage -ScriptBlock {
    param($Context)
    $status = $Context.Response.StatusCode
    $Context.Response.ContentType = 'text/plain'
    $Context.Response.WriteAsync("Custom handler status: $status") | Out-Null
}

# With LanguageOptions (advanced: cross-language code)
$lo = [Kestrun.Hosting.Options.LanguageOptions]::new()
$lo.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
$lo.Code = @'
param($Context)
$Context.Response.ContentType = "text/plain"
$Context.Response.WriteAsync("Handled by LanguageOptions") | Out-Null
'@
Enable-KrStatusCodePage -LanguageOptions $lo

# With content format
Enable-KrStatusCodePage -ContentType "text/html" -BodyFormat $htmlTemplate

# With redirects
Enable-KrStatusCodePage -LocationFormat "/error/{0}"

# With re-execution
Enable-KrStatusCodePage -PathFormat "/errors/{0}" -QueryFormat "originalPath={0}"
```

## Sample Scripts

The following examples demonstrate different approaches to handling status codes:

- **[17.1-StatusCodePages-Default.ps1](/pwsh/tutorial/examples/17.1-StatusCodePages-Default.ps1)** — Default status code pages
- **[17.2-StatusCodePages-Options.ps1](/pwsh/tutorial/examples/17.2-StatusCodePages-Options.ps1)** — Options-style configuration (template/redirect/re-exec)
- **[17.3-StatusCodePages-CustomPowerShell.ps1](/pwsh/tutorial/examples/17.3-StatusCodePages-CustomPowerShell.ps1)** — Custom handler with PowerShell script block
- **[17.4-StatusCodePages-CustomCSharp.ps1](/pwsh/tutorial/examples/17.4-StatusCodePages-CustomCSharp.ps1)** — Custom handler with inline C#
- **[17.5-StatusCodePages-ContentFormat.ps1](/pwsh/tutorial/examples/17.5-StatusCodePages-ContentFormat.ps1)** — Content type and body format
- **[17.6-StatusCodePages-Redirects.ps1](/pwsh/tutorial/examples/17.6-StatusCodePages-Redirects.ps1)** — Redirect to error pages
- **[17.7-StatusCodePages-ReExecute.ps1](/pwsh/tutorial/examples/17.7-StatusCodePages-ReExecute.ps1)** — Re-execute with different paths

Each example builds on the hello-world template and shows how to trigger and handle different status codes.

## Best Practices

1. **Always handle 404 errors** - Provide meaningful "not found" pages
2. **Secure error messages** - Don't expose sensitive information in error pages
3. **Consistent styling** - Match your application's design
4. **Logging** - Log errors while showing friendly messages to users
5. **Testing** - Test error pages with various status codes

## Advanced Configuration

### Custom Error Pages with External Files

For better maintainability, separate HTML templates from PowerShell code:

```powershell
# Load HTML template from external file
$htmlTemplatePath = Join-Path $PSScriptRoot "Assets\wwwroot\statuscodepages\error-template.html"
$htmlTemplate = Get-Content -Path $htmlTemplatePath -Raw

# Use with content format
Enable-KrStatusCodePage -ContentType "text/html; charset=utf-8" -BodyFormat $htmlTemplate
```

### Error Page Hierarchies

Organize error pages by status code families:

- **4xx Client Errors** - User-facing friendly messages
- **5xx Server Errors** - Technical support contact information
- **Custom Status Codes** - Application-specific error handling

### Integration with Logging

Combine status code pages with structured logging:

```powershell
$handler = {
    param($context)
    $statusCode = $context.HttpContext.Response.StatusCode

    # Log the error with context
    Write-KrLog -Level Error -Message "Status code $statusCode" -Properties @{
        Path = $context.HttpContext.Request.Path
        UserAgent = $context.HttpContext.Request.Headers["User-Agent"]
    }

    # Generate custom response
    # ... error page logic
}

Enable-KrStatusCodePage -ScriptBlock $handler
```
