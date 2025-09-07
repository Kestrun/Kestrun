---
title: Endpoints
parent: Tutorials
nav_order: 6
---

# Introduction to Endpoints

> 🚧 **Work in Progress**
>
> This page is currently under development. Content will be expanded with guides, examples, and best practices soon.
> Thank you for your patience while we build it out.

## Quick start: run the samples

From the repository root:

```powershell
# 1) Multiple content types
pwsh .\examples\PowerShell\Tutorial\2-Multiple-Content-Types.ps1

# 2) Multi-language routes (PS/C#/VB)
pwsh .\examples\PowerShell\Tutorial\3-Multi-Language-Routes.ps1
```

Then browse the routes (default listener: <http://127.0.0.1:5000>):

- 2-Multiple-Content-Types: GET /hello, /hello-json, /hello-xml, /hello-yaml
- 3-Multi-Language-Routes: GET /hello (defined in PowerShell, C#, and VB.NET examples)

Stop the server with Ctrl+C in the terminal.

## What each sample shows

### 2-Multi-Language-Routes: Content negotiation made simple

- Return JSON, XML, YAML, and plain text using dedicated helpers
- See how to call `Write-KrJsonResponse`, `Write-KrXmlResponse`, `Write-KrYamlResponse`, and `Write-KrTextResponse`

### 3-Multiple-Content-Types: Mix languages inline

- Keep your server and plumbing in PowerShell
- Author individual routes in C# or VB.NET using the `-Language` and `-Code` parameters

## Next steps

- Dive into the other Tutorial chapters (Certificates, Logging, Razor Pages, Scheduling)
- Explore the richer example scripts under `examples/PowerShell` (e.g., `MultiRoutes.ps1`)
- Browse the PowerShell cmdlet reference under `docs/pwsh/cmdlets`
- Explore the PowerShell module source at `src/PowerShell/Kestrun`
