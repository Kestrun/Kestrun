---
title: Runtime Contract Enforcement
parent: OpenAPI
nav_order: 6
---

# Runtime Contract Enforcement

This is the part that makes Kestrun feel different: OpenAPI is not just documentation. It can become an **allow-list** for what the endpoint accepts and returns.

## Response content types

If an operation declares that it produces JSON, then returning a different format like YAML is a contract violation.

### Practical rule of thumb

- Use `Write-KrResponse` when you want **content negotiation**
- Use `Write-KrJsonResponse` / `Write-KrYamlResponse` / `Write-KrXmlResponse` when you want to force a specific format

If you force a format, make sure your OpenAPI response content types match what you actually return.

## Request body content types (415)

If you declare request body content types via `[OpenApiRequestBody(ContentType = 'application/json')]`,
Kestrun rejects mismatches with **415 Unsupported Media Type**.

This is intentional: clients must send one of the declared `Content-Type` values.

## Parameter and body schemas

OpenAPI parameter schemas and model schemas are typically derived from:

- Parameter types such as `[int]`, `[string]`, and PowerShell class types
- Validation attributes such as `[ValidateRange]`, `[ValidateSet]`, `[ValidatePattern]`, `[ValidateNotNullOrEmpty]`
- OpenAPI component metadata such as `[OpenApiParameterComponent]`, `[OpenApiSchemaComponent]`, and `[OpenApiPropertyAttribute]`

At runtime, Kestrun applies these rules during binding and validation.

## Auto client-error responses

When OpenAPI metadata enables runtime contract checks, Kestrun can add matching client-error responses to the OpenAPI operation.

- **400 Bad Request**: malformed JSON, invalid route/query conversions, request binding/parsing failures
- **422 Unprocessable Entity**: validation failures after successful parsing/binding
- **415 Unsupported Media Type**: request `Content-Type` not allowed by the request body contract
- **406 Not Acceptable**: no overlap between the client `Accept` header and the operation response content types

These responses use a shared schema component (default name: `KestrunErrorResponse`) and default media type `application/problem+json`.

## Configure the generated error contract

Use `Set-KrOpenApiErrorSchema` to control both the schema component name and one or more response media types.

```powershell
# Default behavior
Set-KrOpenApiErrorSchema -Name 'KestrunErrorResponse'

# Custom schema name + multiple media types
Set-KrOpenApiErrorSchema -Name 'ApiError' -ContentType @('application/problem+json', 'application/json')
```

Configure this before `Build-KrOpenApiDocument` or `Add-KrOpenApiRoute` so generated output stays stable for tests and clients.

## Customize the runtime error payload

Use `Set-KrPowerShellErrorResponse` when you also want to customize the runtime error payload shape for PowerShell route execution,
while keeping the OpenAPI error contract aligned.

```powershell
Set-KrOpenApiErrorSchema -Name 'ApiError' -ContentType @('application/problem+json', 'application/json')

Set-KrPowerShellErrorResponse -ScriptBlock {
  $payload = [ordered]@{
    status = $StatusCode
    title = 'Request failed'
    detail = $ErrorMessage
    path = [string]$Context.Request.Path
    timestamp = (Get-Date).ToUniversalTime().ToString('o')
  }

  Write-KrJsonResponse -InputObject $payload -StatusCode $StatusCode -ContentType 'application/problem+json'
}
```

If no custom script is configured, or if the custom script fails, Kestrun falls back to the default error writer.

## Why this matters

The result is simple and powerful:

- your generated OpenAPI document stays aligned with real behavior
- content negotiation is documented and enforced
- validation rules show up in docs and in runtime behavior
- client error responses can be generated consistently

That is what turns OpenAPI from "nice docs" into a real API contract in Kestrun.
