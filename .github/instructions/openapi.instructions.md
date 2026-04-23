---
applyTo: "docs/guides/openapi/**,docs/pwsh/tutorial/10.openapi/**,docs/_includes/examples/pwsh/*OpenAPI*.ps1,tests/PowerShell.Tests/Kestrun.Tests/Tutorial/Tutorial-10*.Tests.ps1"
---

# Kestrun OpenAPI Instructions

OpenAPI changes must follow `.github/copilot-instructions.md` as the global source of truth.
These rules apply when implementing, documenting, or testing OpenAPI features in this repository.

## Core Principles

- Treat Kestrun OpenAPI as code-first. PowerShell classes, variables, attributes, and route functions define the contract.
- Do not invent unsupported OpenAPI behavior, attributes, cmdlets, routes, defaults, or document shapes.
- Verify every OpenAPI claim against current Kestrun code, the OpenAPI guide, or existing working examples and tests.
- Keep runtime behavior and OpenAPI documentation aligned. The spec should describe what the route actually does.
- Prefer minimal, reusable components when the same schema, parameter, request body, or response appears more than once.

## Standard Implementation Flow

When creating a new OpenAPI example or feature implementation, prefer this order:

1. Create the server with `New-KrServer`.
2. Add a listener with `Add-KrEndpoint -Port $Port`.
3. Add top-level document metadata with `Add-KrOpenApiInfo` and optional contact, license, server, tag, or external-doc entries.
4. Define schema classes, parameter components, request body components, response components, headers, links, examples, or callbacks as needed.
5. Define route functions using OpenAPI attributes such as `[OpenApiPath]`, `[OpenApiResponse]`, `[OpenApiRequestBody]`, or reusable component references.
6. Finalize staged configuration with `Enable-KrConfiguration`.
7. Register UI routes with `Add-KrApiDocumentationRoute` when the example should expose Swagger, ReDoc, or other viewers.
8. Register raw document routes with `Add-KrOpenApiRoute`.
9. Build and validate with `Build-KrOpenApiDocument` and `Test-KrOpenApiDocument` when practical.
10. Start the server with `Start-KrServer`.

## Top-Level Document Rules

- Prefer `Add-KrOpenApiInfo` for `info.title`, `info.version`, and `info.description`.
- Use `Add-KrOpenApiContact`, `Add-KrOpenApiLicense`, `Add-KrOpenApiServer`, `Add-KrOpenApiTag`, and `Add-KrOpenApiExternalDoc` instead of manually modeling top-level objects.
- Use `Add-KrOpenApiServer -Variables` with `New-KrOpenApiServerVariable` for templated server URLs.
- Use extension keys only when they start with `x-`.
- When the example exposes documentation UIs, keep the JSON route and UI routes consistent with the intended OpenAPI version and endpoint.

## Route And Operation Rules

- Use `[OpenApiPath(...)]` on the route function to define the HTTP method, pattern, tags, and operation metadata.
- Use comment-based help blocks for route functions so Kestrun can populate OpenAPI summary, description, and parameter help.
- Use `[OpenApiResponse(...)]` for simple inline response documentation.
- Use `[OpenApiResponseRef(...)]` or reusable response components when the same response appears on multiple operations.
- Put `[OpenApiRequestBody(...)]` or `[OpenApiRequestBodyRef(...)]` on the body parameter, not on the function.
- For path parameters, prefer explicit parameter metadata on the function parameter with `[OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true)]`.
- Keep the runtime route contract aligned with the documented contract. If the operation returns `201`, `400`, or `404`, the implementation should actually write that status code.

## Component Rules

- Use `[OpenApiSchemaComponent]` on PowerShell classes that should appear under `components.schemas`.
- Use `[OpenApiPropertyAttribute]` on class properties for descriptions, examples, formats, enums, and constraints.
- Use `[OpenApiParameterComponent]` on variables for reusable parameter components.
- Use `[OpenApiRequestBodyComponent]` on variables for reusable request bodies.
- Use `[OpenApiResponseComponent]` on variables for reusable responses.
- Use `= NoDefault` on parameter and request-body component variables when you do not want an OpenAPI default value emitted.
- If a schema should be reusable by reference, prefer a named component over repeating inline anonymous shapes.
- If reusable primitives are needed, define a scalar alias component from `OpenApiDate`, `OpenApiUuid`, `OpenApiInt64`, and related scalar wrappers instead of expecting the base wrapper type to appear in `components.schemas`.

