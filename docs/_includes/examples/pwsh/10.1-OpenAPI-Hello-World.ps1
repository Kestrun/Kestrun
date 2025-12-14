param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# --- Logging / Server ---

New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

$srv = New-KrServer -Name 'OpenAPI Hello World' -PassThru

Add-KrEndpoint -Port $Port -IPAddress $IPAddress
# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Hello World API' `
    -Version '1.0.0' `
    -Description 'A simple OpenAPI 3.1 example with a single endpoint.'

Add-KrOpenApiContact -Email 'support@example.com'
# Add Server info
Add-KrOpenApiServer -Url "http://$($IPAddress):$Port" -Description 'Local Server'

# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

# Simple GET endpoint with a text response

<#
.SYNOPSIS
    Get greeting message.
.DESCRIPTION
    Returns a simple greeting message.
#>
function getGreeting {
    [OpenApiPath(HttpVerb = 'get' , Pattern = '/greeting' )]
    param()
    Write-KrTextResponse -Text 'Hello, World!' -StatusCode 200
}

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute  # Default pattern '/openapi/{version}/openapi.{format}'

# =========================================================
#                      RUN SERVER
# =========================================================

Start-KrServer -Server $srv -CloseLogsOnExit
