<#
    Sample: OpenAPI Document Information
    Purpose: Demonstrate how to populate document-level metadata including title, version, contact, license, and servers.
    File:    15.8-OpenAPI-Document-Info.ps1
    Notes:   Shows document info setup, server configuration with environment variables, and metadata retrieval via endpoint.
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Configure logging
New-KrLogger |
    Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Information |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# Create server instance
$srv = New-KrServer -Name 'OpenAPI Document Info Demo'

# Configure endpoint
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Set document-level metadata: title, version, description, summary, and terms
Add-KrOpenApiInfo -Title 'Document Info API' -Version '1.0.0' -Description 'Shows how to populate document metadata.' -Summary 'Document metadata demo' -TermsOfService 'https://example.com/terms'

# Add contact and license information
Add-KrOpenApiContact -Name 'API Support' -Email 'support@example.com' -Url 'https://example.com/support'
Add-KrOpenApiLicense -Name 'MIT' -Url 'https://opensource.org/licenses/MIT' -Identifier 'MIT'

# Configure server endpoints with optional environment variable substitution
$serverVars = New-KrOpenApiServerVariable -Name 'env' -Default 'dev' -Enum @('dev', 'staging', 'prod') -Description 'Deployment environment'
Add-KrOpenApiServer -Url 'https://{env}.api.example.com' -Description 'Environment-specific endpoint' -Variables $serverVars
Add-KrOpenApiServer -Url 'https://api.example.com' -Description 'Production'
Add-KrOpenApiServer -Url 'https://staging-api.example.com' -Description 'Staging'
Add-KrOpenApiServer -Url '/' -Description 'Self'

# Add top-level external documentation link
Add-KrOpenApiExternalDoc -Description 'Full API guide' -Url 'https://example.com/api-guide'

# Apply configuration and register documentation routes
Enable-KrConfiguration
Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc
Add-KrOpenApiRoute

<#
.SYNOPSIS
    Get API document info.
.DESCRIPTION
    Returns OpenAPI document metadata as a JSON payload, demonstrating
    access to the configured document information.
#>
function getApiInfo {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/info')]
    [OpenApiResponse(Description = 'Returns document metadata sample', ContentType = 'application/json')]
    param()

    # Retrieve the built OpenAPI document and extract metadata
    $OpenInfo = Get-KrOpenApiDocument
    $info = [ordered]@{
        title = $OpenInfo.info.title
        description = $OpenInfo.info.description
        version = $OpenInfo.info.version
        termsOfService = $OpenInfo.info.termsOfService
        contact = $OpenInfo.info.contact.email
        license = $OpenInfo.info.license.name
    }
    Write-KrJsonResponse -InputObject $info -StatusCode 200
}

# Build and validate the OpenAPI specification
Build-KrOpenApiDocument
Test-KrOpenApiDocument

# Start the server and keep logs open until exit
Start-KrServer -Server $srv -CloseLogsOnExit
