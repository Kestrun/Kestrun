---
title: Routing
parent: OpenAPI
nav_order: 3
---

# Routing

Routes are PowerShell functions decorated with attributes. This is where schemas, parameters, request bodies, responses, tags, and extensions all come together.

## `[OpenApiPath]`

Defines the HTTP method, route pattern, summary, description, and tags.

```powershell
[OpenApiPath(
    HttpVerb = 'post',
    Pattern = '/users',
    Summary = 'Create a user',
    Tags = @('Users')
)]
```

## `[OpenApiResponse]`

Defines possible responses. You can reference a class type directly or a component name.

```powershell
# Reference by type
[OpenApiResponse(StatusCode = '200', Schema = [User])]

# Reference by component name
[OpenApiResponse(StatusCode = '400', Schema = "ErrorResponse")]

# Inline description
[OpenApiResponse(StatusCode = '204', Description = 'No Content')]
```

## `[OpenApiRequestBody]`

Defines the expected request body.

```powershell
# Option A: typed body
param(
  [OpenApiRequestBody(ContentType = 'application/json')]
  [CreateUserRequest]$Body
)

# Option B: explicit reference
param(
  [OpenApiRequestBodyRef(ReferenceId = 'CreateUserRequest', Required = $true)]
  [object]$Body
)

# Inline definition
param(
  [OpenApiRequestBody(Description = 'Raw text', ContentType = 'text/plain')]
  [string]$Body
)
```

## `[OpenApiParameter]`

Defines individual parameters if not using a reusable component.

```powershell
[OpenApiParameter(Name = 'id', In = 'path', Required = $true, Type = [long])]
```

or better yet, use the common attributes used by PowerShell for validation:

```powershell
[OpenApiParameter( In = 'path', Required = $true)]
[Parameter(Mandatory)]
[long]$id
```

## Using reusable parameter components in routes

```powershell
function listItems {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/items')]
    param(
        [OpenApiParameterRef(ReferenceId = 'page')]
        [int]$page,

        [OpenApiParameterRef(ReferenceId = 'limit')]
        [int]$limit
    )
}
```

### Path parameter example

```powershell
function getItem {
  [OpenApiPath(HttpVerb = 'get', Pattern = '/items/{id}')]
  param(
    [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true)]
    [int]$id
  )
}
```

## Request body components in routes

```powershell
function createProduct {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/products')]
    [OpenApiResponse(StatusCode = '201')]
    param(
        [OpenApiRequestBody(ContentType = 'application/json')]
        [CreateProductRequest]$Body
    )
}
```

If you cannot or do not want to type the runtime parameter, use a request-body reference:

```powershell
function createProduct {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/products')]
    [OpenApiResponse(StatusCode = '201')]
    param(
        [OpenApiRequestBodyRef(ReferenceId = 'CreateProductRequest', Required = $true)]
        [object]$Body
    )
}
```

## Response components in routes

```powershell
function getProduct {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/products/{id}')]
    [OpenApiResponse(StatusCode = '200', Schema = [ProductSchema], ContentType = ('application/json', 'application/xml'))]
    [OpenApiResponseRef(StatusCode = '404', ReferenceId = 'NotFound')]
    param()
}
```

## Tags, external docs, and hierarchical tags

```powershell
$ordersExternalDocs = New-KrOpenApiExternalDoc -Description 'Order docs' -Url 'https://example.com/orders'

Add-KrOpenApiTag -Name 'operations' -Description 'Common operational endpoints' -Kind 'category'
Add-KrOpenApiTag -Name 'orders' -Description 'Order operations' -Parent 'operations' -Kind 'resource' -ExternalDocs $ordersExternalDocs
Add-KrOpenApiTag -Name 'orders.read' -Description 'Read-only order operations' -Parent 'orders' -Kind 'operation'
```

## HTTP QUERY method (OpenAPI 3.2+)

The HTTP `QUERY` method is a semantically clearer alternative to `GET` for search/filter operations that require a request body.

### Example

```powershell
[OpenApiParameterComponent(In = 'Query', Description = 'Page number')]
[ValidateRange(1, 1000)]
[int]$page = 1

[OpenApiParameterComponent(In = 'Query', Description = 'Page size')]
[ValidateRange(1, 100)]
[int]$pageSize = 25

[OpenApiSchemaComponent()]
class ProductSearchFilters {
    [OpenApiPropertyAttribute(Description = 'Search query', Example = 'laptop')]
    [string]$q

    [OpenApiPropertyAttribute(Description = 'Min price', Example = 500)]
    [double]$minPrice
}

function searchProducts {
    [OpenApiPath(HttpVerb = 'query', Pattern = '/products/search')]
    [OpenApiResponse(StatusCode = '200', Description = 'Paginated results')]
    param(
        [OpenApiParameterRef(ReferenceId = 'page')]
        [int]$page,

        [OpenApiParameterRef(ReferenceId = 'pageSize')]
        [int]$pageSize,

        [OpenApiRequestBody(Description = 'Search filters')]
        [ProductSearchFilters]$filters
    )

    Write-KrResponse -InputObject @{
        page = $page
        pageSize = $pageSize
        total = 42
        items = @()
    } -StatusCode 200
}
```

For OpenAPI 3.0 and 3.1, Kestrun emits the fallback extension `x-oai-additionalOperations.QUERY`.

## RFC 6570 path expressions (OpenAPI 3.2)

In OpenAPI 3.2, path templates are RFC 6570 URI templates.

Supported mapping patterns:

- `{var}` - single path segment
- `{+var}` - reserved expansion, can represent multiple segments
- `{var*}` - explode, can represent multiple segments

### Multi-segment variable mapping

```text
OpenAPI 3.2 template:  /files/{+path}
Kestrun pattern:       /files/{**path}
ASP.NET Core route:    /files/{*path}
```

Kestrun maps the captured ASP.NET Core route values to RFC 6570 variables with rules that keep routing and OpenAPI aligned.
