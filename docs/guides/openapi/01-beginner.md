---
title: Beginner
parent: OpenAPI
nav_order: 1
---

# Beginner

This page is the shortest path from "nothing" to a working Kestrun OpenAPI document.

## Concepts

| Concept | Description |
|---------|-------------|
| **Schema Component** | A PowerShell class decorated with `[OpenApiSchemaComponent]`. Defines data structure. |
| **Request Body** | A PowerShell variable decorated with `[OpenApiRequestBodyComponent]`. Defines a reusable payload entry under `components.requestBodies`. |
| **Response** | A variable decorated with `[OpenApiResponseComponent]`. Defines a reusable response entry under `components.responses`. |
| **Parameter** | A PowerShell parameter/variable decorated with `[OpenApiParameterComponent]`. Defines reusable parameters. |
| **Property Attribute** | `[OpenApiPropertyAttribute]` on class properties defines validation, examples, and descriptions. |
| **Route Attribute** | `[OpenApiPath]` on functions defines the HTTP method and route pattern. |

## Minimal example

```powershell
Import-Module Kestrun

# 1. Define a schema
[OpenApiSchemaComponent(RequiredProperties = ('name'))]
class Pet {
    [OpenApiPropertyAttribute(Description = 'Pet ID', Example = 1)]
    [long]$id

    [OpenApiPropertyAttribute(Description = 'Pet Name', Example = 'Fido')]
    [string]$name
}

# 2. Setup server & metadata
New-KrServer -Name 'Pet API'
Add-KrEndpoint -Port 5000
Add-KrOpenApiInfo -Title 'Pet API' -Version '1.0.0'

# 3. Define route
function getPet {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/pets/{id}')]
    [OpenApiResponse(StatusCode = '200', Description = 'Found pet', Schema = [Pet])]
    param([int]$id)

    Write-KrJsonResponse @{ id = $id; name = 'Fido' }
}

# 4. Enable & build
Enable-KrConfiguration
Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrOpenApiRoute

Build-KrOpenApiDocument
Start-KrServer
```

## Document metadata

Configure the top-level API information.

```powershell
# Basic Info
Add-KrOpenApiInfo -Title 'My API' -Version '1.0.0' -Description 'API Description'

# Contact & License
Add-KrOpenApiContact -Name 'Support' -Email 'help@example.com'

# License can be expressed either as a URL (common) or an SPDX identifier
Add-KrOpenApiLicense -Name 'MIT' -Url 'https://opensource.org/licenses/MIT'
# Add-KrOpenApiLicense -Name 'Apache 2.0' -Identifier 'Apache-2.0'

# Servers
Add-KrOpenApiServer -Url 'https://api.example.com' -Description 'Production'
Add-KrOpenApiServer -Url 'http://localhost:5000' -Description 'Local'
```

### Templated servers

```powershell
$serverVars = New-KrOpenApiServerVariable -Name 'env' -Default 'dev' -Enum @('dev', 'staging', 'prod') -Description 'Deployment environment'
Add-KrOpenApiServer -Url 'https://{env}.api.example.com' -Description 'Environment-specific endpoint' -Variables $serverVars
```

## Build and view

1. Enable configuration: `Enable-KrConfiguration`
2. Register viewers: `Add-KrApiDocumentationRoute -DocumentType Swagger`
3. Register OpenAPI route: `Add-KrOpenApiRoute`
4. Build: `Build-KrOpenApiDocument`
5. Test: `Test-KrOpenApiDocument`
6. Start: `Start-KrServer ...`

Access the default UI routes at `/docs/swagger` and `/docs/redoc`, and the raw OpenAPI JSON at `/openapi/v3.1/openapi.json`.

> **Note on CORS:** If you plan to host Swagger UI or other documentation viewers on a different domain or port than your API,
enable CORS in your Kestrun server configuration so the browser can fetch the OpenAPI JSON.
