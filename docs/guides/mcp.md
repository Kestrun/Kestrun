---
layout: default
title: MCP Server
parent: Guides
nav_order: 37
---

# Kestrun MCP Server

`Kestrun.Mcp` is a stdio-based Model Context Protocol (MCP) server for local Kestrun apps.
It starts a Kestrun PowerShell script in-process, inspects the resulting host, and exposes safe MCP tools for route discovery,
OpenAPI inspection, runtime inspection, request validation, and optional route invocation.

## What It Exposes

The initial tool surface is:

- `kestrun.list_routes`
- `kestrun.get_route`
- `kestrun.get_openapi`
- `kestrun.inspect_runtime`
- `kestrun.validate_request`
- `kestrun.invoke_route`

These tools are backed by Kestrun's own route metadata, OpenAPI document generation, and HTTP pipeline.
`kestrun.invoke_route` sends a real HTTP request to the running Kestrun listener. It does not bypass middleware, routing, validation, or content negotiation.

## What You Can Do With It

`Kestrun.Mcp` is most useful when you want an MCP client or coding agent to inspect a live local Kestrun app without reverse-engineering the script by hand.

Typical uses:

- discover the routes a script actually registered, including verbs, tags, summaries, request content types, and response content types
- inspect one route in detail, including OpenAPI-derived request and response schemas when the route is annotated
- fetch the generated OpenAPI document as structured JSON instead of scraping `/openapi/...` text output
- check whether a request is likely to fail with `404`, `406`, or `415` before sending it
- safely invoke allowlisted local routes through the normal HTTP pipeline for smoke testing and agent workflows

## Safety Model

The default posture is local and non-destructive:

- `kestrun.invoke_route` is disabled unless you explicitly enable it with one or more `--allow-invoke` patterns.
- Invocation is limited to allowlisted paths.
- Response headers such as `Authorization`, `Cookie`, `Set-Cookie`, and `X-Api-Key` are redacted in tool output.
- Runtime inspection returns a safe configuration snapshot and does not expose secret values from environment variables, configuration, certificates, or auth tokens.

## Build And Install

`Kestrun.Mcp` is a C# project in this repository and is included in `Kestrun.sln`.
The repository's standard `Invoke-Build Build` flow stays focused on the main Kestrun artifacts, so the MCP host has its own dedicated build task.

From the repository root, restore dependencies and build the MCP host explicitly:

```powershell
Invoke-Build Restore
Invoke-Build Build-KestrunMcp
```

If you prefer to build the project directly with `dotnet`:

```powershell
dotnet build .\src\CSharp\Kestrun.Mcp\Kestrun.Mcp.csproj
```

This produces `Kestrun.Mcp.dll` under:

- `src/CSharp/Kestrun.Mcp/bin/Debug/net10.0/`
- or the matching `Release` output if you build with `-c Release`

## What You Need

To use the MCP server, you need:

- a Kestrun PowerShell script to run
- the Kestrun module manifest, typically `src/PowerShell/Kestrun/Kestrun.psd1`
- a stdio-capable MCP client
- .NET 10 and PowerShell available on the local machine

The server starts the target script in-process, waits for a `KestrunHost`, and then exposes MCP tools against that running host.

## Run The Server

From the repository root:

```powershell
dotnet run --project .\src\CSharp\Kestrun.Mcp\Kestrun.Mcp.csproj -- `
  --script .\docs\_includes\examples\pwsh\24.1-Mcp-Hello.ps1 `
  --kestrun-manifest .\src\PowerShell\Kestrun\Kestrun.psd1
```

You can also launch the built DLL directly:

```powershell
dotnet .\src\CSharp\Kestrun.Mcp\bin\Debug\net10.0\Kestrun.Mcp.dll -- `
  --script .\docs\_includes\examples\pwsh\24.1-Mcp-Hello.ps1 `
  --kestrun-manifest .\src\PowerShell\Kestrun\Kestrun.psd1
```

## Command-Line Options

The initial command-line surface is:

- `--script <path>`: required path to the Kestrun PowerShell script to run
- `--kestrun-manifest <path>`: optional explicit `Kestrun.psd1` path
- `--host-name <name>`: optional named host when the script does not use the default host
- `--discover-pshome`: let the process discover `PSHOME` instead of setting it explicitly
- `--allow-invoke <pattern>`: enable `kestrun.invoke_route` and allow a path glob such as `/hello` or `/api/*`

If `--kestrun-manifest` is omitted, `Kestrun.Mcp` tries common local manifest locations before failing.

If the script registers a named host instead of using the default host, add `--host-name`:

```powershell
dotnet run --project .\src\CSharp\Kestrun.Mcp\Kestrun.Mcp.csproj -- `
  --script .\docs\_includes\examples\pwsh\24.1-Mcp-Hello.ps1 `
  --kestrun-manifest .\src\PowerShell\Kestrun\Kestrun.psd1 `
  --host-name MyHost
