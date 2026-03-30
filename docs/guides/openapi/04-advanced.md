---
title: Advanced
parent: OpenAPI
nav_order: 4
---

# Advanced

This page gathers the deeper modeling features that are valuable once the reader already understands schemas, components, and route decoration.

## Additional and pattern properties

### `additionalProperties`

To model an object map, set `AdditionalPropertiesAllowed` and optionally specify the value type.

```powershell
[OpenApiSchemaComponent(
    Description = 'Inventory counts by status',
    AdditionalPropertiesAllowed = $true,
    AdditionalProperties = [OpenApiInt32]
)]
class Inventory {
    [string]$help
}
```

Runtime notes:

- Dynamic key/value pairs are stored in the model's `AdditionalProperties` hashtable
- Unknown keys from JSON or form data are added there
- Values are converted to the declared value type when possible

### Pattern properties

```powershell
[OpenApiSchemaComponent(Description = 'Feature flags by prefix')]
[OpenApiPatternProperties(KeyPattern = '^x-')]
class FeatureFlags {}
```

Runtime notes:

- Pattern-property rules populate `AdditionalProperties` only with keys that match the regex
- Keys that do not match any pattern are ignored during binding
- When multiple patterns exist, the first matching rule determines type conversion

## Composition and discriminators

```powershell
[OpenApiSchemaComponent(
  OneOfTypes = @([Cat], [Dog]),
  DiscriminatorPropertyName = 'kind',
  DiscriminatorMappingKeys = ('cat', 'dog'),
  DiscriminatorMappingRefs = ('#/components/schemas/Cat', '#/components/schemas/Dog')
)]
class Pet {}
```

This produces a `oneOf` schema with a discriminator, which is especially useful for clients and code generation.

## XML modeling

`OpenApiXml` can drive both:

1. OpenAPI `schema.xml` metadata
2. Runtime XML serialization / deserialization

```powershell
function createProduct {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/products')]
    [OpenApiResponse(StatusCode = '201', Schema = [Product], ContentType = ('application/json', 'application/xml'))]
    param(
        [OpenApiRequestBody(ContentType = 'application/xml')]
        [Product]$Body
    )

    Write-KrResponse -InputObject $Body -StatusCode 201
}
```

## External docs

### Document-level external docs

```powershell
$apiPortalExtensions = [ordered]@{ 'x-docType' = 'portal'; 'x-audience' = 'internal'; 'x-owner' = 'api-platform' }
Add-KrOpenApiExternalDoc -Description 'API portal' -Url 'https://example.com/api-portal' -Extensions $apiPortalExtensions
```

### Tag-level external docs

```powershell
$ordersExternalDocs = New-KrOpenApiExternalDoc -Description 'Order docs' -Url 'https://example.com/orders'
Add-KrOpenApiTag -Name 'orders' -Description 'Order operations' -Parent 'operations' -Kind 'resource' -ExternalDocs $ordersExternalDocs
```

## Headers and links

### Header component references

```powershell
function createUser {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/users', Tags = 'Users')]
    [OpenApiResponse(StatusCode = '201', Description = 'Created')]
    [OpenApiResponseHeaderRef(StatusCode = '201', Key = 'X-Correlation-Id', ReferenceId = 'X-Correlation-Id')]
    [OpenApiResponseHeaderRef(StatusCode = '201', Key = 'Location', ReferenceId = 'Location')]
    param()
}
```

### Link component references

```powershell
function createUser {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/users', Tags = 'Users')]
    [OpenApiResponse(StatusCode = '201', Description = 'Created')]
    [OpenApiResponseLinkRef(StatusCode = '201', Key = 'get', ReferenceId = 'GetUserLink')]
    [OpenApiResponseLinkRef(StatusCode = '201', Key = 'update', ReferenceId = 'UpdateUserLink')]
    param()
}
```

## Feature matrix

| Feature | Status | Notes |
| :--- | :--- | :--- |
| **Schemas** | ✅ Supported | Use `[OpenApiSchemaComponent]` classes |
| **Request Bodies** | ✅ Supported | Use `[OpenApiRequestBodyComponent]` variables |
| **Responses** | ✅ Supported | Use `[OpenApiResponseComponent]` variables |
| **Parameters** | ✅ Supported | Define components with `[OpenApiParameterComponent]`, reference via `[OpenApiParameterRef]` |
| **Headers** | ✅ Supported | Use `New-KrOpenApiHeader` + `Add-KrOpenApiComponent`, then reference via `OpenApiResponseHeaderRef` |
| **Examples** | ✅ Supported | Use `New-KrOpenApiExample` + `Add-KrOpenApiComponent`, then reference via example ref attributes |
| **Inheritance** | ✅ Supported | PowerShell class inheritance works for schemas |
| **Generics** | 🚧 Partial | Use `Array = $true` for lists |
| **Links** | ✅ Supported | Use `New-KrOpenApiLink` + `Add-KrOpenApiComponent`, then reference via `OpenApiResponseLinkRef` |
| **Extensions (x-*)** | ✅ Supported | Supported at document, operation, and component levels |

## External references

- [OpenAPI Specification v3.1.0](https://spec.openapis.org/oas/v3.1.0)
- [Swagger UI Documentation](https://swagger.io/docs/open-source-tools/swagger-ui/usage/installation/)
- [PowerShell Classes](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_classes)
- [JSON Schema Validation](https://json-schema.org/understanding-json-schema/)
