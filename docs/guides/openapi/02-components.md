---
title: Components
parent: OpenAPI
nav_order: 2
---

# Components

Define **schemas first**, then build reusable request bodies, responses, parameters, headers, links, and examples on top of them.

## Schema components

Use `[OpenApiSchemaComponent]` to define reusable data models.

### Basic schema

```powershell
[OpenApiSchemaComponent(RequiredProperties = ('username', 'email'))]
class User {
    [OpenApiPropertyAttribute(Description = 'Unique ID', Format = 'int64', Example = 1)]
    [long]$id

    [OpenApiPropertyAttribute(Description = 'Username', Example = 'jdoe')]
    [string]$username

    [OpenApiPropertyAttribute(Description = 'Email address', Format = 'email')]
    [string]$email
}
```

### Enums and validation

Use PowerShell validation attributes alongside OpenAPI attributes.

```powershell
[OpenApiSchemaComponent()]
class Product {
    [OpenApiPropertyAttribute(Description = 'Product Status', Example = 'available')]
    [ValidateSet('available', 'pending', 'sold')]
    [string]$status

    [OpenApiPropertyAttribute(Minimum = 0, Maximum = 100)]
    [int]$stock
}
```

### Arrays

To define a schema that is a list of other objects, use inheritance and the `Array` property.

```powershell
[OpenApiSchemaComponent(Description = 'List of users', Array = $true)]
class UserList : User {}
```

### Primitive schema components

Kestrun supports two common ways to model primitive values in OpenAPI:

1. Use a native PowerShell/.NET type (for example `[string]`, `[int]`, `[datetime]`)
2. Create a reusable primitive schema component by deriving from:
   - `OpenApiString`
   - `OpenApiInteger`
   - `OpenApiNumber`
   - `OpenApiBoolean`

Use the primitive wrappers when you want a named reusable schema under `components.schemas`.

```powershell
[OpenApiSchemaComponent(Description = 'Employee identifier', Example = 'a54a57ca-36f8-421b-a6b4-2e8f26858a4c')]
class EmployeeId : OpenApiUuid {}

[OpenApiSchemaComponent(Description = 'Calendar date (YYYY-MM-DD)', Example = '2026-01-13')]
class Date : OpenApiDate {}
```

Use the alias in other schemas to produce `$ref` references:

```powershell
[OpenApiSchemaComponent(RequiredProperties = ('id', 'hireDate'))]
class Employee {
    [EmployeeId]$id
    [Date]$hireDate
}
```

### Arrays of scalar aliases

```powershell
[OpenApiSchemaComponent(Description = 'List of visit dates', Array = $true)]
class Dates : Date {}
```

### Common schema patterns

#### Plain object schema

```powershell
[OpenApiSchemaComponent()]
class User {
    [string]$username
    [string]$email
}
```

#### Required properties

```powershell
[OpenApiSchemaComponent(RequiredProperties = ('username'))]
class User {
    [string]$username
    [string]$email
}
```

#### Enums

**ValidateSet inline enum:**

```powershell
[OpenApiSchemaComponent()]
class Order {
    [ValidateSet('placed', 'approved', 'delivered')]
    [string]$status
}
```

**PowerShell enum as reusable schema component:**

```powershell
enum TicketType { general; event }

[OpenApiSchemaComponent()]
class Ticket {
    [TicketType]$type
}
```

#### Property-level arrays

```powershell
[OpenApiSchemaComponent()]
class Album {
    [string[]]$photoUrls
}
```

#### Inheritance → `allOf`

```powershell
[OpenApiSchemaComponent(RequiredProperties = ('id'))]
class PersonBase {
    [string]$id
}

[OpenApiSchemaComponent(RequiredProperties = ('email'))]
class Person : PersonBase {
    [string]$email
}
```

#### Dictionary / `additionalProperties`

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

#### Pattern properties

```powershell
[OpenApiSchemaComponent(Description = 'Feature flags by prefix')]
[OpenApiPatternProperties(KeyPattern = '^x-')]
class FeatureFlags {}
```

#### Composition with `oneOf` / `anyOf` / `allOf`

```powershell
[OpenApiSchemaComponent(
  OneOfTypes = @([Cat], [Dog]),
  DiscriminatorPropertyName = 'kind',
  DiscriminatorMappingKeys = ('cat', 'dog'),
  DiscriminatorMappingRefs = ('#/components/schemas/Cat', '#/components/schemas/Dog')
)]
class Pet {}
```

### XML modeling

Kestrun supports OpenAPI XML modeling via `OpenApiXml`. The XML metadata is used both for OpenAPI generation and for runtime XML serialization/deserialization.

```powershell
[OpenApiSchemaComponent(RequiredProperties = ('Id', 'Name', 'Price'))]
[OpenApiXml(Name = 'Product')]
class Product {
    [OpenApiXml(Name = 'id', Attribute = $true)]
    [int]$Id

    [OpenApiXml(Name = 'ProductName')]
    [string]$Name

    [OpenApiXml(Name = 'Price', Namespace = 'http://example.com/pricing', Prefix = 'price')]
    [double]$Price

    [OpenApiXml(Name = 'Item', Wrapped = $true)]
    [string[]]$Items
}
```

