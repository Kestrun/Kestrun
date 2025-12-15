---
layout: default
title: ExceptionHandling
parent: Guides
nav_order: 50
---

# Exception Handling

Robust error handling patterns for PowerShell and C# routes, with consistent API responses.

## Overview

Kestrun exposes a flexible exception handling middleware that can:

- Re-execute to a dedicated error path
- Run a custom handler (PowerShell script or C# inline code)
- Emit RFC 7807 ProblemDetails
- Show developer exception page (development only)

These modes are driven by `Enable-KrExceptionHandling` and work uniformly for PowerShell, C#, and VB.NET routes.

## When to use each mode

- Re-execute to Path: Centralize rendering of user-friendly error pages or JSON payloads.
- PowerShell Handler: Author error formatting in PowerShell; great for PS-first apps.
- C# Inline Handler: Maximum performance with typed access to `Context`.
- ProblemDetails: API-first services that want standardized machine-readable errors.
- Developer Exception Page: Local development diagnostics only.

## Middleware ordering

Place exception handling early in the pipeline, before routing that might short-circuit errors, but after diagnostics you still want to run.

Common placement:

1. Logging / correlation
2. Exception handling
3. Static files / compression
4. Routing and endpoints

## PowerShell vs C# behavior

- PowerShell routes can handle their own exceptions. If you want the middleware to handle PS exceptions,
  throw outside route blocks or ensure the middleware is configured to capture them.
- C# and other compiled handlers naturally flow exceptions into the middleware pipeline.

> Note: Custom exception handlers implemented as PowerShell scripts are not supported at this time. Use C# or VB.NET for custom exception handling middleware.

## ProblemDetails contracts

When `-UseProblemDetails` is enabled, unhandled exceptions become JSON with fields including:

- type: A URI reference that identifies the problem type (optional)
- title: Short, human-readable summary
- status: HTTP status code
- detail: Human-readable explanation
- traceId: Correlation identifier for diagnostics

Clients should not rely on exact wording. Contract shape follows RFC 7807.

## Developer exception page

- Contains stack traces and, optionally, source snippets. Guard with environment checks.
- Use `-IncludeDetailsInDevelopment` and tweak `-SourceCodeLineCount` for context length.

## Cmdlets and APIs

- PowerShell: `Enable-KrExceptionHandling`, `Write-KrJsonResponse`, `Write-KrTextResponse`
- C#: `Context.Response.WriteJsonResponse`, `WriteTextResponse`, etc.

## Tutorials

- [Re-execute to Path](/pwsh/tutorial/18.ExceptionHandling/1.Re-execute-Path)
- [PowerShell Handler](/pwsh/tutorial/18.ExceptionHandling/2.PowerShell-Handler)
- [C# Inline Handler](/pwsh/tutorial/18.ExceptionHandling/3.CSharp-Inline)
- [ProblemDetails Fallback](/pwsh/tutorial/18.ExceptionHandling/4.ProblemDetails)
- [Developer Exception Page](/pwsh/tutorial/18.ExceptionHandling/5.Developer-Exception-Page)

## Security considerations

- Do not enable developer exception pages in production.
- Avoid leaking stack traces or sensitive details in ProblemDetails. Use generic titles and include only safe fields.

## References

- [RFC 7807 — Problem Details for HTTP APIs](https://datatracker.ietf.org/doc/html/rfc7807)
- [ASP.NET Core error handling](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling)

---

Return to the [Guides index](./index).
