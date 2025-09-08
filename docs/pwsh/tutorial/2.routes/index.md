---
title: Routes
parent: Tutorials
nav_order: 2
---

# Introduction to Routes

## Quick start: run the samples

From the repository root:

```powershell
# 1) Multiple content types
pwsh .\examples\PowerShell\Tutorial\2-Multiple-Content-Types.ps1

# 2) Multi-language routes (PS/C#/VB)
pwsh .\examples\PowerShell\Tutorial\3-Multi-Language-Routes.ps1

# 3) Route parameters & request data
pwsh .\examples\PowerShell\Tutorial\2.3-Route-Parameters.ps1

# 4) Route options (MapRouteOptions)
pwsh .\examples\PowerShell\Tutorial\2.4-Route-Options.ps1

# 5) Route groups (shared prefix)
pwsh .\examples\PowerShell\Tutorial\2.5-Route-Group.ps1
```

Then browse the routes (default listener: <http://127.0.0.1:5000>):

- 2-Multiple-Content-Types: GET /hello, /hello-json, /hello-xml, /hello-yaml
- 3-Multi-Language-Routes: GET /hello (defined in PowerShell, C#, and VB.NET examples)
- 2.3-Route-Parameters: GET /input/{value}, PATCH /input?value=, POST /input (body), PUT /input (header), DELETE /input (cookie)
- 2.4-Route-Options: GET /xml/{message}, /yaml, /json, /txt
- 2.5-Route-Group: grouped endpoints under /input (same verbs / data sources inside a group)

Stop the server with Ctrl+C in the terminal.

## What each sample shows

### 2-Multiple-Content-Types: Content negotiation made simple

- Return JSON, XML, YAML, and plain text using dedicated helpers
- See how to call `Write-KrJsonResponse`, `Write-KrXmlResponse`, `Write-KrYamlResponse`, and `Write-KrTextResponse`

### 3-Multi-Language-Routes: Mix languages inline

- Keep your server and plumbing in PowerShell
- Author individual routes in C# or VB.NET using the `-Language` and `-Code` parameters

### 2.3-Route-Parameters: Read request data

- Demonstrates route, query, body, header, cookie extraction helpers
- Shows verb differentiation to avoid pattern collisions

### 2.4-Route-Options: Rich configuration

- Use `MapRouteOptions` / `New-KrMapRouteOption` for metadata & multi-language
- Mix hashtable, strongly typed, and pipeline construction styles

### 2.5-Route-Group: Shared prefix organization

- Encapsulate related endpoints under one base path
- Reduce duplication and simplify refactors (change the prefix once)

## Next steps

- Continue to Static Files tutorial for hosting assets
- Explore richer example scripts under `examples/PowerShell` (e.g., `MultiRoutes.ps1`)
- Browse the PowerShell cmdlet reference under `/pwsh/cmdlets`
- Inspect the PowerShell module source at `https://github.com/Kestrun/Kestrun/tree/main/src/PowerShell/Kestrun`
