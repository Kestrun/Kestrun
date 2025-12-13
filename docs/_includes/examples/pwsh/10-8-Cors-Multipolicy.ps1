<#
    Sample:  OpenAPI 3.1.0 sample script for Cors policies and multiple content types in a Muststore application.
    Purpose: To demonstrate how to configure CORS policies and handle multiple content types in a Muststore application.
    File:    10-8-Cors-MultiPolicy.ps1
    Notes:   This script demonstrates how to configure CORS policies and handle multiple content types in a Muststore application.
             It includes sample functions for listing products, getting product details by ID, retrieving partner inventory levels,
             creating a new order, deleting an order, and a UI server for testing CORS behaviors.
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback,
    [string]$PartnerOrigin = 'http://127.0.0.1:5000'
)

if (-not (Get-Module Kestrun)) { Import-Module Kestrun }
# ---------------------------------------------------------------------------
# Create servers (adjust these to your actual Kestrun host factory cmdlets)
# ---------------------------------------------------------------------------

New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault
New-KrServer -Name 'Swagger Petstore - OpenAPI 3.1'
Add-KrEndpoint -Port $Port -IPAddress $IPAddress



$uiOrigin = "http://$($IPAddress.ToString()):$UiPort"
# Expose OpenAPI (adjust to your OpenAPI enablement)
Add-KrOpenApiInfo -Title 'CORS Multi-Policy Sample' -Version '1.0.0' `
    -Description 'Sample: Multiple CORS policies + multiple APIs + intentional browser CORS failures.'

Add-KrOpenApiServer -Url '/' -Description 'Local Kestrun Server'
Add-KrOpenApiTag -Name 'catalog' -Description 'Product catalog operations'
Add-KrOpenApiTag -Name 'partner' -Description 'Partner-specific operations'
Add-KrOpenApiTag -Name 'orders' -Description 'Order management operations'

# ---------------------------------------------------------------------------
# In-memory data
# ---------------------------------------------------------------------------
$Products = @(
    [ordered]@{ id = 1; name = 'Red Ball'; price = 9.99 }
    [ordered]@{ id = 2; name = 'Blue Bone'; price = 12.49 }
    [ordered]@{ id = 3; name = 'Green Mouse'; price = 4.95 }
)

# ===========================================================================
# 1) CORS policies (API server)
# ===========================================================================
# Default policy: allow the UI origin for GET only (keeps "no Cors metadata" interesting)
New-KrCorsPolicyBuilder |
    Set-KrCorsOrigin -Origins $uiOrigin |
    Set-KrCorsMethod -Methods GET |
    Set-KrCorsHeader -Any |
    Add-KrCorsPolicy -Default

# PublicRead: explicitly allow UI origin GET (catalog operations)
New-KrCorsPolicyBuilder |
    Set-KrCorsOrigin -Origins $uiOrigin |
    Set-KrCorsMethod -Methods GET |
    Set-KrCorsHeader -Any |
    Add-KrCorsPolicy -Name 'PublicRead'

# PartnerOnly: allow ONLY partner origin (UI origin will fail)
New-KrCorsPolicyBuilder |
    Set-KrCorsOrigin -Origins $PartnerOrigin |
    Set-KrCorsMethod -Methods GET |
    Set-KrCorsHeader -Any |
    Add-KrCorsPolicy -Name 'PartnerOnly'

# AdminWrite: allow UI origin, but only for write methods; triggers preflight from the UI
New-KrCorsPolicyBuilder |
    Set-KrCorsOrigin -Origins $uiOrigin |
    Set-KrCorsMethod -Methods POST, PUT, DELETE |
    Set-KrCorsHeader -Any |
    Add-KrCorsPolicy -Name 'AdminWrite'


[OpenApiSchemaComponent()]
class Order {
    [Guid]$orderId
    [int]$productId
    [DateTime]$created
}

Enable-KrConfiguration

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================
Add-KrOpenApiRoute  # Default Pattern '/openapi/{version}/openapi.{format}'

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc
Add-KrApiDocumentationRoute -DocumentType Scalar
Add-KrApiDocumentationRoute -DocumentType Rapidoc
Add-KrApiDocumentationRoute -DocumentType Elements

# ===========================================================================
# 2) API operations (operationId == function name)
#    Summary/description/params come from comment-based help.
# ===========================================================================

<#
.SYNOPSIS
    List available products.
.DESCRIPTION
    Returns the public product catalog.
    This operation is accessible cross-origin from approved UI origins.
#>
function listProducts {
    [OpenApiPath(HttpVerb = 'Get', Pattern = '/products', Tags = 'catalog', CorsPolicy = 'PublicRead')]
    [OpenApiResponse(StatusCode = '200', Description = 'Product list', ContentType = 'application/json')]
    param()
    Write-KrLog -Level Information -Message 'GET /products'
    Write-KrJsonResponse -StatusCode 200 -InputObject $Products
}

<#
.SYNOPSIS
    Get product details by ID.
.DESCRIPTION
    Returns details for a single product.
.PARAMETER productId
    The unique identifier of the product.
#>
function getProductById {
    [OpenApiPath(HttpVerb = 'Get', Pattern = '/products/{productId}', Tags = 'catalog', CorsPolicy = 'PublicRead')]
    [OpenApiResponse(StatusCode = '200', Description = 'Product details', ContentType = 'application/json')]
    [OpenApiResponse(StatusCode = '404', Description = 'Not found')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path)]
        [Parameter(Mandatory)]
        [int]$productId
    )
    Write-KrLog -Level Information -Message "GET /products/$productId"
    Expand-KrObject -InputObject $Products
    $item = $Products | Where-Object id -EQ $productId | Select-Object -First 1
    if (-not $item) { return Write-KrStatusResponse -StatusCode 404 }

    Write-KrJsonResponse -StatusCode 200 -InputObject $item
}

<#
.SYNOPSIS
    Retrieve partner inventory levels.
.DESCRIPTION
    Returns inventory information reserved for business partners.
    Requests from the UI origin will fail due to the PartnerOnly CORS policy.
#>
function getPartnerInventory {
    [OpenApiPath(HttpVerb = 'Get', Pattern = '/partner/inventory', Tags = 'partner', CorsPolicy = 'PartnerOnly')]
    [OpenApiResponse(StatusCode = '200', Description = 'Partner inventory list', ContentType = 'application/json')]
    param()
    Write-KrLog -Level Information -Message 'GET /partner/inventory'
    Write-KrJsonResponse -StatusCode 200 -InputObject @(
        [pscustomobject]@{ sku = 'RB-001'; qty = 100 }
        [pscustomobject]@{ sku = 'BB-002'; qty = 50 }
    )
}

<#
.SYNOPSIS
    Create a new order.
.DESCRIPTION
    Creates a new order for a given product ID.
    This operation requires a trusted UI origin and will trigger a CORS preflight request.
.PARAMETER Body
    JSON payload containing:
      - productId: integer
#>
function createOrder {
    [OpenApiPath(HttpVerb = 'Post', Pattern = '/orders', Tags = 'orders', CorsPolicy = 'AdminWrite')]
    [OpenApiResponse(StatusCode = '201', Description = 'Order created', SchemaRef = 'Order', ContentType = 'application/json')]
    [OpenApiResponse(StatusCode = '400', Description = 'Invalid request')]
    param(
        [OpenApiRequestBody(Required = $true, ContentType = ('application/json'))]
        [Int]$productId
    )
    Write-KrLog -Level Information -Message "POST /orders with productId $productId"
    if ( $productId -le 0) { Write-KrStatusResponse -StatusCode 400 }else {

        $created = [Order]@{
            orderId = [guid]::NewGuid()
            productId = [int]$productId
            created = Get-Date
        }

        Write-KrJsonResponse -StatusCode 201 -InputObject $created
    }
}

<#
.SYNOPSIS
    Delete an order.
.DESCRIPTION
    Deletes an existing order by ID.
    This operation is restricted by the AdminWrite CORS policy.
.PARAMETER orderId
    The unique identifier of the order.
#>
function deleteOrder {
    [OpenApiPath(HttpVerb = 'Delete', Pattern = '/orders/{orderId}', Tags = 'orders', CorsPolicy = 'AdminWrite')]

    [OpenApiResponse(StatusCode = '204', Description = 'Deleted')]
    param(
        [OpenApiParameter(Name = 'orderId', In = 'path', Required = $true)]
        [Guid]$orderId = 'f91aa45e-b733-49ed-a379-f79ea210fbd4'  # dummy value
    )
    Write-KrLog -Level Information -Message "DELETE /orders/$orderId"
    Write-KrStatusResponse -StatusCode 204
}

<#
.SYNOPSIS
    Endpoint without explicit CORS association.
.DESCRIPTION
    Demonstrates behavior when an operation has no Cors='...' metadata.
    The effective behavior depends on the default CORS policy configured on the server.
#>
function getNoCorsInfo {
    [OpenApiPath(HttpVerb = 'Get', Pattern = '/nocors', Tags = 'misc')]
    [OpenApiResponse(StatusCode = '200', Description = 'No CORS metadata', ContentType = 'application/json')]
    param()

    Write-KrJsonResponse -StatusCode 200 -InputObject @{
        ok = $true
        note = 'No Cors property was provided for this operation'
    }
}




# ===========================================================================
# 3) UI server: browser test page
# ===========================================================================

<#
.SYNOPSIS
    UI page for testing CORS behaviors.
.DESCRIPTION
    Serves an HTML page that performs cross-origin fetch calls to the API server.
#>
function corsTestUi {
    param()

    $indexHtml = @"
<!doctype html>
<html>
<head>
  <meta charset="utf-8"/>
  <title>Kestrun CORS Multi-Policy Demo</title>
  <style>
    body { font-family: sans-serif; margin: 20px; }
    button { margin: 4px; }
    pre { background: #111; color: #0f0; padding: 10px; overflow:auto; }
    code { background:#eee; padding:2px 4px; }
  </style>
</head>
<body>
  <h1>Kestrun CORS Multi-Policy Demo</h1>
  <p><b>UI Origin:</b> $uiOrigin</p>
  <p><b>API Origin:</b> http://127.0.0.1:$ApiPort</p>

  <div>
    <button onclick="callApi('GET', '/products')">GET /products (should succeed)</button>
    <button onclick="callApi('GET', '/partner/inventory')">GET /partner/inventory (should FAIL CORS)</button>
    <button onclick="callApi('POST', '/orders', { productId: 1 })">POST /orders (should succeed; preflight)</button>
    <button onclick="callApi('POST', '/orders', { foo: 'bar' })">POST /orders invalid body (CORS ok; API 400)</button>
    <button onclick="callApi('GET', '/nocors')">GET /nocors (depends on default policy)</button>
    <button onclick="openOpenApi()">OpenAPI JSON</button>
  </div>

  <h2>Result</h2>
  <pre id="out"></pre>

<script>
const apiBase = 'http://127.0.0.1:$ApiPort';

function log(msg) {
  document.getElementById('out').textContent = msg;
}

async function callApi(method, path, body) {
  const url = apiBase + path;

  try {
    const res = await fetch(url, {
      method,
      mode: 'cors',
      headers: body ? { 'content-type': 'application/json' } : undefined,
      body: body ? JSON.stringify(body) : undefined
    });

    const text = await res.text();
    log('HTTP ' + res.status + '\\n\\n' + text);
  } catch (e) {
    // CORS failures typically surface as a fetch TypeError/network error in the browser.
    log('FETCH ERROR (often CORS):\\n' + e);
  }
}

function openOpenApi() {
  window.open(apiBase + '/openapi/v1.json', '_blank');
}
</script>
</body>
</html>
"@

    Write-KrHtmlResponse -StatusCode 200 -Template $indexHtml
}



# ===========================================================================
# 4) Start server
# ===========================================================================
Start-KrServer
