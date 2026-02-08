---
title: Tutorials
parent: PowerShell

nav_order: 0
---

# Tutorials

Step-by-step guides to build and ship with Kestrun. This index lists runnable sample scripts and documentation chapters in recommended learning order.

## Prerequisites

- PowerShell 7.4, 7.5, or 7.6 (preview)
- .NET (run-only scenarios do NOT require the full SDK)

| PowerShell Version | Install (Run Samples) | Notes |
|--------------------|-----------------------|-------|
| 7.4 / 7.5 | [.NET 8 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) | Bundles base runtime + ASP.NET Core |
| 7.6 (preview) | [.NET 10 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) | Preview - updates frequently |

If you already have the **.NET SDK** for those versions installed you don't need to install the runtime separately.

- Kestrun module: installed or available from this repository at `src/PowerShell/Kestrun/Kestrun.psm1`
- Supported OS: same as .NET 8/10 (Windows, Linux, macOS), including ARM/ARM64

Verify (optional):

```powershell
dotnet --list-runtimes | Where-Object { $_ -match 'Microsoft.(AspNetCore|NETCore).App' }
```

You should see `Microsoft.AspNetCore.App 8.0.x` (and 10.0.x if using PS 7.6 preview).

## Quick start: run the samples

From the repository root:

```powershell
# 1) Hello World
pwsh .\examples\PowerShell\Tutorial\1-Hello-World.ps1
```

