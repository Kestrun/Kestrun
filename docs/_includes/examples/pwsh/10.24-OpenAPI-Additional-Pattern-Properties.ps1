<#
    Sample: OpenAPI AdditionalProperties + PatternProperties
    Purpose: Demonstrate schema components that allow arbitrary keys and pattern-constrained keys.
    File:    10.24-OpenAPI-Additional-Pattern-Properties.ps1
    Notes:
            - Shows AdditionalProperties with a typed value schema
            - Shows PatternProperties with regex-constrained keys
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# --- Logging / Server ---
New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'OpenAPI AdditionalProperties + PatternProperties'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Additional/Pattern Properties API' `
    -Version '1.0.0' `
    -Description 'Demonstrates schema components with AdditionalProperties and PatternProperties.'

# =========================================================
#                      COMPONENT SCHEMAS
# =========================================================

# Inventory counts keyed by dynamic status names (pattern enforced)
[OpenApiSchemaComponent(Description = 'Inventory counts by status key.')]
[OpenApiPatternProperties(KeyPattern = '^[a-z][a-z0-9_]*$', SchemaType = [int])]
class InventoryCounts {}

# Feature flags keyed by arbitrary name (additional properties typed as boolean)
[OpenApiSchemaComponent(
    Description = 'Feature flags keyed by name.',
    AdditionalPropertiesAllowed = $true,
    AdditionalProperties = [OpenApiBoolean]
)]
class FeatureFlags {}

# Request combining both kinds of dynamic maps
[OpenApiSchemaComponent(
    Description = 'Catalog update request.',
    AdditionalPropertiesAllowed = $true,
    AdditionalProperties = [OpenApiString]
)]
class CatalogUpdateRequest {
    [OpenApiProperty(Description = 'Item identifier.', Example = 'SKU-1001')]
    [string]$itemId

    [OpenApiProperty(Description = 'Inventory counts by status key.')]
    [InventoryCounts]$inventory

    [OpenApiProperty(Description = 'Feature flags for the item.')]
    [FeatureFlags]$flags
}

# Response echoes the update
[OpenApiSchemaComponent(Description = 'Catalog update response.')]
class CatalogUpdateResponse {
    [OpenApiProperty(Description = 'Item identifier.', Example = 'SKU-1001')]
    [string]$itemId

    [OpenApiProperty(Description = 'Inventory snapshot.')]
    [InventoryCounts]$inventory

    [OpenApiProperty(Description = 'Feature flags snapshot.')]
    [FeatureFlags]$flags

    [OpenApiProperty(Description = 'Server timestamp (RFC 3339).', Example = '2026-02-03T12:34:56.789Z')]
    [OpenApiDateTime]$updatedAt
}

# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

<#
.SYNOPSIS
    Get sample inventory counts.
.DESCRIPTION
    Returns an InventoryCounts object with pattern-constrained keys.
#>
function getInventoryCounts {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/inventory/counts')]
    [OpenApiResponse(StatusCode = '200', Description = 'Inventory counts', Schema = [InventoryCounts], ContentType = 'application/json')]
    param()

    $counts = [InventoryCounts]@{
        AdditionalProperties = @{ available = 120; reserved = 8; backorder = 3 }
    }

    Write-KrResponse $counts.AdditionalProperties -StatusCode 200
}

<#
.SYNOPSIS
    Get sample feature flags.
.DESCRIPTION
    Returns a FeatureFlags object backed by AdditionalProperties.
#>
function getFeatureFlags {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/features/flags')]
    [OpenApiResponse(StatusCode = '200', Description = 'Feature flags', Schema = [FeatureFlags], ContentType = 'application/json')]
    param()

    $flags = [FeatureFlags]@{
        AdditionalProperties = @{ betaPricing = $true; allowBackorder = $false }
    }

    Write-KrResponse $flags.AdditionalProperties -StatusCode 200
}

<#
.SYNOPSIS
    Update catalog data.
.DESCRIPTION
    Accepts dynamic inventory counts and feature flags, then echoes the update.
.PARAMETER body
    Catalog update request payload.
#>
function updateCatalog {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/catalog/update')]
    [OpenApiResponse(StatusCode = '200', Description = 'Updated catalog item', Schema = [CatalogUpdateResponse], ContentType = 'application/json')]
    param(
        [OpenApiRequestBody(Description = 'Catalog update request.', Required = $true, ContentType = 'application/json')]
        [CatalogUpdateRequest]$body
    )

    $response = [CatalogUpdateResponse]@{
        itemId = $body.itemId
        inventory = $body.inventory
        flags = $body.flags
        updatedAt = (Get-Date).ToUniversalTime().ToString('o')
    }

    $payload = @{
        itemId = $response.itemId
        inventory = $response.inventory.AdditionalProperties
        flags = $response.flags.AdditionalProperties
        updatedAt = $response.updatedAt
    }

    Write-KrResponse $payload -StatusCode 200
}

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute

Build-KrOpenApiDocument
if (Test-KrOpenApiDocument) {
    Write-KrLog -Level Information -Message 'OpenAPI document built and validated successfully.'
} else {
    Write-KrLog -Level Error -Message 'OpenAPI document validation failed.'
}

# =========================================================
#                      RUN SERVER
# =========================================================

Start-KrServer -CloseLogsOnExit
