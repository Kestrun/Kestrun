---
layout: default
title: MCP Server
parent: Guides
nav_order: 37
---

# Kestrun MCP Server

`Kestrun.Mcp` is a stdio-based Model Context Protocol (MCP) server for local Kestrun apps.
It starts a Kestrun PowerShell script in-process, inspects the resulting host, and exposes safe MCP tools for route discovery, OpenAPI inspection, runtime inspection, request validation, and optional route invocation.

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

## Safety Model

The default posture is local and non-destructive:

- `kestrun.invoke_route` is disabled unless you explicitly enable it with one or more `--allow-invoke` patterns.
- Invocation is limited to allowlisted paths.
- Response headers such as `Authorization`, `Cookie`, `Set-Cookie`, and `X-Api-Key` are redacted in tool output.
- Runtime inspection returns a safe configuration snapshot and does not expose secret values from environment variables, configuration, certificates, or auth tokens.

## Run The Server

From the repository root:

```powershell
dotnet run --project .\src\CSharp\Kestrun.Mcp\Kestrun.Mcp.csproj -- `
  --script .\docs\_includes\examples\pwsh\24.1-Mcp-Hello.ps1 `
  --kestrun-manifest .\src\PowerShell\Kestrun\Kestrun.psd1
```

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

## Example Script: Hello World

Sample file: `docs/_includes/examples/pwsh/24.1-Mcp-Hello.ps1`

With that script running through `Kestrun.Mcp`, an MCP client can call:

- `kestrun.list_routes` to see `/hello`
- `kestrun.inspect_runtime` to inspect listeners and uptime
- `kestrun.invoke_route` for `/hello` if `--allow-invoke /hello` is enabled

## Example Script: OpenAPI-Aware Route

Sample file: `docs/_includes/examples/pwsh/24.2-Mcp-OpenAPI.ps1`

This makes `kestrun.get_route` and `kestrun.get_openapi` useful for the same route:

- `kestrun.get_route` returns route metadata plus OpenAPI-derived request and response schemas when available.
- `kestrun.get_openapi` returns the generated OpenAPI document as structured JSON.
- `kestrun.validate_request` can explain likely `404`, `406`, or `415` outcomes before you send a request.

## MCP Client Connection

Any MCP-compatible client that supports stdio servers can launch `Kestrun.Mcp`.
The exact configuration shape depends on the client, but it typically looks like this:

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

## Notes

- `kestrun.get_openapi` supports selecting an OpenAPI version such as `2.0`, `3.0`, `3.1`, or `3.2`.
- `kestrun.get_route` can select a route by pattern and/or operation id.
- `kestrun.inspect_runtime` reports Kestrun runtime status, start time, uptime, listeners, and a safe configuration snapshot.
- `kestrun.validate_request` is intended for debugging and agent planning. It predicts likely framework outcomes from route metadata and known content-type / Accept constraints.