Then browse the routes (default listener: <http://127.0.0.1:5000>):
Read the note on each sample for the routes detail.

Stop the server with Ctrl+C in the terminal.

## Samples & Chapters Overview

| Order | Topic | Chapter | Sample Script | Focus |
| ----- | ----- | ------- | ------------- | ----- |
| 1 | Introduction | [Hello World][ch-1-1-hello-world] | [Script][sc-1-1-hello-world] | This sample spins up a small Kestrun server and returns plain text from a single route. |
| 2 | Routes | [Multiple Content Types][ch-2-1-multiple-content-types] | [Script][sc-2-1-multiple-content-types] | Return text, JSON, XML, and YAML from different routes. |
| 3 | Routes | [Multi-language Routes (PS/C#/VB)][ch-2-2-multi-language-routes] | [Script][sc-2-2-multi-language-routes] | Map routes in PowerShell and implement others inline in C# or VB.NET. |
| 4 | Routes | [Route Parameters & Request Data][ch-2-3-route-parameters] | [Script][sc-2-3-route-parameters] | Read data from different parts of the incoming HTTP request: path (route) parameters, query string, body (form / JSON), headers, and cookies. |
| 5 | Routes | [Route Options (MapRouteOptions)][ch-2-4-route-options] | [Script][sc-2-4-route-options] | Use `MapRouteOptions` / `New-KrMapRouteOption` to configure routes with richer metadata, different languages, and advanced behaviors. |
| 6 | Routes | [Route Groups][ch-2-5-route-groups] | [Script][sc-2-5-route-groups] | Group related routes under a shared URL prefix to remove repetition, keep concerns |
| 7 | Static Routes | [Serving Static Files][ch-3-1-static-routes] | [Script][sc-3-1-static-routes] | Expose folders of files (HTML, CSS, JS, images, etc.) over HTTP using the static files service. |
| 8 | Static Routes | [File Server & Directory Browsing][ch-3-2-file-server] | [Script][sc-3-2-file-server] | Serve a directory tree with optional HTML listings and custom MIME mappings. |
| 9 | Static Routes | [Static Route Overrides][ch-3-3-static-override-routes] | [Script][sc-3-3-static-override-routes] | Inject dynamic responses at specific file paths under a static directory. |
| 10 | Static Routes | [Adding a Favicon][ch-3-4-favicon] | [Script][sc-3-4-favicon] | Add a site icon (`favicon.ico` / PNG) so browsers display your brand in tabs and bookmarks. |
| 11 | Static Routes | [File Server Caching Headers][ch-3-5-file-server-caching] | [Script][sc-3-5-file-server-caching] | Add HTTP cache directives (Cache-Control) directly to static file responses served by the file server middleware. |
| 12 | Static Routes | [Response Caching & Conditional Requests][ch-3-6-response-caching] | [Script][sc-3-6-response-caching] | Layer fine‑grained dynamic caching and validator logic on top of (or independent from) file server level |
| 13 | Shared Variable | [Using Shared Variables][ch-4-1-shared-variables] | [Script][sc-4-1-shared-variables] | Share in-memory state (counters, caches, configuration) by defining variables **before** `Enable-KrConfiguration` |
| 14 | Shared Variable | [Managing Shared State][ch-4-2-shared-state] | [Script][sc-4-2-shared-state] | Use the Kestrun shared state cmdlets (`Set-KrSharedState`, `Get-KrSharedState`, `Remove-KrSharedState`) to create |
| 15 | Logging | [Simple Logging][ch-5-1-simple-logging] | [Script][sc-5-1-simple-logging] | Introduce structured logging early: create a named logger with console + rolling file sinks, register it as |
| 16 | Logging | [Multiple Loggers & Levels][ch-5-2-multiple-loggers-levels] | [Script][sc-5-2-multiple-loggers-levels] | Run more than one named logger at once and control their verbosity separately. |
| 17 | Logging | [Enrichment & Correlation IDs][ch-5-3-enrichment-correlation-ids] | [Script][sc-5-3-enrichment-correlation-ids] | Attach useful properties (enrichment) to your log events and add a per-request |
| 18 | Logging | [Sinks (Console/File/JSON)][ch-5-4-sinks] | [Script][sc-5-4-sinks] | Wire different outputs for your logs. |
| 19 | Logging | [Sinks (HTTP/EventLog/Syslog)][ch-5-5-sinks-advanced] | [Script][sc-5-5-sinks-advanced] | Push logs to external systems. |
| 20 | Logging | [Hot Reload (Update Logger)][ch-5-6-hot-reload] | [Script][sc-5-6-hot-reload] | Change logging behavior without restarting the server. |
| 21 | Logging | [Apache Common Access Log][ch-5-7-apache-common-access-log] | [Script][sc-5-7-apache-common-access-log] | Add structured request logging in the classic **Apache Common Log Format (CLF)** (also called *Common Access Log*). |
| 22 | Certificates | [Create a Self‑Signed Certificate][ch-6-1-self-signed] | [Script][sc-6-1-self-signed] | Generate a development certificate for localhost and bind it to an HTTPS listener. |
| 23 | Certificates | [Generate a CSR (Certificate Signing Request)][ch-6-2-csr] | [Script][sc-6-2-csr] | Run a small API that generates a CSR from request parameters. |
| 24 | Certificates | [Import, Export, Validate & EKU][ch-6-3-import-export-validate] | [Script][sc-6-3-import-export-validate] | Expose API endpoints to import a certificate and export it in different formats, with simple validation and EKU reporting. |
| 25 | Endpoints | [Basic Server][ch-7-1-basic-server] | [Script][sc-7-1-basic-server] | Minimal server: one HTTP listener (loopback:5000) + a single PowerShell route returning text. |
| 26 | Endpoints | [Multiple Listeners][ch-7-2-multiple-listeners] | [Script][sc-7-2-multiple-listeners] | Serve the same routes on more than one port / interface. |
| 27 | Endpoints | [HTTPS & Certificates][ch-7-3-https] | [Script][sc-7-3-https] | Add TLS with a development self‑signed certificate or an existing PFX. |
| 28 | Endpoints | [Named Pipes (Windows)][ch-7-4-named-pipes] | [Script][sc-7-4-named-pipes] | Bind Kestrun to a Windows named pipe for local IPC instead of a TCP port. |
| 29 | Endpoints | [Unix Sockets][ch-7-5-unix-sockets] | [Script][sc-7-5-unix-sockets] | Bind to a Unix domain socket instead of TCP for local reverse proxy performance. |
| 30 | Endpoints | [Mixed HTTP Protocols][ch-7-6-mixed-httpprotocols] | [Script][sc-7-6-mixed-httpprotocols] | Expose the same application over multiple listeners each constrained to a specific HTTP protocol version |
| 31 | Endpoints | [URI Endpoint][ch-7-7-uri-endpoint] | [Script][sc-7-7-uri-endpoint] | Demonstrates using a pre-built `System.Uri` with `Add-KrEndpoint -Uri` instead of separately specifying |
| 32 | Authentication | [Basic (PowerShell)][ch-8-1-basic-ps] | [Script][sc-8-1-basic-ps] | Define a Basic auth scheme with a PowerShell script block validating username/password. |
| 33 | Authentication | [Basic (C# / VB.NET)][ch-8-2-basic-multilang] | [Script][sc-8-2-basic-multilang] | Implement credential validation in C# and VB.NET while retaining PowerShell host scripts. |
| 34 | Authentication | [API Key][ch-8-3-api-key] | [Script][sc-8-3-api-key] | Validate requests using an API key header (fixed value, script, or inline C#/VB code). |
| 35 | Authentication | [JWT Tokens][ch-8-4-jwt] | [Script][sc-8-4-jwt] | Issue and validate bearer tokens for stateless auth. |
| 36 | Authentication | [Cookies][ch-8-5-cookies] | [Script][sc-8-5-cookies] | Maintain user sessions with a login form + cookie session authentication. |
| 37 | Authentication | [Windows Authentication][ch-8-6-windows-authentication] | [Script][sc-8-6-windows-authentication] | Leverage the host (Windows / Active Directory) credentials for transparent authentication. |
| 38 | Authentication | [Claims & Policies][ch-8-7-claims-policies] | [Script][sc-8-7-claims-policies] | Define claim-based authorization rules and enforce them per route. |
| 39 | Authentication | [Client Certificate][ch-8-8-client-certificate] | [Script][sc-8-8-client-certificate] | Authenticate requests using client TLS certificates (mTLS). |
| 40 | Authentication | [OpenID Connect (Okta)][ch-8-9-openid-connect-okta] | [Script][sc-8-9-openid-connect-okta] | Add Okta login using the authorization code flow with PKCE and persist the session via cookies. |
| 41 | Authentication | [Multiple Schemes][ch-8-10-multiple-schemes] | [Script][sc-8-10-multiple-schemes] | Combine several authentication mechanisms (Basic, API Key, JWT Bearer) in a single server and allow |
| 42 | Authentication | [GitHub Authentication][ch-8-11-github-authentication] | [Script][sc-8-11-github-authentication] | Enable GitHub OAuth login with a ready-made wrapper that configures PKCE, token persistence, and session cookie forwarding. |
| 43 | Authentication | [Full Demo][ch-8-12-full-demo] | [Script][sc-8-12-full-demo] | Integrated example combining Basic, API Key, JWT, Cookies, claims & policies. |
| 44 | Authentication | [OpenID Connect (Duende Demo)][ch-8-12-openid-connect] | [Script][sc-8-12-openid-connect] | Authenticate users via OpenID Connect using the public demo server at |
| 45 | Authentication | [Full Demo][ch-8-13-full-demo] | [Script][sc-8-13-full-demo] | Integrated example combining Basic, API Key, JWT, Cookies, claims & policies. |
| 46 | Responses | [Text & JSON][ch-9-1-basic-text-json] | [Script][sc-9-1-basic-text-json] | Use `Write-KrTextResponse` for plain UTF-8 text and `Write-KrJsonResponse` for structured object data. |
| 47 | Responses | [Structured (XML / YAML / CSV)][ch-9-2-structured-xml-yaml-csv] | [Script][sc-9-2-structured-xml-yaml-csv] | Return alternative serializations for interoperability, configuration and exports. |
| 48 | Responses | [Binary & Stream][ch-9-3-binary-stream] | [Script][sc-9-3-binary-stream] | Use these when returning non-text payloads, large files, or custom generated data. |
| 49 | Responses | [HTML Templates & Files][ch-9-4-html-templates-files] | [Script][sc-9-4-html-templates-files] | Render dynamic HTML or return files with proper headers. |
| 50 | Responses | [Special Formats (BSON / CBOR)][ch-9-5-special-bson-cbor] | [Script][sc-9-5-special-bson-cbor] | Use `Write-KrBsonResponse` and `Write-KrCborResponse` for compact binary object encodings, ideal for IoT, |
| 51 | Responses | [Redirects][ch-9-6-redirects] | [Script][sc-9-6-redirects] | Send 3xx status and a Location header using `Write-KrRedirectResponse`. |
| 52 | Responses | [Errors][ch-9-7-errors] | [Script][sc-9-7-errors] | Use `Write-KrErrorResponse` for consistent failure payloads. |
| 53 | Responses | [Caching & Revalidation][ch-9-8-caching] | [Script][sc-9-8-caching] | Combine `Add-KrCacheResponse` (Cache-Control directives) with `Test-KrCacheRevalidation` (ETag / Last-Modified) for efficient conditional requests. |
| 54 | Responses | [Content Negotiation][ch-9-9-content-negotiation] | [Script][sc-9-9-content-negotiation] | `Write-KrResponse` provides content negotiation and automatic format selection based on the client's `Accept` header. |
| 55 | Responses | [Low-Level Response Stream][ch-9-10-low-level-response] | [Script][sc-9-10-low-level-response] | Direct manipulation of `$Context.Response.Body` stream for scenarios requiring manual control over response writing. |
| 56 | OpenApi | [Hello World][ch-10-1-hello-world] | [Script][sc-10-1-hello-world] | Create a simple OpenAPI 3.1 specification with a single greeting endpoint. |
| 57 | OpenApi | [Component Schemas][ch-10-2-component-schemas] | [Script][sc-10-2-component-schemas] | Define reusable request and response schemas using PowerShell classes decorated with OpenAPI attributes. |
| 58 | OpenApi | [RequestBody Components][ch-10-3-requestbody-components] | [Script][sc-10-3-requestbody-components] | Create reusable request body components that can be shared across multiple endpoints. |
| 59 | OpenApi | [Parameter Components][ch-10-4-parameter-components] | [Script][sc-10-4-parameter-components] | Create reusable parameter components for query strings, path variables, headers, and cookies. |
| 60 | OpenApi | [Response Components][ch-10-5-response-components] | [Script][sc-10-5-response-components] | Create reusable response components for consistent response structures across multiple endpoints. |
| 61 | OpenApi | [Complete Components][ch-10-6-complete-components] | [Script][sc-10-6-complete-components] | Combine request body and response components in a complete API with validation and error handling. |
| 62 | OpenApi | [Tags and External Docs][ch-10-7-tags-and-docs] | [Script][sc-10-7-tags-and-docs] | Group operations with tags and attach external documentation links to both the document and specific routes. |
| 63 | OpenApi | [Document Information][ch-10-8-document-info] | [Script][sc-10-8-document-info] | Populate OpenAPI document metadata including title, version, summary, terms of service, contact, license, and server list. |
| 64 | OpenApi | [Component Headers][ch-10-9-component-headers] | [Script][sc-10-9-component-headers] | Demonstrates how to define reusable header components for OpenAPI responses. |
| 65 | OpenApi | [Component Links][ch-10-10-component-links] | [Script][sc-10-10-component-links] | Demonstrates how to define reusable link components to describe relationships between operations. |
| 66 | OpenApi | [Component Callbacks][ch-10-11-component-callbacks] | [Script][sc-10-11-component-callbacks] | Demonstrates how to define reusable callback components for asynchronous operations. |
| 67 | OpenApi | [WebHooks][ch-10-12-webhooks] | [Script][sc-10-12-webhooks] | Demonstrates how to define top-level WebHooks in the OpenAPI document. |
| 68 | OpenApi | [Examples][ch-10-13-examples] | [Script][sc-10-13-examples] | Demonstrates OpenAPI example components plus inline examples applied to requests, responses, and parameters. |
| 69 | OpenApi | [Swagger Petstore][ch-10-14-petstore-api] | [Script][sc-10-14-petstore-api] | Implementation of the classic [Swagger Petstore](https://github.com/swagger-api/swagger-petstore) example (OpenAPI 3.1) using Kestrun. |
| 70 | OpenApi | [Redocly Museum API][ch-10-15-museum-api] | [Script][sc-10-15-museum-api] | A comprehensive example implementing the [Redocly Museum API](https://redocly.com/docs/cli/) specification using Kestrun. |
| 71 | OpenApi | [Multiple OpenAPI Documents][ch-10-18-multi-document-openapi] | [Script][sc-10-18-multi-document-openapi] | Shows how to serve multiple OpenAPI documents (a default document plus a separate webhook document) from the same Kestrun server. |
| 72 | OpenApi | [Product Search with HTTP QUERY (OpenAPI 3.2)][ch-10-19-product-search-query] | [Script][sc-10-19-product-search-query] | Demonstrates the OpenAPI 3.2 HTTP `QUERY` method with structured request body filters, pagination parameters, and response content negotiation. |
| 73 | OpenApi | [SSE (OpenAPI)][ch-10-20-sse] | [Script][sc-10-20-sse] | Document a Server-Sent Events (SSE) endpoint (`text/event-stream`) using OpenAPI. |
| 74 | OpenApi | [SSE Broadcast (OpenAPI)][ch-10-21-sse-broadcast] | [Script][sc-10-21-sse-broadcast] | Document a broadcast SSE endpoint (`text/event-stream`) and a JSON broadcast trigger API using OpenAPI. |
| 75 | OpenApi | [SignalR (OpenAPI)][ch-10-22-signalr] | [Script][sc-10-22-signalr] | Document SignalR-adjacent HTTP routes (that trigger hub broadcasts) using OpenAPI. |
| 76 | OpenApi | [XML Modeling][ch-10-23-xml-modeling] | [Script][sc-10-23-xml-modeling] | Demonstrate OpenAPI 3.2 XML modeling with attributes, namespaces, and wrapped arrays using the `OpenApiXml` attribute. |
| 77 | OpenApi | [RFC 6570 Variable Mapping][ch-10-24-rfc6570-variable-mapping] | [Script][sc-10-24-rfc6570-variable-mapping] | Document OpenAPI 3.2 RFC 6570 path expressions and map ASP.NET Core route values into RFC6570 variables for multi-segment paths. |
| 78 | OpenApi | [Additional and Pattern Properties][ch-10-25-additional-pattern-properties] | [Script][sc-10-25-additional-pattern-properties] | Model dynamic key/value objects using `additionalProperties` and `patternProperties`. |
| 79 | Razor | [Razor Pages Quickstart][ch-11-1-razorpages-quickstart] | [Script][sc-11-1-razorpages-quickstart] | Serve Razor Pages where each `.cshtml` can have an optional sibling `.cshtml.ps1` script that builds a per-request model. |
| 80 | Razor | [Razor Pages with Antiforgery][ch-11-2-razorpages-antiforgery] | [Script][sc-11-2-razorpages-antiforgery] | Protect unsafe endpoints using cookie + header antiforgery tokens, and expose a token endpoint. |
| 81 | Scheduler | [Scheduling Quickstart][ch-12-1-scheduling-quickstart] | [Script][sc-12-1-scheduling-quickstart] | Enable the scheduler, register a couple of jobs, and expose diagnostic routes. |
| 82 | Scheduler | [Scheduling with CRON][ch-12-2-scheduling-cron] | [Script][sc-12-2-scheduling-cron] | Run jobs using 6-field CRON expressions with seconds precision. |
| 83 | Scheduler | [Scheduling Report][ch-12-3-scheduling-report] | [Script][sc-12-3-scheduling-report] | Expose an endpoint that returns the aggregated schedule report, optionally in a specific time zone. |
| 84 | Server Configuration | [Server Limits][ch-13-1-server-limits] | [Script][sc-13-1-server-limits] | Fine‑tune resource & safety thresholds using `Set-KrServerLimit`. |
| 85 | Server Configuration | [Server Options][ch-13-2-server-options] | [Script][sc-13-2-server-options] | Configure runtime behaviors (headers, compression, runspaces) with `Set-KrServerOptions`. |
| 86 | Lifecycle | [Start/Stop Patterns][ch-14-1-start-stop] | [Script][sc-14-1-start-stop] | Control server lifecycle: blocking vs non‑blocking, programmatic shutdown. |
| 87 | Lifecycle | [Full Server Demo][ch-14-2-full-demo] | [Script][sc-14-2-full-demo] | Run an integrated demo server with multiple endpoints, basic routes, and clean start/stop. |
| 88 | Middleware | [Antiforgery Protection][ch-15-1-antiforgery] | [Script][sc-15-1-antiforgery] | Protect state‑changing endpoints (POST/PUT/PATCH/DELETE) from Cross-Site Request Forgery by validating a token that is *bound* to a user session cookie. |
| 89 | Middleware | [Response Compression][ch-15-2-compression] | [Script][sc-15-2-compression] | Reduce payload size (bandwidth + latency) for text-based responses by enabling |
| 90 | Middleware | [HTTPS Redirection][ch-15-3-https-redirection] | [Script][sc-15-3-https-redirection] | Force HTTP traffic to upgrade to HTTPS with a configurable redirect status code and optional explicit HTTPS port. |
| 91 | Middleware | [HTTPS Strict Transport Security (HSTS)][ch-15-4-https-hsts] | [Script][sc-15-4-https-hsts] | HTTP Strict Transport Security (HSTS) is a web security policy mechanism that helps protect websites against |
| 92 | Middleware | [SignalR][ch-15-5-signalr] | [Script][sc-15-5-signalr] | Add real-time, bidirectional communication to your server for live events, progress, and notifications using SignalR. |
| 93 | Middleware | [Host Filtering][ch-15-6-host-filtering] | [Script][sc-15-6-host-filtering] | Restrict which Host headers are allowed to reach your app. |
| 94 | Middleware | [Forwarded Headers][ch-15-7-forwarded-headers] | [Script][sc-15-7-forwarded-headers] | Honor X-Forwarded-* headers from a reverse proxy to reflect the original client IP, scheme, and host. |
| 95 | Middleware | [CORS][ch-15-8-cors] | [Script][sc-15-8-cors] | Configure Cross-Origin Resource Sharing (CORS) policies to control browser access from different origins. |
| 96 | Middleware | [SSE][ch-15-9-sse] | [Script][sc-15-9-sse] | Stream real-time server events to browsers using Server-Sent Events (SSE). |
| 97 | Middleware | [SSE Broadcast][ch-15-10-ssebroadcast] | [Script][sc-15-10-ssebroadcast] | Keep an SSE connection open and broadcast events to all connected clients. |
| 98 | Middleware | [Request Decompression][ch-15-11-request-decompression] | [Script][sc-15-11-request-decompression] | Accept request bodies compressed with `Content-Encoding` (for example `gzip`) by enabling the Request Decompression middleware. |
| 99 | Health | [Health Quickstart][ch-16-1-health-quickstart] | [Script][sc-16-1-health-quickstart] | Expose `/healthz` and aggregate basic probes (script + HTTP) into a single JSON document for liveness / readiness checks. |
| 100 | Health | [Script Probe][ch-16-2-health-script-probe] | [Script][sc-16-2-health-script-probe] | Demonstrates a standalone PowerShell script probe measuring latency and producing custom metrics. |
| 101 | Health | [HTTP Probe][ch-16-3-health-http-probe] | [Script][sc-16-3-health-http-probe] | Shows using the built‑in HTTP probe to call an internal route and fold its success/failure into the health aggregate. |
| 102 | Health | [Process Probe][ch-16-4-health-process-probe] | [Script][sc-16-4-health-process-probe] | Executes an external process (`dotnet --info`) and reports its success & duration. |
| 103 | Health | [C# Inline Probe][ch-16-5-health-csharp-probe] | [Script][sc-16-5-health-csharp-probe] | Illustrates using the C# inline capability to craft a probe in code when PowerShell alone is insufficient or you want direct .NET API access. |
| 104 | Health | [Disk Probe][ch-16-6-health-disk-probe] | [Script][sc-16-6-health-disk-probe] | Demonstrates the built‑in disk space probe (auto‑registered) and how to override it with custom thresholds. |
| 105 | Health | [Health Response Format][ch-16-7-health-response-format] | [Script][sc-16-7-health-response-format] | Demonstrates configuring health endpoints with Auto response format for content negotiation |
| 106 | Status Code Pages | [Default Status Code Pages][ch-17-1-default-status-code-pages] | [Script][sc-17-1-default-status-code-pages] | Enable built-in error pages so 404, 500, and similar responses return consistent content. |
| 107 | Status Code Pages | [Custom Options][ch-17-2-custom-options] | [Script][sc-17-2-custom-options] | Configure StatusCodePages behavior via options such as content type, formats, redirects, or re-execution. |
| 108 | Status Code Pages | [Custom Handler (PowerShell)][ch-17-3-custom-handler-powershell] | [Script][sc-17-3-custom-handler-powershell] | Handle status codes with a PowerShell ScriptBlock that logs and returns a JSON error payload. |
| 109 | Status Code Pages | [Custom Handler (C# Inline)][ch-17-4-custom-handler-csharp-inline] | [Script][sc-17-4-custom-handler-csharp-inline] | Handle status codes with an inline C# snippet that composes a JSON payload and writes the response. |
| 110 | Status Code Pages | [Content Format][ch-17-5-content-format] | [Script][sc-17-5-content-format] | Customize the error content type and HTML body with external templates and variables. |
| 111 | Status Code Pages | [Redirects][ch-17-6-redirects] | [Script][sc-17-6-redirects] | Redirect error responses to friendly routes and render details via templates. |
| 112 | Status Code Pages | [Re-execute][ch-17-7-re-execute] | [Script][sc-17-7-re-execute] | Re-execute the pipeline using alternate routes to generate friendly error pages while keeping the original URL. |
| 113 | Exception Handling | [Exception Handling Path][ch-18-1-exceptionhandlingpath] | [Script][sc-18-1-exceptionhandlingpath] | Re-execute the pipeline to a fixed error path when an unhandled exception occurs. |
| 114 | Exception Handling | [VB.NET Handler][ch-18-2-vbnet-handler] | [Script][sc-18-2-vbnet-handler] | Handle exceptions using a VB.NET middleware handler for consistent JSON across routes. |
| 115 | Exception Handling | [C# Inline Handler][ch-18-3-csharp-inline] | [Script][sc-18-3-csharp-inline] | Handle exceptions with a C# inline handler. |
| 116 | Exception Handling | [ProblemDetails Fallback][ch-18-4-problemdetails] | [Script][sc-18-4-problemdetails] | Return RFC 7807 ProblemDetails for unhandled exceptions, ideal for API clients. |
| 117 | Exception Handling | [Developer Exception Page][ch-18-5-developer-exception-page] | [Script][sc-18-5-developer-exception-page] | Show rich error details during development. |
| 118 | Sessions | [Basic Sessions][ch-19-1-sessions-basic] | [Script][sc-19-1-sessions-basic] | Use cookie-based session state to persist small key-value data across requests (counters, user info, preferences). |
| 119 | Sessions | [Sessions with Redis][ch-19-2-sessions-redis] | [Script][sc-19-2-sessions-redis] | Use StackExchange.Redis as the distributed cache backing for ASP.NET Core session state. |
| 120 | Sessions | [Sessions with SQL Server][ch-19-3-sessions-sql] | [Script][sc-19-3-sessions-sql] | Use SQL Server as the distributed cache backing for ASP.NET Core session state. |
| 121 | Tasks | [Basic Tasks][ch-20-1-tasks] | [Script][sc-20-1-tasks] | Run ad-hoc scripts as background tasks with status, progress, results, cancellation, and controlled removal. |
| 122 | Localization | [Localization][ch-21-1-localization] | [Script][sc-21-1-localization] | Serve localized responses with PowerShell-style string tables resolved per request. |
| 123 | Localization | [Razor Localization][ch-21-2-razor-localization] | [Script][sc-21-2-razor-localization] | Render localized Razor pages with per-request culture resolution. |
| 124 | File and Form Uploads | [Basic multipart/form-data upload][ch-22-1-basic-multipart] | [Script][sc-22-1-basic-multipart] | Parse a simple multipart upload with one text field and one file. |
| 125 | File and Form Uploads | [Multiple files (same field name)][ch-22-2-multiple-files] | [Script][sc-22-2-multiple-files] | Accept multiple files posted under a single field name. |
| 126 | File and Form Uploads | [application/x-www-form-urlencoded forms][ch-22-3-urlencoded] | [Script][sc-22-3-urlencoded] | Parse classic HTML form posts with urlencoded bodies. |
| 127 | File and Form Uploads | [multipart/mixed ordered parts][ch-22-4-multipart-mixed] | [Script][sc-22-4-multipart-mixed] | Parse ordered multipart payloads where part order matters. |
| 128 | File and Form Uploads | [Nested multipart/mixed][ch-22-5-nested-multipart] | [Script][sc-22-5-nested-multipart] | Handle a single nested multipart/mixed payload inside an ordered multipart body. |
| 129 | File and Form Uploads | [Request-level compression][ch-22-6-request-compressed] | [Script][sc-22-6-request-compressed] | Enable ASP.NET Core RequestDecompression middleware for gzip request bodies. |
| 130 | File and Form Uploads | [Part-level compression][ch-22-7-part-compressed] | [Script][sc-22-7-part-compressed] | Decompress individual multipart parts using per-part Content-Encoding. |
| 131 | File and Form Uploads | [OpenAPI: basic multipart upload][ch-22-8-basic-multipart-openapi] | [Script][sc-22-8-basic-multipart-openapi] | Document a basic multipart/form-data upload with OpenAPI annotations. |
| 132 | File and Form Uploads | [OpenAPI: multiple files][ch-22-9-multiple-files-openapi] | [Script][sc-22-9-multiple-files-openapi] | Describe multiple files under the same field name with OpenAPI. |
| 133 | File and Form Uploads | [OpenAPI: urlencoded forms][ch-22-10-url-encoded-openapi] | [Script][sc-22-10-url-encoded-openapi] | Document application/x-www-form-urlencoded payloads with OpenAPI. |
| 134 | File and Form Uploads | [OpenAPI: multipart/mixed][ch-22-11-multipart-mixed-openapi] | [Script][sc-22-11-multipart-mixed-openapi] | Document ordered multipart/mixed payloads with OpenAPI. |
| 135 | File and Form Uploads | [OpenAPI: nested multipart][ch-22-12-nested-multipart-openapi] | [Script][sc-22-12-nested-multipart-openapi] | Document nested multipart/mixed payloads with OpenAPI. |
| 136 | File and Form Uploads | [OpenAPI: request-level compression][ch-22-13-request-compressed-openapi] | [Script][sc-22-13-request-compressed-openapi] | Document request-compressed multipart uploads with OpenAPI. |
| 137 | File and Form Uploads | [OpenAPI: part-level compression][ch-22-14-part-compressed-openapi] | [Script][sc-22-14-part-compressed-openapi] | Document per-part Content-Encoding with OpenAPI annotations. |
| 138 | File and Form Uploads | [OpenAPI: file hash upload][ch-22-15-file-hash-openapi] | [Script][sc-22-15-file-hash-openapi] | Upload a binary file and return MD5/SHA1/SHA256/SHA384/SHA512 hashes with OpenAPI annotations. |
| 139 | File and Form Uploads | [Form rules: file hash upload][ch-22-16-file-hash-rule] | [Script][sc-22-16-file-hash-rule] | Upload a binary file using `New-KrFormPartRule` and return MD5/SHA1/SHA256/SHA384/SHA512 hashes. |

Static chapters and scripts are all linked directly above for quick navigation.

[ch-1-1-hello-world]: ./1.introduction/1.Hello-World
[ch-2-1-multiple-content-types]: ./2.routes/1.Multiple-Content-Types
[ch-2-2-multi-language-routes]: ./2.routes/2.Multi-Language-Routes
[ch-2-3-route-parameters]: ./2.routes/3.Route-Parameters
[ch-2-4-route-options]: ./2.routes/4.Route-Options
[ch-2-5-route-groups]: ./2.routes/5.Route-Groups
[ch-3-1-static-routes]: ./3.static/1.Static-Routes
[ch-3-2-file-server]: ./3.static/2.File-Server
[ch-3-3-static-override-routes]: ./3.static/3.Static-Override-Routes
[ch-3-4-favicon]: ./3.static/4.Favicon
[ch-3-5-file-server-caching]: ./3.static/5.File-Server-Caching
[ch-3-6-response-caching]: ./3.static/6.Response-Caching
[ch-4-1-shared-variables]: ./4.variable/1.Shared-Variables
[ch-4-2-shared-state]: ./4.variable/2.Shared-State
[ch-5-1-simple-logging]: ./5.logging/1.Simple-Logging
[ch-5-2-multiple-loggers-levels]: ./5.logging/2.Multiple-Loggers-Levels
[ch-5-3-enrichment-correlation-ids]: ./5.logging/3.Enrichment-Correlation-IDs
[ch-5-4-sinks]: ./5.logging/4.Sinks
[ch-5-5-sinks-advanced]: ./5.logging/5.Sinks-Advanced
[ch-5-6-hot-reload]: ./5.logging/6.Hot-Reload
[ch-5-7-apache-common-access-log]: ./5.logging/7.Apache-Common-Access-Log
[ch-6-1-self-signed]: ./6.certificates/1.Self-Signed
[ch-6-2-csr]: ./6.certificates/2.CSR
[ch-6-3-import-export-validate]: ./6.certificates/3.Import-Export-Validate
[ch-7-1-basic-server]: ./7.endpoints/1.Basic-Server
[ch-7-2-multiple-listeners]: ./7.endpoints/2.Multiple-Listeners
[ch-7-3-https]: ./7.endpoints/3.Https
[ch-7-4-named-pipes]: ./7.endpoints/4.Named-Pipes
[ch-7-5-unix-sockets]: ./7.endpoints/5.Unix-Sockets
[ch-7-6-mixed-httpprotocols]: ./7.endpoints/6.Mixed-HttpProtocols
[ch-7-7-uri-endpoint]: ./7.endpoints/7.Uri-Endpoint
[ch-8-1-basic-ps]: ./8.authentication/1.Basic-PS
[ch-8-2-basic-multilang]: ./8.authentication/2.Basic-MultiLang
[ch-8-3-api-key]: ./8.authentication/3.Api-Key
[ch-8-4-jwt]: ./8.authentication/4.Jwt
[ch-8-5-cookies]: ./8.authentication/5.Cookies
[ch-8-6-windows-authentication]: ./8.authentication/6.Windows-Authentication
[ch-8-7-claims-policies]: ./8.authentication/7.Claims-Policies
[ch-8-8-client-certificate]: ./8.authentication/8.Client-Certificate
[ch-8-9-openid-connect-okta]: ./8.authentication/9.OpenID-Connect-Okta
[ch-8-10-multiple-schemes]: ./8.authentication/10.Multiple-Schemes
[ch-8-11-github-authentication]: ./8.authentication/11.GitHub-Authentication
[ch-8-12-full-demo]: ./8.authentication/12.Full-Demo
[ch-8-12-openid-connect]: ./8.authentication/12.OpenID-Connect
[ch-8-13-full-demo]: ./8.authentication/13.Full-Demo
[ch-9-1-basic-text-json]: ./9.Responses/1.Basic-Text-Json
[ch-9-2-structured-xml-yaml-csv]: ./9.Responses/2.Structured-Xml-Yaml-Csv
[ch-9-3-binary-stream]: ./9.Responses/3.Binary-Stream
[ch-9-4-html-templates-files]: ./9.Responses/4.Html-Templates-Files
[ch-9-5-special-bson-cbor]: ./9.Responses/5.Special-Bson-Cbor
[ch-9-6-redirects]: ./9.Responses/6.Redirects
[ch-9-7-errors]: ./9.Responses/7.Errors
[ch-9-8-caching]: ./9.Responses/8.Caching
[ch-9-9-content-negotiation]: ./9.Responses/9.Content-Negotiation
[ch-9-10-low-level-response]: ./9.Responses/10.Low-Level-Response
[ch-10-1-hello-world]: ./10.openapi/1.Hello-World
[ch-10-2-component-schemas]: ./10.openapi/2.Component-Schemas
[ch-10-3-requestbody-components]: ./10.openapi/3.RequestBody-Components
[ch-10-4-parameter-components]: ./10.openapi/4.Parameter-Components
[ch-10-5-response-components]: ./10.openapi/5.Response-Components
[ch-10-6-complete-components]: ./10.openapi/6.Complete-Components
[ch-10-7-tags-and-docs]: ./10.openapi/7.Tags-and-Docs
[ch-10-8-document-info]: ./10.openapi/8.Document-Info
[ch-10-9-component-headers]: ./10.openapi/9.Component-Headers
[ch-10-10-component-links]: ./10.openapi/10.Component-Links
[ch-10-11-component-callbacks]: ./10.openapi/11.Component-Callbacks
[ch-10-12-webhooks]: ./10.openapi/12.WebHooks
[ch-10-13-examples]: ./10.openapi/13.Examples
[ch-10-14-petstore-api]: ./10.openapi/14.Petstore-API
[ch-10-15-museum-api]: ./10.openapi/15.Museum-API
[ch-10-18-multi-document-openapi]: ./10.openapi/18.Multi-Document-OpenAPI
[ch-10-19-product-search-query]: ./10.openapi/19.Product-Search-Query
[ch-10-20-sse]: ./10.openapi/20.Sse
[ch-10-21-sse-broadcast]: ./10.openapi/21.Sse-Broadcast
[ch-10-22-signalr]: ./10.openapi/22.SignalR
[ch-10-23-xml-modeling]: ./10.openapi/23.XML-Modeling
[ch-10-24-rfc6570-variable-mapping]: ./10.openapi/24.RFC6570-Variable-Mapping
[ch-10-25-additional-pattern-properties]: ./10.openapi/25.Additional-Pattern-Properties
[ch-11-1-razorpages-quickstart]: ./11.razor/1.RazorPages-Quickstart
[ch-11-2-razorpages-antiforgery]: ./11.razor/2.RazorPages-Antiforgery
[ch-12-1-scheduling-quickstart]: ./12.scheduling/1.Scheduling-Quickstart
[ch-12-2-scheduling-cron]: ./12.scheduling/2.Scheduling-Cron
[ch-12-3-scheduling-report]: ./12.scheduling/3.Scheduling-Report
[ch-13-1-server-limits]: ./13.server-configuration/1.Server-Limits
[ch-13-2-server-options]: ./13.server-configuration/2.Server-Options
[ch-14-1-start-stop]: ./14.lifecycle/1.Start-Stop
[ch-14-2-full-demo]: ./14.lifecycle/2.Full-Demo
[ch-15-1-antiforgery]: ./15.middleware/1.Antiforgery
[ch-15-2-compression]: ./15.middleware/2.Compression
[ch-15-3-https-redirection]: ./15.middleware/3.Https-Redirection
[ch-15-4-https-hsts]: ./15.middleware/4.Https-Hsts
[ch-15-5-signalr]: ./15.middleware/5.SignalR
[ch-15-6-host-filtering]: ./15.middleware/6.Host-Filtering
[ch-15-7-forwarded-headers]: ./15.middleware/7.Forwarded-Headers
[ch-15-8-cors]: ./15.middleware/8.Cors
[ch-15-9-sse]: ./15.middleware/9.Sse
[ch-15-10-ssebroadcast]: ./15.middleware/10.SseBroadcast
[ch-15-11-request-decompression]: ./15.middleware/11.Request-Decompression
[ch-16-1-health-quickstart]: ./16.health/1.Health-Quickstart
[ch-16-2-health-script-probe]: ./16.health/2.Health-Script-Probe
[ch-16-3-health-http-probe]: ./16.health/3.Health-Http-Probe
[ch-16-4-health-process-probe]: ./16.health/4.Health-Process-Probe
[ch-16-5-health-csharp-probe]: ./16.health/5.Health-CSharp-Probe
[ch-16-6-health-disk-probe]: ./16.health/6.Health-Disk-Probe
[ch-16-7-health-response-format]: ./16.health/7.Health-Response-Format
[ch-17-1-default-status-code-pages]: ./17.StatusCodePages/1.Default-Status-Code-Pages
[ch-17-2-custom-options]: ./17.StatusCodePages/2.Custom-Options
[ch-17-3-custom-handler-powershell]: ./17.StatusCodePages/3.Custom-Handler-PowerShell
[ch-17-4-custom-handler-csharp-inline]: ./17.StatusCodePages/4.Custom-Handler-CSharp-Inline
[ch-17-5-content-format]: ./17.StatusCodePages/5.Content-Format
[ch-17-6-redirects]: ./17.StatusCodePages/6.Redirects
[ch-17-7-re-execute]: ./17.StatusCodePages/7.Re-execute
[ch-18-1-exceptionhandlingpath]: ./18.ExceptionHandling/1.ExceptionHandlingPath
[ch-18-2-vbnet-handler]: ./18.ExceptionHandling/2.VBNet-Handler
[ch-18-3-csharp-inline]: ./18.ExceptionHandling/3.CSharp-Inline
[ch-18-4-problemdetails]: ./18.ExceptionHandling/4.ProblemDetails
[ch-18-5-developer-exception-page]: ./18.ExceptionHandling/5.Developer-Exception-Page
[ch-19-1-sessions-basic]: ./19.Sessions/1.Sessions-Basic
[ch-19-2-sessions-redis]: ./19.Sessions/2.Sessions-Redis
[ch-19-3-sessions-sql]: ./19.Sessions/3.Sessions-Sql
[ch-20-1-tasks]: ./20.Tasks/1.Tasks
[ch-21-1-localization]: ./21.Localization/1.Localization
[ch-21-2-razor-localization]: ./21.Localization/2.Razor-Localization
[ch-22-1-basic-multipart]: ./22.file-and-form-uploads/22.1-Basic-Multipart
[ch-22-2-multiple-files]: ./22.file-and-form-uploads/22.2-Multiple-Files
[ch-22-3-urlencoded]: ./22.file-and-form-uploads/22.3-UrlEncoded
[ch-22-4-multipart-mixed]: ./22.file-and-form-uploads/22.4-Multipart-Mixed
[ch-22-5-nested-multipart]: ./22.file-and-form-uploads/22.5-Nested-Multipart
[ch-22-6-request-compressed]: ./22.file-and-form-uploads/22.6-Request-Compressed
[ch-22-7-part-compressed]: ./22.file-and-form-uploads/22.7-Part-Compressed
[ch-22-8-basic-multipart-openapi]: ./22.file-and-form-uploads/22.8-Basic-Multipart-OpenAPI
[ch-22-9-multiple-files-openapi]: ./22.file-and-form-uploads/22.9-Multiple-Files-OpenAPI
[ch-22-10-url-encoded-openapi]: ./22.file-and-form-uploads/22.10-Url-Encoded-OpenAPI
[ch-22-11-multipart-mixed-openapi]: ./22.file-and-form-uploads/22.11-Multipart-Mixed-OpenAPI
[ch-22-12-nested-multipart-openapi]: ./22.file-and-form-uploads/22.12-Nested-Multipart-OpenAPI
[ch-22-13-request-compressed-openapi]: ./22.file-and-form-uploads/22.13-Request-Compressed-OpenAPI
[ch-22-14-part-compressed-openapi]: ./22.file-and-form-uploads/22.14-Part-Compressed-OpenAPI
[ch-22-15-file-hash-openapi]: ./22.file-and-form-uploads/22.15-File-Hash-OpenAPI
[ch-22-16-file-hash-rule]: ./22.file-and-form-uploads/22.16-File-Hash-Rule
[sc-1-1-hello-world]: /pwsh/tutorial/examples/1.1-Hello-World.ps1
[sc-2-1-multiple-content-types]: /pwsh/tutorial/examples/2.1-Multiple-Content-Types.ps1
[sc-2-2-multi-language-routes]: /pwsh/tutorial/examples/2.2-Multi-Language-Routes.ps1
[sc-2-3-route-parameters]: /pwsh/tutorial/examples/2.3-Route-Parameters.ps1
[sc-2-4-route-options]: /pwsh/tutorial/examples/2.4-Route-Options.ps1
[sc-2-5-route-groups]: /pwsh/tutorial/examples/2.5-Route-Group.ps1
[sc-3-1-static-routes]: /pwsh/tutorial/examples/3.1-Static-Routes.ps1
[sc-3-2-file-server]: /pwsh/tutorial/examples/3.2-File-Server.ps1
[sc-3-3-static-override-routes]: /pwsh/tutorial/examples/3.3-Static-OverrideRoutes.ps1
[sc-3-4-favicon]: /pwsh/tutorial/examples/3.4-Add-FavIcon.ps1
[sc-3-5-file-server-caching]: /pwsh/tutorial/examples/3.5-File-ServerCaching.ps1
[sc-3-6-response-caching]: /pwsh/tutorial/examples/3.6-Response-Caching.ps1
[sc-4-1-shared-variables]: /pwsh/tutorial/examples/4.1-Shared-Variables.ps1
[sc-4-2-shared-state]: /pwsh/tutorial/examples/4.2-Shared-State.ps1
[sc-5-1-simple-logging]: /pwsh/tutorial/examples/5.1-Simple-Logging.ps1
[sc-5-2-multiple-loggers-levels]: /pwsh/tutorial/examples/5.2-Multiple-Loggers-Levels.ps1
[sc-5-3-enrichment-correlation-ids]: /pwsh/tutorial/examples/5.3-Enrichment-Correlation-IDs.ps1
[sc-5-4-sinks]: /pwsh/tutorial/examples/5.4-Sinks.ps1
[sc-5-5-sinks-advanced]: /pwsh/tutorial/examples/5.5-Sinks-Advanced.ps1
[sc-5-6-hot-reload]: /pwsh/tutorial/examples/5.6-Hot-Reload.ps1
[sc-5-7-apache-common-access-log]: /pwsh/tutorial/examples/5.7-ApacheLog.ps1
[sc-6-1-self-signed]: /pwsh/tutorial/examples/6.1-Cert-SelfSigned.ps1
[sc-6-2-csr]: /pwsh/tutorial/examples/6.2-Cert-CSR.ps1
[sc-6-3-import-export-validate]: /pwsh/tutorial/examples/6.3-Cert-Import-Export.ps1
[sc-7-1-basic-server]: /pwsh/tutorial/examples/7.1-Basic-Server.ps1
[sc-7-2-multiple-listeners]: /pwsh/tutorial/examples/7.2-Multiple-Listeners.ps1
[sc-7-3-https]: /pwsh/tutorial/examples/7.3-Https.ps1
[sc-7-4-named-pipes]: /pwsh/tutorial/examples/7.4-Named-Pipes.ps1
[sc-7-5-unix-sockets]: /pwsh/tutorial/examples/7.5-Unix-Sockets.ps1
[sc-7-6-mixed-httpprotocols]: /pwsh/tutorial/examples/7.6-Mixed-HttpProtocols.ps1
[sc-7-7-uri-endpoint]: /pwsh/tutorial/examples/7.7-Uri-Endpoint.ps1
[sc-8-1-basic-ps]: /pwsh/tutorial/examples/8.1-Basic-PS.ps1
[sc-8-2-basic-multilang]: /pwsh/tutorial/examples/8.2-Basic-MultiLang.ps1
[sc-8-3-api-key]: /pwsh/tutorial/examples/8.3-Api-Key.ps1
[sc-8-4-jwt]: /pwsh/tutorial/examples/8.4-Jwt.ps1
[sc-8-5-cookies]: /pwsh/tutorial/examples/8.5-Cookies.ps1
[sc-8-6-windows-authentication]: /pwsh/tutorial/examples/8.6-Windows-Authentication.ps1
[sc-8-7-claims-policies]: /pwsh/tutorial/examples/8.7-Claims-Policies.ps1
[sc-8-8-client-certificate]: /pwsh/tutorial/examples/8.8-Client-Certificate-Authentication.ps1
[sc-8-9-openid-connect-okta]: /pwsh/tutorial/examples/8.9-Oidc-OktaSample.ps1
[sc-8-10-multiple-schemes]: /pwsh/tutorial/examples/8.8-Multiple-Schemes.ps1
[sc-8-11-github-authentication]: /pwsh/tutorial/examples/8.10-GitHubAuthentication.ps1
[sc-8-12-full-demo]: /pwsh/tutorial/examples/8.12-Full-Demo.ps1
[sc-8-12-openid-connect]: /pwsh/tutorial/examples/8.11-OIDC.ps1
[sc-8-13-full-demo]: /pwsh/tutorial/examples/8.12-Full-Demo.ps1
[sc-9-1-basic-text-json]: /pwsh/tutorial/examples/9.1-Text-Json.ps1
[sc-9-2-structured-xml-yaml-csv]: /pwsh/tutorial/examples/9.2-Structured-Xml-Yaml-Csv.ps1
[sc-9-3-binary-stream]: /pwsh/tutorial/examples/9.3-Binary-Stream.ps1
[sc-9-4-html-templates-files]: /pwsh/tutorial/examples/9.4-Html-Files.ps1
[sc-9-5-special-bson-cbor]: /pwsh/tutorial/examples/9.5-Bson-Cbor.ps1
[sc-9-6-redirects]: /pwsh/tutorial/examples/9.6-Redirects.ps1
[sc-9-7-errors]: /pwsh/tutorial/examples/9.7-Errors.ps1
[sc-9-8-caching]: /pwsh/tutorial/examples/9.8-Caching.ps1
[sc-9-9-content-negotiation]: /pwsh/tutorial/examples/9.9-Content-Negotiation.ps1
[sc-9-10-low-level-response]: /pwsh/tutorial/examples/9.10-Low-Level-Response.ps1
[sc-10-1-hello-world]: /pwsh/tutorial/examples/10.1-OpenAPI-Hello-World.ps1
[sc-10-2-component-schemas]: /pwsh/tutorial/examples/10.2-OpenAPI-Component-Schema.ps1
[sc-10-3-requestbody-components]: /pwsh/tutorial/examples/10.3-OpenAPI-Component-RequestBody.ps1
[sc-10-4-parameter-components]: /pwsh/tutorial/examples/10.4-OpenAPI-Component-Parameter.ps1
[sc-10-5-response-components]: /pwsh/tutorial/examples/10.5-OpenAPI-Component-Response.ps1
[sc-10-6-complete-components]: /pwsh/tutorial/examples/10.6-OpenAPI-Components-RequestBody-Response.ps1
[sc-10-7-tags-and-docs]: /pwsh/tutorial/examples/10.7-OpenAPI-Tags.ps1
[sc-10-8-document-info]: /pwsh/tutorial/examples/10.8-OpenAPI-Document-Info.ps1
[sc-10-9-component-headers]: /pwsh/tutorial/examples/10.9-OpenAPI-Component-Header.ps1
[sc-10-10-component-links]: /pwsh/tutorial/examples/10.10-OpenAPI-Component-Link.ps1
[sc-10-11-component-callbacks]: /pwsh/tutorial/examples/10.11-OpenAPI-Component-Callback.ps1
[sc-10-12-webhooks]: /pwsh/tutorial/examples/10.12-OpenAPI-WebHook.ps1
[sc-10-13-examples]: /pwsh/tutorial/examples/10.13-OpenAPI-Examples.ps1
[sc-10-14-petstore-api]: /pwsh/tutorial/examples/10.14-OpenAPI-PetStore.ps1
[sc-10-15-museum-api]: /pwsh/tutorial/examples/10.15-OpenAPI-Museum.ps1
[sc-10-18-multi-document-openapi]: /pwsh/tutorial/examples/10.18-OpenAPI-MultiDocs.ps1
[sc-10-19-product-search-query]: /pwsh/tutorial/examples/10.19-OpenAPI-Hello-Query.ps1
[sc-10-20-sse]: /pwsh/tutorial/examples/10.20-OpenAPI-Sse.ps1
[sc-10-21-sse-broadcast]: /pwsh/tutorial/examples/10.21-OpenAPI-SseBroadcast.ps1
[sc-10-22-signalr]: /pwsh/tutorial/examples/10.17-OpenAPI-SignalR.ps1
[sc-10-23-xml-modeling]: /pwsh/tutorial/examples/10.22-OpenAPI-XML-Modeling.ps1
[sc-10-24-rfc6570-variable-mapping]: /pwsh/tutorial/examples/10.23-OpenAPI-Path-Template-Mapping.ps1
[sc-10-25-additional-pattern-properties]: /pwsh/tutorial/examples/10.24-OpenAPI-Additional-Pattern-Properties.ps1
[sc-11-1-razorpages-quickstart]: /pwsh/tutorial/examples/11.1-RazorPages.ps1
[sc-11-2-razorpages-antiforgery]: /pwsh/tutorial/examples/11.2-RazorPages-Antiforgery.ps1
[sc-12-1-scheduling-quickstart]: /pwsh/tutorial/examples/12.1-Scheduling-Quickstart.ps1
[sc-12-2-scheduling-cron]: /pwsh/tutorial/examples/12.2-Scheduling-Cron.ps1
[sc-12-3-scheduling-report]: /pwsh/tutorial/examples/12.3-Scheduling-Report.ps1
[sc-13-1-server-limits]: /pwsh/tutorial/examples/13.1-Server-Limits.ps1
[sc-13-2-server-options]: /pwsh/tutorial/examples/13.2-Server-Options.ps1
[sc-14-1-start-stop]: /pwsh/tutorial/examples/14.1-Start-Stop.ps1
[sc-14-2-full-demo]: /pwsh/tutorial/examples/14.2-Full-Demo.ps1
[sc-15-1-antiforgery]: /pwsh/tutorial/examples/15.1-Antiforgery.ps1
[sc-15-2-compression]: /pwsh/tutorial/examples/15.2-Compression.ps1
[sc-15-3-https-redirection]: /pwsh/tutorial/examples/15.3-Https-Redirection.ps1
[sc-15-4-https-hsts]: /pwsh/tutorial/examples/15.4-Https-Hsts.ps1
[sc-15-5-signalr]: /pwsh/tutorial/examples/15.5-SignalR.ps1
[sc-15-6-host-filtering]: /pwsh/tutorial/examples/15.6-HostFiltering.ps1
[sc-15-7-forwarded-headers]: /pwsh/tutorial/examples/15.7-Forwarded-Header.ps1
[sc-15-8-cors]: /pwsh/tutorial/examples/15.8-Cors-Multipolicy.ps1
[sc-15-9-sse]: /pwsh/tutorial/examples/15.9-Sse.ps1
[sc-15-10-ssebroadcast]: /pwsh/tutorial/examples/15.10-SseBroadcast.ps1
[sc-15-11-request-decompression]: /pwsh/tutorial/examples/15.11-Request-Decompression.ps1
[sc-16-1-health-quickstart]: /pwsh/tutorial/examples/16.1-Health-Quickstart.ps1
[sc-16-2-health-script-probe]: /pwsh/tutorial/examples/16.2-Health-Script-Probe.ps1
[sc-16-3-health-http-probe]: /pwsh/tutorial/examples/16.3-Health-Http-Probe.ps1
[sc-16-4-health-process-probe]: /pwsh/tutorial/examples/16.4-Health-Process-Probe.ps1
[sc-16-5-health-csharp-probe]: /pwsh/tutorial/examples/16.5-Health-CSharp-Probe.ps1
[sc-16-6-health-disk-probe]: /pwsh/tutorial/examples/16.6-Health-Disk-Probe.ps1
[sc-16-7-health-response-format]: /pwsh/tutorial/examples/16.7-Health-Response-Format.ps1
[sc-17-1-default-status-code-pages]: /pwsh/tutorial/examples/17.1-StatusCodePages-Default.ps1
[sc-17-2-custom-options]: /pwsh/tutorial/examples/17.2-StatusCodePages-Options.ps1
[sc-17-3-custom-handler-powershell]: /pwsh/tutorial/examples/17.3-StatusCodePages-CustomPowerShell.ps1
[sc-17-4-custom-handler-csharp-inline]: /pwsh/tutorial/examples/17.4-StatusCodePages-CustomCSharp.ps1
[sc-17-5-content-format]: /pwsh/tutorial/examples/17.5-StatusCodePages-ContentFormat.ps1
[sc-17-6-redirects]: /pwsh/tutorial/examples/17.6-StatusCodePages-Redirects.ps1
[sc-17-7-re-execute]: /pwsh/tutorial/examples/17.7-StatusCodePages-ReExecute.ps1
[sc-18-1-exceptionhandlingpath]: /pwsh/tutorial/examples/18.1-ExceptionHandling-Path.ps1
[sc-18-2-vbnet-handler]: /pwsh/tutorial/examples/18.2-ExceptionHandling-VBNet.ps1
[sc-18-3-csharp-inline]: /pwsh/tutorial/examples/18.3-ExceptionHandling-CSharp.ps1
[sc-18-4-problemdetails]: /pwsh/tutorial/examples/18.4-ExceptionHandling-ProblemDetails.ps1
[sc-18-5-developer-exception-page]: /pwsh/tutorial/examples/18.5-ExceptionHandling-Developer.ps1
[sc-19-1-sessions-basic]: /pwsh/tutorial/examples/19.1-Sessions.ps1
[sc-19-2-sessions-redis]: /pwsh/tutorial/examples/19.2-Sessions-Redis.ps1
[sc-19-3-sessions-sql]: /pwsh/tutorial/examples/19.3-Sessions-Sql.ps1
[sc-20-1-tasks]: /pwsh/tutorial/examples/20.1-Task.ps1
[sc-21-1-localization]: /pwsh/tutorial/examples/21.1-Localization.ps1
[sc-21-2-razor-localization]: /pwsh/tutorial/examples/21.2-Razor-Localization.ps1
[sc-22-1-basic-multipart]: /pwsh/tutorial/examples/22.1-Basic-Multipart.ps1
[sc-22-2-multiple-files]: /pwsh/tutorial/examples/22.2-Multiple-Files.ps1
[sc-22-3-urlencoded]: /pwsh/tutorial/examples/22.3-UrlEncoded.ps1
[sc-22-4-multipart-mixed]: /pwsh/tutorial/examples/22.4-Multipart-Mixed.ps1
[sc-22-5-nested-multipart]: /pwsh/tutorial/examples/22.5-Nested-Multipart.ps1
[sc-22-6-request-compressed]: /pwsh/tutorial/examples/22.6-Request-Compressed.ps1
[sc-22-7-part-compressed]: /pwsh/tutorial/examples/22.7-Part-Compressed.ps1
[sc-22-8-basic-multipart-openapi]: /pwsh/tutorial/examples/22.8-basic-multipart-OpenAPI.ps1
[sc-22-9-multiple-files-openapi]: /pwsh/tutorial/examples/22.9-Multiple-Files-OpenAPI.ps1
[sc-22-10-url-encoded-openapi]: /pwsh/tutorial/examples/22.10-Url-Encoded-OpenAPI.ps1
[sc-22-11-multipart-mixed-openapi]: /pwsh/tutorial/examples/22.11-Multipart-Mixed-OpenAPI.ps1
[sc-22-12-nested-multipart-openapi]: /pwsh/tutorial/examples/22.12-Nested-Multipart-OpenAPI.ps1
[sc-22-13-request-compressed-openapi]: /pwsh/tutorial/examples/22.13-Request-Compressed-OpenAPI.ps1
[sc-22-14-part-compressed-openapi]: /pwsh/tutorial/examples/22.14-Part-Compressed-OpenAPI.ps1
[sc-22-15-file-hash-openapi]: /pwsh/tutorial/examples/22.15-File-Hash-OpenAPI.ps1
[sc-22-16-file-hash-rule]: /pwsh/tutorial/examples/22.16-File-Hash-Rule.ps1
