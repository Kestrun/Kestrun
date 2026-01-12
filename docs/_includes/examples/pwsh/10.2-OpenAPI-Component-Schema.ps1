<#
    Sample: OpenAPI Component Schemas
    Purpose: Demonstrate reusable component schemas for request and response payloads.
    File:    10.2-OpenAPI-Component-Schema.ps1
    Notes:   Shows schema component definition with required fields, property attributes, and content type negotiation.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# --- Logging / Server ---
New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'OpenAPI Component Schema'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Component Schema API' `
    -Version '1.0.0' `
    -Description 'Demonstrates reusable component schemas passed as parameters and returned as responses.'

# =========================================================
#                      COMPONENT SCHEMAS
# =========================================================

# Request schema: User input for creating a user
[OpenApiSchemaComponent(RequiredProperties = ('firstName', 'lastName', 'email'))]
class CreateUserRequest {
    [OpenApiPropertyAttribute(Description = 'First name of the user', Example = 'John')]
    [string]$firstName

    [OpenApiPropertyAttribute(Description = 'Last name of the user', Example = 'Doe')]
    [string]$lastName

    [OpenApiPropertyAttribute(Description = 'Email address', Format = 'email', Example = 'john.doe@example.com')]
    [string]$email

    [OpenApiPropertyAttribute(Description = 'User age', Minimum = 0, Maximum = 150, Example = 30)]
    [int]$age
}

# Response schema: User data returned from server
[OpenApiSchemaComponent(RequiredProperties = ('id', 'firstName', 'lastName', 'email'))]
class UserResponse {
    [OpenApiPropertyAttribute(Description = 'Unique user identifier', Format = 'int64', Example = 1)]
    [long]$id

    [OpenApiPropertyAttribute(Description = 'First name', Example = 'John')]
    [string]$firstName

    [OpenApiPropertyAttribute(Description = 'Last name', Example = 'Doe')]
    [string]$lastName

    [OpenApiPropertyAttribute(Description = 'Email address', Format = 'email', Example = 'john.doe@example.com')]
    [string]$email

    [OpenApiPropertyAttribute(Description = 'User age', Example = 30)]
    [int]$age

    [OpenApiPropertyAttribute(Description = 'ISO 8601 creation timestamp', Format = 'date-time')]
    [string]$createdAt
}

# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

# POST endpoint: Accept CreateUserRequest, return UserResponse
<#
.SYNOPSIS
    Create a new user.
.DESCRIPTION
    Accepts user information and returns the created user with an assigned ID.
.PARAMETER body
    User creation request payload
#>
function createUser {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/users')]
    [OpenApiResponse(StatusCode = '201', Description = 'User created successfully', Schema = [UserResponse], ContentType = ('application/json', 'application/xml', 'application/yaml'))]
    [OpenApiResponse(StatusCode = '400', Description = 'Invalid input')]
    param(
        [OpenApiRequestBody(ContentType = ('application/json', 'application/xml', 'application/yaml', 'application/x-www-form-urlencoded'))]
        [CreateUserRequest]$body
    )

    # Simple validation
    if (-not $body.firstName -or -not $body.lastName -or -not $body.email) {
        Write-KrJsonResponse @{error = 'firstName, lastName, and email are required' } -StatusCode 400
        return
    }

    # Create response
    $response = @{
        id = 1
        firstName = $body.firstName
        lastName = $body.lastName
        email = $body.email
        age = $body.age -as [int]
        createdAt = (Get-Date).ToUniversalTime().ToString('o')
    }

    Write-KrResponse $response -StatusCode 201
}

# GET endpoint: Return a user by ID as UserResponse
<#
.SYNOPSIS
    Get user by ID.
.DESCRIPTION
    Retrieves a user resource by its identifier.
.PARAMETER userId
    The user ID to retrieve
#>
function getUser {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/users/{userId}')]
    [OpenApiResponse(StatusCode = '200', Description = 'User found', Schema = [UserResponse], ContentType = ('application/json', 'application/xml', 'application/yaml'))]
    [OpenApiResponse(StatusCode = '404', Description = 'User not found')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true)]
        [int]$userId
    )

    # Mock user data
    [UserResponse]$response = [UserResponse]::new()
    $response.id = $userId
    $response.firstName = 'John'
    $response.lastName = 'Doe'
    $response.email = 'john.doe@example.com'
    $response.age = 30
    $response.createdAt = (Get-Date).AddDays(-1).ToUniversalTime().ToString('o')

    Write-KrResponse $response -StatusCode 200
}

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute

Build-KrOpenApiDocument
Test-KrOpenApiDocument

# =========================================================
#                      RUN SERVER
# =========================================================


Start-KrServer -CloseLogsOnExit