```

To enable request invocation for specific routes:

```powershell
dotnet run --project .\src\CSharp\Kestrun.Mcp\Kestrun.Mcp.csproj -- `
  --script .\docs\_includes\examples\pwsh\24.1-Mcp-Hello.ps1 `
  --kestrun-manifest .\src\PowerShell\Kestrun\Kestrun.psd1 `
  --allow-invoke /hello `
  --allow-invoke /api/*
```

## Configure A Script For MCP

The easiest setup is a normal local Kestrun script that:

1. creates a server
2. adds one or more listeners
3. enables configuration
4. registers routes
5. starts the server

Example:

```powershell
param([int]$Port = $env:PORT ?? 5000)

New-KrServer -Name 'MCP Hello'
Add-KrEndpoint -Port $Port
Enable-KrConfiguration

Add-KrMapRoute -Pattern '/hello' -Verbs Get -ScriptBlock {
    Write-KrJsonResponse @{ message = 'hello from Kestrun' }
}

Start-KrServer
```

The same script can still be run normally outside MCP. The MCP host just supplies the process/runtime wrapper around it.

## Configure An MCP Client

Any MCP client that supports stdio servers can launch `Kestrun.Mcp`.
The MCP client needs to know:

- the executable to start, usually `dotnet`
- the arguments needed to launch `Kestrun.Mcp`
- which script the MCP server should run
- whether route invocation should be enabled

Typical client configuration:

```json
{
  "mcpServers": {
    "kestrun": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "./src/CSharp/Kestrun.Mcp/Kestrun.Mcp.csproj",
        "--",
        "--script",
        "./docs/_includes/examples/pwsh/24.1-Mcp-Hello.ps1",
        "--kestrun-manifest",
        "./src/PowerShell/Kestrun/Kestrun.psd1",
        "--allow-invoke",
        "/hello"
      ]
    }
  }
}
```

If your client prefers launching a built binary instead of `dotnet run`, point it at the built DLL:

```json
{
  "mcpServers": {
    "kestrun": {
      "command": "dotnet",
      "args": [
        "./src/CSharp/Kestrun.Mcp/bin/Debug/net10.0/Kestrun.Mcp.dll",
        "--script",
        "./docs/_includes/examples/pwsh/24.1-Mcp-Hello.ps1",
        "--kestrun-manifest",
        "./src/PowerShell/Kestrun/Kestrun.psd1"
      ]
    }
  }
}
```

## How To Use It

Once the MCP client connects, a typical workflow is:

1. Call `kestrun.inspect_runtime` to confirm the host is running and listeners are active.
2. Call `kestrun.list_routes` to see the registered routes and metadata.
3. Call `kestrun.get_route` for one route when you need request/response schema detail.
4. Call `kestrun.get_openapi` when you want the full generated OpenAPI document.
5. Call `kestrun.validate_request` before invoking a route if you want likely `404`, `406`, or `415` outcomes explained.
6. Call `kestrun.invoke_route` only for explicitly allowlisted paths.

## Practical Walkthrough: Hello Route

Sample file: `docs/_includes/examples/pwsh/24.1-Mcp-Hello.ps1`

This script exposes one `GET /hello` route. It is the shortest useful MCP walkthrough because it shows route discovery, runtime inspection, and optional invocation.

Start the MCP server with route invocation enabled for `/hello`:

```powershell
dotnet run --project .\src\CSharp\Kestrun.Mcp\Kestrun.Mcp.csproj -- `
  --script .\docs\_includes\examples\pwsh\24.1-Mcp-Hello.ps1 `
  --kestrun-manifest .\src\PowerShell\Kestrun\Kestrun.psd1 `
  --allow-invoke /hello
```

Then use the MCP tools in this order.

### 1. Confirm The Runtime Is Ready

Tool call:

```json
{
  "tool": "kestrun.inspect_runtime",
  "arguments": {}
}
```

Typical result excerpt:

```json
{
  "applicationName": "MCP Hello",
  "status": "running",
  "routeCount": 1
}
```

### 2. Discover The Registered Route

Tool call:

```json
{
  "tool": "kestrun.list_routes",
  "arguments": {}
}
```

Typical result excerpt:

```json
[
  {
    "pattern": "/hello",
    "verbs": ["GET"]
  }
]
```

### 3. Invoke The Route Through Kestrun

Tool call:

```json
{
  "tool": "kestrun.invoke_route",
  "arguments": {
    "method": "GET",
    "path": "/hello"
  }
}
```

Typical result excerpt:

```json
{
  "statusCode": 200,
  "contentType": "application/json; charset=utf-8",
  "body": "{\"message\":\"hello from Kestrun\"}"
}
```

That is a real request through the running Kestrun listener. If `/hello` was not included in `--allow-invoke`,
the same tool call would return a `403` error with code `invoke_not_allowlisted`.

## Example Script: Hello World

Sample file: `docs/_includes/examples/pwsh/24.1-Mcp-Hello.ps1`

With that script running through `Kestrun.Mcp`, an MCP client can call:

- `kestrun.list_routes` to see `/hello`
- `kestrun.inspect_runtime` to inspect listeners and uptime
- `kestrun.invoke_route` for `/hello` if `--allow-invoke /hello` is enabled

## Practical Walkthrough: OpenAPI-Aware Route

Sample file: `docs/_includes/examples/pwsh/24.2-Mcp-OpenAPI.ps1`

This script exposes a `POST /items/{id}` route with OpenAPI metadata. It is the better example when you want to see how MCP helps an agent inspect schemas,
validate a request before sending it, and then invoke the route safely.

Start the MCP server with invocation enabled for the route family:

```powershell
dotnet run --project .\src\CSharp\Kestrun.Mcp\Kestrun.Mcp.csproj -- `
  --script .\docs\_includes\examples\pwsh\24.2-Mcp-OpenAPI.ps1 `
  --kestrun-manifest .\src\PowerShell\Kestrun\Kestrun.psd1 `
  --allow-invoke /items/*
```

### 1. Inspect A Route By `operationId`

Tool call:

```json
{
  "tool": "kestrun.get_route",
  "arguments": {
    "operationId": "createItem"
  }
}
```

Typical result excerpt:

```json
{
  "route": {
    "pattern": "/items/{id}",
    "verbs": ["POST"],
    "tags": ["items"],
    "operationId": "createItem"
  },
  "requestSchemas": {
    "application/json": {
      "type": "object"
    }
  }
}
```

### 2. Fetch The Generated OpenAPI Document

Tool call:

```json
{
  "tool": "kestrun.get_openapi",
  "arguments": {
    "version": "3.1"
  }
}
```

Typical result excerpt:

```json
{
  "documentId": "Default",
  "version": "3.1.2",
  "document": {
    "openapi": "3.1.2",
    "paths": {
      "/items/{id}": {
        "post": {}
      }
    }
  }
}
```

### 3. Validate A Request Before Sending It

Tool call:

```json
{
  "tool": "kestrun.validate_request",
  "arguments": {
    "method": "POST",
    "path": "/items/42",
    "headers": {
      "Content-Type": "application/json",
      "Accept": "application/json"
    },
    "body": {
      "name": "widget"
    }
  }
}
```

Typical result excerpt:

```json
{
  "isValid": true,
  "statusCode": 200,
  "message": "The request matches a registered route and satisfies known content-type/accept constraints."
}
```

A failure example with an incompatible `Accept` header:

```json
{
  "tool": "kestrun.validate_request",
  "arguments": {
    "method": "POST",
    "path": "/items/42",
    "headers": {
      "Content-Type": "application/json",
      "Accept": "text/plain"
    },
    "body": {
      "name": "widget"
    }
  }
}
```

Typical failure excerpt:

```json
{
  "isValid": false,
  "statusCode": 406,
  "error": {
    "code": "not_acceptable"
  }
}
```

### 4. Invoke The Route After Validation

Tool call:

```json
{
  "tool": "kestrun.invoke_route",
  "arguments": {
    "method": "POST",
    "path": "/items/42",
    "headers": {
      "Content-Type": "application/json",
      "Accept": "application/json"
    },
    "body": {
      "name": "widget"
    }
  }
}
```

Typical result excerpt:

```json
{
  "statusCode": 200,
  "contentType": "application/json; charset=utf-8",
  "body": "{\"itemId\":42,\"ok\":true}"
}
```

This makes `kestrun.get_route` and `kestrun.get_openapi` useful for the same route:

- `kestrun.get_route` returns route metadata plus OpenAPI-derived request and response schemas when available.
- `kestrun.get_openapi` returns the generated OpenAPI document as structured JSON.
- `kestrun.validate_request` can explain likely `404`, `406`, or `415` outcomes before you send a request.
- `kestrun.invoke_route` returns response headers too, but sensitive values such as `Set-Cookie` are redacted when present.

## Example Agent Tasks

Once the MCP server is connected, these are realistic things to ask an MCP-capable agent to do:

- "List the routes in my Kestrun script and tell me which ones accept JSON."
- "Show me the OpenAPI schema for `createItem`."
- "Check why `POST /items/42` with `Accept: text/plain` would fail."
- "Invoke `/hello` and summarize the JSON response."
- "Fetch the OpenAPI 3.1 document and compare it with the route metadata for `/items/{id}`."

## Notes

- `kestrun.get_openapi` supports selecting an OpenAPI version such as `2.0`, `3.0`, `3.1`, or `3.2`.
- `kestrun.get_route` can select a route by pattern and/or operation id.
- `kestrun.inspect_runtime` reports Kestrun runtime status, start time, uptime, listeners, and a safe configuration snapshot.
- `kestrun.validate_request` is intended for debugging and agent planning.
It predicts likely framework outcomes from route metadata and known content-type / Accept constraints.
