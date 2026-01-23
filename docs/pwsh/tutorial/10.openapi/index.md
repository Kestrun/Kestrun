---
title: OpenApi
parent: Tutorials
nav_order: 10
has_children: true
---

# OpenAPI Specification

Build self-documenting REST APIs using OpenAPI 3.1 specification with component schemas, request bodies, responses, and parameters.

## Tutorials

Browse the step-by-step guides below to learn OpenAPI features in Kestrun:

1. **[Hello World](./1.Hello-World)** — Create a simple OpenAPI spec with a single endpoint
2. **[Component Schemas](./2.Component-Schemas)** — Define reusable request and response schemas
3. **[RequestBody Components](./3.RequestBody-Components)** — Create reusable request body components
4. **[Parameter Components](./4.Parameter-Components)** — Define reusable query and path parameters
5. **[Response Components](./5.Response-Components)** — Create consistent response structures
6. **[Complete Components](./6.Complete-Components)** — Combine all component types in a full API
7. **[Tags and External Docs](./7.Tags-and-Docs)** — Group operations and attach documentation links
8. **[Document Information](./8.Document-Info)** — Populate title, contact, license, and server metadata
9. **[Component Headers](./9.Component-Headers)** — Define reusable response headers
10. **[Component Links](./10.Component-Links)** — Define operation links
11. **[Component Callbacks](./11.Component-Callbacks)** — Define asynchronous callbacks
12. **[WebHooks](./12.WebHooks)** — Define top-level webhooks (Planned)
13. **[Examples](./13.Examples)** — Component and inline examples for requests, responses, and parameters
14. **[Swagger Petstore](./14.Petstore-API)** — Full implementation of the Swagger Petstore API
15. **[Redocly Museum API](./15.Museum-API)** — Full implementation of the Redocly Museum API
16. **[Multiple OpenAPI Documents](./18.Multi-Document-OpenAPI)** — Serve multiple OpenAPI documents (default + webhook) from one server
17. **[Product Search with HTTP QUERY](./19.Product-Search-Query)** — OpenAPI 3.2 QUERY method with request body search filters
18. **[SSE (OpenAPI)](./20.Sse)** — Document a `text/event-stream` SSE endpoint
19. **[SSE Broadcast (OpenAPI)](./21.Sse-Broadcast)** — Document broadcast SSE + a trigger API
20. **[SignalR (OpenAPI)](./22.SignalR)** — Document SignalR-adjacent HTTP routes with OpenAPI
21. **[XML Modeling](./23.XML-Modeling)** — OpenAPI 3.2 XML metadata (`OpenApiXml`) for schemas
22. **[RFC 6570 Variable Mapping](./24.RFC6570-Variable-Mapping)** — RFC6570 path expressions (`{+path}`) mapped from ASP.NET route values

## Quick start: run the samples

From the repository root:

```powershell
# 1) Hello World - minimal OpenAPI spec
pwsh .\docs\_includes\examples\pwsh\10.1-OpenAPI-Hello-World.ps1

# 2) Component schemas - request/response definitions
pwsh .\docs\_includes\examples\pwsh\10.2-OpenAPI-Component-Schema.ps1

# 3) RequestBody components - reusable request structures
pwsh .\docs\_includes\examples\pwsh\10.3-OpenAPI-Component-RequestBody.ps1

# 4) Parameter components - reusable query/path parameters
pwsh .\docs\_includes\examples\pwsh\10.4-OpenAPI-Component-Parameter.ps1

# 5) Response components - reusable response structures
pwsh .\docs\_includes\examples\pwsh\10.5-OpenAPI-Component-Response.ps1

# 6) Complete components - full API with all component types
pwsh .\docs\_includes\examples\pwsh\10.6-OpenAPI-Components-RequestBody-Response.ps1

# 7) Tags and external docs - group operations and link docs
pwsh .\docs\_includes\examples\pwsh\10.7-OpenAPI-Tags.ps1

# 8) Document info - contact, license, servers
pwsh .\docs\_includes\examples\pwsh\10.8-OpenAPI-Document-Info.ps1

# 9) Component headers - reusable response headers
pwsh .\docs\_includes\examples\pwsh\10.9-OpenAPI-Component-Header.ps1

# 10) Component links - operation relationships
pwsh .\docs\_includes\examples\pwsh\10.10-OpenAPI-Component-Link.ps1

# 13) Examples - component + inline examples
pwsh .\docs\_includes\examples\pwsh\10.13-OpenAPI-Examples.ps1
```

## Key Concepts

- **OpenAPI 3.0+ Specifications (including 3.2)**: Industry-standard API documentation format
- **Component Schemas**: Reusable data structures defined once and referenced multiple times
- **RequestBody Components**: Standardized request payloads across endpoints
- **Parameter Components**: Consistent query string, path, header, and cookie parameters
- **Response Components**: Uniform response structures for success and error cases
- **Swagger UI**: Interactive API documentation and testing interface
- **Code Generation**: Use OpenAPI specs to generate client SDKs

## Documentation Routes

Kestrun provides multiple documentation viewers for your OpenAPI spec:

- **Swagger UI** — Classic, feature-rich interactive documentation
- **ReDoc** — Clean, responsive documentation layout
- **Scalar** — Modern, beautiful API explorer
- **RapidOC** — Fast, performant alternative
- **Elements** — StopLight Elements API viewer

Visit `http://localhost:5000/swagger`, `/redoc`, `/scalar`, `/rapidoc`, or `/elements` to view the documentation (paths vary by script).
