---
layout: default
parent: Guides
title: Imperative vs Declarative Routing
nav_order: 141
---

# Imperative vs Declarative Routing

Kestrun supports two common PowerShell routing styles: **imperative routing** (map routes explicitly) and **declarative attribute routing**
(decorate route functions with attributes).

This guide explains what each style does, how they differ, and when to prefer one over the other.

## Quick definitions

### Imperative routing (explicit mapping)

You explicitly register routes with `Add-KrMapRoute` by providing the HTTP verb, pattern, and handler script block.

Example (from the Hello World tutorial):

- Full source: **[1.1-Hello-World.ps1](/pwsh/tutorial/examples/1.1-Hello-World.ps1)**

```powershell
Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    Write-KrTextResponse -InputObject 'Hello, World!' -StatusCode 200
}
```

### Declarative attribute routing (function + attributes)

You define a function and decorate it with `[OpenApiPath]` (and optionally `[OpenApiResponse]`, `[OpenApiRequestBody]`, etc.).
Kestrun uses those attributes to map the function to an HTTP endpoint and to generate OpenAPI metadata.

Example (from the OpenAPI Hello World tutorial):

- Full source: **[10.1-OpenAPI-Hello-World.ps1](/pwsh/tutorial/examples/10.1-OpenAPI-Hello-World.ps1)**

```powershell
function getGreeting {
    [OpenApiPath(HttpVerb = 'get' , Pattern = '/greeting' )]
    param()

    Write-KrTextResponse -Text 'Hello, World!' -StatusCode 200
}
```

## What changes (and what doesn’t)

### What changes

- **How endpoints are registered**:
  - Imperative: you call `Add-KrMapRoute` per route.
  - Declarative: you write functions and attach route attributes.
- **How OpenAPI is authored**:
  - Imperative: OpenAPI metadata is typically attached via route options/parameters.
  - Declarative: OpenAPI metadata is written right next to the handler via attributes like `[OpenApiPath]` and `[OpenApiResponse]`.

### What doesn’t change

- **Runtime response behavior**: status codes, headers, and body are still controlled by what your handler writes.
- **Middleware behavior**: middleware such as Status Code Pages, CORS, compression, authentication, etc., runs the same regardless of routing style.

## Status codes: runtime vs documentation (important)

With declarative attribute routing, it’s easy to assume:

- `[OpenApiResponse(StatusCode = '404')]` means the endpoint will return 404.

It does **not**.

- `[OpenApiResponse(...)]` is documentation.
- Your code must set the runtime status (for example `Write-KrStatusResponse -StatusCode 404`, or `Write-KrTextResponse ... -StatusCode 404`).

This distinction matters for middleware like Status Code Pages, which only activates when the runtime response is a 4xx/5xx *and* the response has no body.

## When to use which

Use **imperative routing** when:

- You want the simplest mental model (explicit registration).
- You’re building quick demos/scripts with a small number of routes.
- You want routes to be very obvious during setup.

Use **declarative attribute routing** when:

- You want OpenAPI metadata to live next to the handler.
- You have many endpoints and want consistent, discoverable documentation.
- You want to lean on attribute-based documentation patterns (`[OpenApiPath]`, `[OpenApiResponse]`, `[OpenApiRequestBody]`, component schemas, etc.).

## Related

- [Routing & Conventions](./routing)
- [OpenAPI Generation](./openapi)
- [Status Code Pages](./statuscodepages)

---

Return to the [Guides index](./index).
