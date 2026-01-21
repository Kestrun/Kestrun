<#
    Sample: OpenAPI 3.2 RFC 6570 Variable Mapping
    Purpose: Demonstrate RFC 6570 URI Template variable mapping from ASP.NET Core route values
    File:    10.23-OpenAPI-VariableMapping.ps1
    Notes:   - Shows how route parameters are extracted and mapped to RFC 6570 variables
             - Demonstrates simple parameters, reserved operators, and regex constraints
             - Uses Rfc6570VariableMapper helper for callback/link template expansion
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

if (-not (Get-Module Kestrun)) { Import-Module Kestrun }

# --- Logging / Server ---
New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'RFC 6570 Variable Mapping'
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'RFC 6570 Variable Mapping API' `
    -Version '1.0.0' `
    -Description 'Demonstrates RFC 6570 URI Template variable mapping for OpenAPI 3.2 path expressions with regex constraints and reserved operators.'

Add-KrOpenApiTag -Name 'Users' -Description 'User management endpoints'
Add-KrOpenApiTag -Name 'Files' -Description 'File access endpoints'
Add-KrOpenApiTag -Name 'API' -Description 'Versioned API endpoints'

# =========================================================
#                     COMPONENT SCHEMAS
# =========================================================

[OpenApiSchemaComponent(Description = 'User information', RequiredProperties = ('id', 'username'))]
class User {
    [OpenApiProperty(Description = 'User ID', Example = 42)]
    [int]$id

    [OpenApiProperty(Description = 'Username', Example = 'johndoe')]
    [string]$username

    [OpenApiProperty(Description = 'Email address', Format = 'email', Example = 'john@example.com')]
    [string]$email
}

[OpenApiSchemaComponent(Description = 'File metadata')]
class FileInfo {
    [OpenApiProperty(Description = 'File path', Example = 'documents/report.pdf')]
    [string]$path

    [OpenApiProperty(Description = 'File size in bytes', Example = 1024)]
    [long]$size

    [OpenApiProperty(Description = 'Content type', Example = 'application/pdf')]
    [string]$contentType
}

[OpenApiSchemaComponent(Description = 'Variable mapping result')]
class VariableMapping {
    [OpenApiProperty(Description = 'RFC 6570 template used', Example = '/api/v{version:[0-9]+}/users/{userId}')]
    [string]$template

    [OpenApiProperty(Description = 'Extracted variables', Example = @{version = '1'; userId = '42'})]
    [hashtable]$variables
}

# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger -OpenApiEndpoint "/openapi/v3.2/openapi.yaml"
Add-KrApiDocumentationRoute -DocumentType Redoc -OpenApiEndpoint "/openapi/v3.2/openapi.yaml"

# =========================================================
#       SIMPLE PARAMETER EXTRACTION
# =========================================================

function getUser {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/users/{userId}', Tags = 'Users')]
    [OpenApiResponse(StatusCode = '200', Description = 'User found', Schema = [User])]
    [OpenApiResponse(StatusCode = '404', Description = 'User not found')]
    param()

    $userId = Get-KrRequestRouteParam -Name 'userId'
    
    Write-KrJsonResponse @{
        id = [int]$userId
        username = "user$userId"
        email = "user${userId}@example.com"
    } -StatusCode 200
}

# =========================================================
#       REGEX CONSTRAINT PARAMETERS
# =========================================================

function getApiUser {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/api/v{version:[0-9]+}/users/{userId:[0-9]+}', Tags = 'API')]
    [OpenApiResponse(StatusCode = '200', Description = 'User found with version info', Schema = [User])]
    param()

    $version = Get-KrRequestRouteParam -Name 'version'
    $userId = Get-KrRequestRouteParam -Name 'userId'
    
    Write-KrJsonResponse @{
        id = [int]$userId
        username = "user$userId"
        email = "user${userId}@example.com"
        apiVersion = "v$version"
    } -StatusCode 200
}

# =========================================================
#       RESERVED OPERATOR (MULTI-SEGMENT)
# =========================================================

function getFile {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/files/{+path}', Tags = 'Files')]
    [OpenApiResponse(StatusCode = '200', Description = 'File metadata', Schema = [FileInfo])]
    [OpenApiResponse(StatusCode = '404', Description = 'File not found')]
    param()

    $filePath = Get-KrRequestRouteParam -Name 'path'
    
    Write-KrJsonResponse @{
        path = $filePath
        size = 1024
        contentType = 'application/octet-stream'
    } -StatusCode 200
}

# =========================================================
#       VARIABLE MAPPING INTROSPECTION
# =========================================================

function getVariableMapping {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/mapping/{template}/{id:[0-9]+}', Tags = 'API')]
    [OpenApiResponse(StatusCode = '200', Description = 'Variable mapping details', Schema = [VariableMapping])]
    param()

    $template = Get-KrRequestRouteParam -Name 'template'
    $id = Get-KrRequestRouteParam -Name 'id'
    
    # Demonstrate variable extraction using the current route pattern
    $routePattern = '/mapping/{template}/{id:[0-9]+}'
    
    Write-KrJsonResponse @{
        template = $routePattern
        variables = @{
            template = $template
            id = $id
        }
        extractedFrom = 'ASP.NET Core RouteValues'
        rfc6570Compliant = $true
    } -StatusCode 200
}

# =========================================================
#                     BUILD & START
# =========================================================

Build-KrOpenApiDocument -Version 'v3.2'
Start-KrServer