## Request body components

Use `[OpenApiRequestBodyComponent]` to define reusable request payloads.

```powershell
# Define the base schema
[OpenApiSchemaComponent(RequiredProperties = ('name', 'price'))]
class ProductSchema {
    [string]$name
    [double]$price
}

# Define a schema type for the create operation
class CreateProductRequest : ProductSchema {}

# Define the Request Body Component
[OpenApiRequestBodyComponent(
    Description = 'Product creation payload',
    Required = $true,
    ContentType = ('application/json', 'application/xml')
)]
[CreateProductRequest]$CreateProductRequest = NoDefault
```

## Response components

Use `[OpenApiResponseComponent]` to define reusable response objects under `components.responses`.

```powershell
[OpenApiSchemaComponent(RequiredProperties = ('code', 'message'))]
class ErrorResponse {
    [OpenApiPropertyAttribute(Example = 400)]
    [int]$code

    [OpenApiPropertyAttribute(Example = 'Invalid input')]
    [string]$message
}

[OpenApiResponseComponent(Description = 'Not Found', ContentType = ('application/json', 'application/xml'))]
[ErrorResponse]$NotFound = NoDefault
```

## Parameter components

Use `[OpenApiParameterComponent]` to define reusable parameter components.

> **Important (defaults):**
>
> - Assign a real value when you want an OpenAPI `schema.default`
> - Use `= NoDefault` when you do not want any OpenAPI default emitted
> - Do not use `$null` as a “no default” marker

```powershell
[OpenApiParameterComponent(In = [OaParameterLocation]::Query, Description = 'Page number', Minimum = 1, Example = 1)]
[int]$page = 1

[OpenApiParameterComponent(In = [OaParameterLocation]::Query, Description = 'Items per page', Minimum = 1, Maximum = 100, Example = 20)]
[int]$limit = 20

[OpenApiParameterComponent(In = 'Header', Description = 'Optional correlation id for tracing', Example = '2f2d68c2-9b7a-4b5c-8b1d-0fdff2a4b9a3')]
[string]$correlationId = NoDefault
```

You can combine them with standard PowerShell validation attributes:

```powershell
[OpenApiParameterComponent(In = 'Query', Description = 'Sort field')]
[ValidateSet('name', 'price')]
[string]$sortBy = 'name'
```

## Headers

### Reusable response headers

```powershell
New-KrOpenApiHeader `
    -Description 'Correlation id for tracing the request across services.' `
    -Schema ([string]) `
    -Required |
    Add-KrOpenApiComponent -Name 'X-Correlation-Id'
```

### Inline one-off response header

```powershell
[OpenApiResponse(StatusCode = '400', Description = 'Invalid input')]
[OpenApiResponseHeader(StatusCode = '400', Key = 'X-Error-Code', Description = 'Machine-readable error code.', Schema = ([string]))]
```

## Links

OpenAPI links describe how one successful response can be used as input to another operation.

```powershell
New-KrOpenApiLink -OperationId 'getUser' -Description 'Fetch the user resource.' `
    -Parameters @{ userId = '$response.body#/id' } |
    Add-KrOpenApiComponent -Name 'GetUserLink'
```

## Examples

Use reusable examples when the same payload must show up in multiple places.

- `New-KrOpenApiExample | Add-KrOpenApiComponent` → store under `components/examples`
- `New-KrOpenApiExample | Add-KrOpenApiInline` → reusable inline snippets
- `OpenApiResponseExampleRef`, `OpenApiRequestBodyExampleRef`, `OpenApiParameterExampleRef` → reference them

## Vendor extensions

Vendor extensions are the standard OpenAPI way to attach non-standard metadata.

### Document-level

```powershell
Add-KrOpenApiExtension -Extensions ([ordered]@{
  'x-tagGroups' = @(
    @{ name = 'Common'; tags = @('operations') },
    @{ name = 'Commerce'; tags = @('orders', 'orders.read') }
  )
})
```

### Operation-level

```powershell
function getMuseumHours {
  [OpenApiPath(HttpVerb = 'get', Pattern = '/museum-hours')]
  [OpenApiExtension('x-badges', '{"name":"Beta","position":"before","color":"purple"}')]
  param()
}
```

### Component-level

Schema, parameter, request body, response, header, link, and example components can all carry `x-*` extensions.

## Attribute usage matrix

| Attribute | Target | Key Properties |
| :--- | :--- | :--- |
| **`[OpenApiSchemaComponent]`** | Class | `Key`, `Examples`, `RequiredProperties`, `Description`, `Array`, `Type`, `Format`, `Example` |
| **`[OpenApiRequestBodyComponent]`** | Variable | `Key`, `ContentType`, `Required`, `Description`, `Inline` |
| **`[OpenApiResponseComponent]`** | Variable | `Description`, `Summary`, `ContentType`, `Inline` |
| **`[OpenApiParameterComponent]`** | Variable | `In`, `Description`, `Required`, `ContentType`, `Example`, `Minimum`, `Maximum`, `Default` |
| **`[OpenApiPropertyAttribute]`** | Parameter/Property/Field | `Description`, `Example`, `Format`, `Required`, `Enum`, `Minimum`, `Maximum`, `Default` |