## Request, Response, And Validation Guidance

- Prefer typed request and response models when the runtime can resolve the type cleanly.
- If a script-defined type is not reliably resolvable in the request runspace, fall back to `[object]` plus `[OpenApiRequestBodyRef(...)]`.
- Use PowerShell validation attributes such as `ValidateSet`, `ValidateRange`, `ValidateLength`, and `ValidatePattern` to shape schema constraints when appropriate.
- Use `Write-KrResponse` when the route should negotiate output by `Accept` header.
- Use `Write-KrJsonResponse` only when the operation is intentionally JSON-only.
- Keep generated error contracts and runtime error payloads aligned. When custom error payloads are part of the example, pair them with `Set-KrOpenApiErrorSchema` or matching response components instead of documenting one shape and returning another.

## Kestrun-Specific OpenAPI Patterns

- For multipart and form uploads, prefer `KrBindForm` plus `KrPart` with OpenAPI attributes on the route when you want typed binding and OpenAPI-friendly request bodies.
- For SSE endpoints, document responses as `text/event-stream` with a `string` schema.
- For SignalR, document hub-adjacent HTTP routes rather than the hub itself as a REST operation.
- For HTTP `QUERY`, use `HttpVerb = 'query'` in `[OpenApiPath]` and remember that OpenAPI 3.0 and 3.1 emit it under `x-oai-additionalOperations.QUERY`, while OpenAPI 3.2 emits a native `query` operation.
- For callbacks and webhooks, use the repository’s callback and webhook attribute patterns rather than inventing raw document fragments.
- For vendor extensions, use `[OpenApiExtension(...)]` or the corresponding `New-KrOpenApi* -Extensions` patterns already used in existing examples.

## Example And Tutorial Standards

- OpenAPI samples under `docs/_includes/examples/pwsh/` must remain runnable tutorial source, not pseudocode.
- Keep OpenAPI tutorial pages under `docs/pwsh/tutorial/10.openapi/` synchronized with the example script they embed.
- When adding or changing an OpenAPI example, update the matching tutorial test under `tests/PowerShell.Tests/Kestrun.Tests/Tutorial/` if behavior or document shape changed.
- Prefer existing tutorial examples such as:
  - `10.1-OpenAPI-Hello-World.ps1` for minimal setup
  - `10.2-OpenAPI-Component-Schema.ps1` for schema components
  - `10.4-OpenAPI-Component-Parameter.ps1` for reusable parameters
  - `10.6-OpenAPI-Components-RequestBody-Response.ps1` for request and response components
  - `10.19-OpenAPI-Hello-Query.ps1` for HTTP `QUERY`

## Testing And Verification

- Prefer validating generated specs with `Build-KrOpenApiDocument` and `Test-KrOpenApiDocument` during development and in examples where that is already the pattern.
- For tutorial tests, assert the emitted document through `/openapi/v3.1/openapi.json` unless the example intentionally focuses on another version.
- If an example exposes multiple OpenAPI documents or UI routes, ensure the documentation UI points at the intended JSON endpoint.
- Verify that documented request bodies, response codes, media types, parameter constraints, and component references appear in the generated document and match runtime behavior.
- When changing reusable examples, compare against the checked-in JSON assets under `docs/_includes/examples/pwsh/Assets/OpenAPI/` when those snapshots are part of the feature workflow.

## Change Discipline

- Prefer incremental edits over large OpenAPI refactors.
- Preserve existing naming and numbering conventions for tutorial examples and pages.
- Do not change unrelated routes, examples, or document versions while addressing a focused OpenAPI task.
- If a behavior is unknown or inconsistent across sources, say so and resolve it from code or tests before documenting it.

## Completion Notes

When reporting OpenAPI work, explain:

- what OpenAPI implementation, example, guide, or test was updated
- what repository sources were used to verify the change
- what document version or endpoint was validated
- any remaining uncertainty, runtime limitations, or version-specific behavior
