<#
    Sample: OpenAPI Header Components
    Purpose: Demonstrate reusable *response header components* and referencing them from responses.
    File:    10.9-OpenAPI-Component-Header.ps1
    Notes:
      - Defines header components under components/headers via New-KrOpenApiHeader | Add-KrOpenApiComponent
      - Applies them to responses via OpenApiResponseHeaderRef (component $ref or Inline clone)
      - Uses OpenApiResponseHeader for one-off inline response headers
      - Sets headers at runtime via $Context.Response.Headers
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

New-KrServer -Name 'OpenAPI Component Headers'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Component Headers API' `
    -Version '1.0.0' `
    -Description 'Demonstrates reusable response header components (correlation, location, etag, rate-limit, retry-after) and inline headers.'

Add-KrOpenApiTag -Name 'Users' -Description 'User CRUD operations.'
Add-KrOpenApiTag -Name 'Operational' -Description 'Operational behaviors (rate limit / retry).'

# =========================================================
#                      COMPONENT SCHEMAS
# =========================================================

[OpenApiSchemaComponent(Description = 'Create user request', RequiredProperties = ('firstName', 'lastName', 'email'))]
class CreateUserRequest {
    [OpenApiPropertyAttribute(Description = 'First name', Example = 'Jane')]
    [string]$firstName

    [OpenApiPropertyAttribute(Description = 'Last name', Example = 'Doe')]
    [string]$lastName

    [OpenApiPropertyAttribute(Description = 'Email address', Format = 'email', Example = 'jane.doe@example.com')]
    [string]$email
}

[OpenApiSchemaComponent(Description = 'User resource', RequiredProperties = ('id', 'firstName', 'lastName', 'email', 'updatedAt'))]
class UserResponse {
    [OpenApiPropertyAttribute(Description = 'Unique user identifier', Format = 'int64', Example = 1)]
    [long]$id

    [OpenApiPropertyAttribute(Description = 'First name', Example = 'Jane')]
    [string]$firstName

    [OpenApiPropertyAttribute(Description = 'Last name', Example = 'Doe')]
    [string]$lastName

    [OpenApiPropertyAttribute(Description = 'Email address', Format = 'email', Example = 'jane.doe@example.com')]
    [string]$email

    [OpenApiPropertyAttribute(Description = 'ISO 8601 update timestamp', Format = 'date-time')]
    [string]$updatedAt
}

# =========================================================
#                IN-MEMORY STORE (THREAD-SAFE)
# =========================================================

# Variables defined before Enable-KrConfiguration are automatically available to routes.
# Because routes may execute concurrently, shared mutable state must be thread-safe.
$Users = [hashtable]::Synchronized(@{})
$UserCounters = [hashtable]::Synchronized(@{ NextUserId = 0 })
$ThrottleCounters = [hashtable]::Synchronized(@{})  # key: ip string, value: count

# =========================================================
#                 HELPERS (THROTTLING)
# =========================================================

<#
.SYNOPSIS
    Gets a client key for throttling (based on IP address).
.OUTPUTS
    [string] The client key.
#>
function Get-ClientKey {
    $ip = $Context.Connection.RemoteIpAddress
    if ($null -eq $ip) { return 'unknown' }
    return $ip.ToString()
}

<#
.SYNOPSIS
    Determines if the current request should be throttled.
.OUTPUTS
    [bool] True if the request should be throttled; otherwise, false.
#>
function Test-Throttle {
    # Demo throttle: allow first 3 requests per client, then return 429.
    $key = Get-ClientKey

    [System.Threading.Monitor]::Enter($ThrottleCounters.SyncRoot)
    try {
        if (-not $ThrottleCounters.ContainsKey($key)) { $ThrottleCounters[$key] = 0 }
        $ThrottleCounters[$key] = [int]$ThrottleCounters[$key] + 1
        return ([int]$ThrottleCounters[$key] -gt 3)
    } finally {
        [System.Threading.Monitor]::Exit($ThrottleCounters.SyncRoot)
    }
}

<#
.SYNOPSIS
    Sets operational headers for the response.
.PARAMETER Limit
    The rate limit value.
.PARAMETER Remaining
    The remaining requests value.
.PARAMETER ResetSeconds
    The reset time in seconds.
.PARAMETER CorrelationId
    The correlation ID value.
.OUTPUTS
    None
#>
function Add-DemoOperationalHeader {
    param(
        [Parameter(Mandatory)]
        [int]$Limit,
        [Parameter(Mandatory)]
        [int]$Remaining,
        [Parameter(Mandatory)]
        [int]$ResetSeconds,
        [Parameter(Mandatory)]
        [string]$CorrelationId
    )

    $Context.Response.Headers['X-Correlation-Id'] = $CorrelationId
    $Context.Response.Headers['X-RateLimit-Limit'] = "$Limit"
    $Context.Response.Headers['X-RateLimit-Remaining'] = "$Remaining"
    $Context.Response.Headers['X-RateLimit-Reset'] = "$ResetSeconds"
}


# =========================================================
#                 COMPONENT HEADERS (reusable)
# =========================================================

# Correlation id (traceability)
$correlationExamples = @{
    'uuid' = New-KrOpenApiExample -Summary 'Correlation id' -Value '7b2a8e5d-0d7c-4f0a-9b3c-3f9d0b8ad7b1'
}
New-KrOpenApiHeader `
    -Description 'Correlation id for tracing the request across services.' `
    -Schema ([string]) `
    -Required `
    -Examples $correlationExamples |
    Add-KrOpenApiComponent -Name 'X-Correlation-Id'

# Location header for 201 Created
New-KrOpenApiHeader `
    -Description 'Canonical URI of the created resource.' `
    -Schema ([string]) `
    -Required |
    Add-KrOpenApiComponent -Name 'Location'

# ETag header for caching / optimistic concurrency
$etagExamples = @{
    'weak' = New-KrOpenApiExample -Summary 'Weak ETag' -Value 'W/"user-1-v3"'
}
New-KrOpenApiHeader `
    -Description 'Entity tag representing the current version of the resource.' `
    -Schema ([string]) `
    -Examples $etagExamples |
    Add-KrOpenApiComponent -Name 'ETag'

# Simple rate limit headers (demo)
New-KrOpenApiHeader -Description 'Maximum requests allowed in the current window.' -Schema ([int]) |
    Add-KrOpenApiComponent -Name 'X-RateLimit-Limit'

New-KrOpenApiHeader -Description 'Remaining requests in the current window.' -Schema ([int]) `
    -Extensions ([ordered]@{
        'x-kestrun-demo' = [ordered]@{
            exampleRemaining = 1
            computedPer = 'client-ip'
            windowSeconds = 60
        }
    }) |
    Add-KrOpenApiComponent -Name 'X-RateLimit-Remaining'

New-KrOpenApiHeader -Description 'Seconds until the window resets.' -Schema ([int]) `
    -Extensions ([ordered]@{
        'x-kestrun-demo' = [ordered]@{
            resetSeconds = 60
            correlationIdExample = '7b2a8e5d-0d7c-4f0a-9b3c-3f9d0b8ad7b1'
        }
    }) |
    Add-KrOpenApiComponent -Name 'X-RateLimit-Reset'

# Retry-After for 429 Too Many Requests
New-KrOpenApiHeader -Description 'Seconds to wait before retrying the request.' -Schema ([int]) | Add-KrOpenApiComponent -Name 'Retry-After'

# =========================================================
#                 ENABLE SERVER + OPENAPI
# =========================================================
Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

<#
.SYNOPSIS
    Create a new user.
.DESCRIPTION
    Accepts user information and returns the created user with an assigned ID.
.PARAMETER body
    User creation request payload
.NOTES
    Returns 201 Created with UserResponse schema and a custom header.
    POST endpoint: Accept CreateUserRequest, return UserResponse
#>
function createUser {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/users', Tags = 'Users')]
    [OpenApiResponse(StatusCode = '201', Description = 'Created', Schema = [UserResponse], ContentType = ('application/json'))]
    [OpenApiResponseHeaderRef(StatusCode = '201', Key = 'X-Correlation-Id', ReferenceId = 'X-Correlation-Id')]
    [OpenApiResponseHeaderRef(StatusCode = '201', Key = 'Location', ReferenceId = 'Location')]
    [OpenApiResponseHeaderRef(StatusCode = '201', Key = 'ETag', ReferenceId = 'ETag')]
    [OpenApiResponseHeaderRef(StatusCode = '201', Key = 'X-RateLimit-Limit', ReferenceId = 'X-RateLimit-Limit')]
    [OpenApiResponseHeaderRef(StatusCode = '201', Key = 'X-RateLimit-Remaining', ReferenceId = 'X-RateLimit-Remaining')]
    [OpenApiResponseHeaderRef(StatusCode = '201', Key = 'X-RateLimit-Reset', ReferenceId = 'X-RateLimit-Reset')]

    [OpenApiResponse(StatusCode = '400', Description = 'Invalid input')]
    [OpenApiResponseHeader(StatusCode = '400', Key = 'X-Error-Code', Description = 'Machine-readable error code.', Schema = ([string]))]

    [OpenApiResponse(StatusCode = '429', Description = 'Too many requests')]
    [OpenApiResponseHeaderRef(StatusCode = '429', Key = 'Retry-After', ReferenceId = 'Retry-After')]
    param(
        [OpenApiRequestBody(ContentType = ('application/json', 'application/xml', 'application/yaml', 'application/x-www-form-urlencoded'))]
        [CreateUserRequest]$body
    )

    $correlationId = [Guid]::NewGuid().ToString()
    Add-DemoOperationalHeader -Limit 3 -Remaining 1 -ResetSeconds 60 -CorrelationId $correlationId

    if (Test-Throttle) {
        $Context.Response.Headers['Retry-After'] = '30'
        Write-KrJsonResponse @{ error = 'Too many requests'; retryAfterSeconds = 30 } -StatusCode 429
        return
    }

    # Simple validation
    if (-not $body.firstName -or -not $body.lastName -or -not $body.email) {
        $Context.Response.Headers['X-Error-Code'] = 'VALIDATION_FAILED'
        Write-KrJsonResponse @{ error = 'firstName, lastName, and email are required' } -StatusCode 400
        return
    }

    $newId = Update-KrSynchronizedCounter -Table $UserCounters -Key 'NextUserId' -By 1

    $user = [UserResponse]::new()
    $user.id = [long]$newId
    $user.firstName = $body.firstName
    $user.lastName = $body.lastName
    $user.email = $body.email
    $user.updatedAt = (Get-Date).ToUniversalTime().ToString('o')

    [System.Threading.Monitor]::Enter($Users.SyncRoot)
    try {
        $Users[[string]$newId] = $user
    } finally {
        [System.Threading.Monitor]::Exit($Users.SyncRoot)
    }

    $Context.Response.Headers['Location'] = "/users/$newId"
    $Context.Response.Headers['ETag'] = "W/`"user-$newId-v1`""

    Write-KrResponse $user -StatusCode 201
}

<#
.SYNOPSIS
    Get user by ID.
.DESCRIPTION
    Retrieves a user resource by its identifier.
.PARAMETER userId
    The user ID to retrieve
#>
function getUser {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/users/{userId}', Tags = 'Users')]
    [OpenApiResponse(StatusCode = '200', Description = 'Found', Schema = [UserResponse], ContentType = ('application/json'))]
    [OpenApiResponseHeaderRef(StatusCode = '200', Key = 'X-Correlation-Id', ReferenceId = 'X-Correlation-Id')]
    [OpenApiResponseHeaderRef(StatusCode = '200', Key = 'ETag', ReferenceId = 'ETag')]
    [OpenApiResponseHeaderRef(StatusCode = '200', Key = 'X-RateLimit-Limit', ReferenceId = 'X-RateLimit-Limit')]
    [OpenApiResponseHeaderRef(StatusCode = '200', Key = 'X-RateLimit-Remaining', ReferenceId = 'X-RateLimit-Remaining')]
    [OpenApiResponseHeaderRef(StatusCode = '200', Key = 'X-RateLimit-Reset', ReferenceId = 'X-RateLimit-Reset')]

    [OpenApiResponse(StatusCode = '404', Description = 'User not found')]
    [OpenApiResponseHeader(StatusCode = '404', Key = 'X-Error-Code', Description = 'Machine-readable error code.', Schema = ([string]))]

    [OpenApiResponse(StatusCode = '429', Description = 'Too many requests')]
    [OpenApiResponseHeaderRef(StatusCode = '429', Key = 'Retry-After', ReferenceId = 'Retry-After')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true)]
        [int]$userId
    )

    $correlationId = [Guid]::NewGuid().ToString()
    Add-DemoOperationalHeader -Limit 3 -Remaining 1 -ResetSeconds 60 -CorrelationId $correlationId

    if (Test-Throttle) {
        $Context.Response.Headers['Retry-After'] = '30'
        Write-KrJsonResponse @{ error = 'Too many requests'; retryAfterSeconds = 30 } -StatusCode 429
        return
    }

    $found = $null
    [System.Threading.Monitor]::Enter($Users.SyncRoot)
    try {
        $found = $Users[[string]$userId]
    } finally {
        [System.Threading.Monitor]::Exit($Users.SyncRoot)
    }

    if ($null -eq $found) {
        $Context.Response.Headers['X-Error-Code'] = 'USER_NOT_FOUND'
        Write-KrJsonResponse @{ error = "User '$userId' not found" } -StatusCode 404
        return
    }

    $v = [int]([DateTimeOffset]::UtcNow.ToUnixTimeSeconds() % 10)
    $Context.Response.Headers['ETag'] = "W/`"user-$userId-v$v`""

    Write-KrResponse $found -StatusCode 200
}

<#
.SYNOPSIS
    Delete user by ID.
.DESCRIPTION
    Deletes a user resource by its identifier.
.PARAMETER userId
    The user ID to delete
#>
function deleteUser {
    [OpenApiPath(HttpVerb = 'delete', Pattern = '/users/{userId}', Tags = 'Users')]
    [OpenApiResponse(StatusCode = '204', Description = 'Deleted')]
    [OpenApiResponseHeaderRef(StatusCode = '204', Key = 'X-Correlation-Id', ReferenceId = 'X-Correlation-Id')]
    [OpenApiResponseHeaderRef(StatusCode = '204', Key = 'X-RateLimit-Limit', ReferenceId = 'X-RateLimit-Limit')]
    [OpenApiResponseHeaderRef(StatusCode = '204', Key = 'X-RateLimit-Remaining', ReferenceId = 'X-RateLimit-Remaining')]
    [OpenApiResponseHeaderRef(StatusCode = '204', Key = 'X-RateLimit-Reset', ReferenceId = 'X-RateLimit-Reset')]

    [OpenApiResponse(StatusCode = '404', Description = 'User not found')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true)]
        [int]$userId
    )

    $correlationId = [Guid]::NewGuid().ToString()
    Add-DemoOperationalHeader -Limit 3 -Remaining 1 -ResetSeconds 60 -CorrelationId $correlationId

    $removed = $false
    [System.Threading.Monitor]::Enter($Users.SyncRoot)
    try {
        if ($Users.ContainsKey([string]$userId)) {
            $Users.Remove([string]$userId) | Out-Null
            $removed = $true
        }
    } finally {
        [System.Threading.Monitor]::Exit($Users.SyncRoot)
    }

    if (-not $removed) {
        Write-KrJsonResponse @{ error = "User '$userId' not found" } -StatusCode 404
        return
    }

    Write-KrTextResponse '' -StatusCode 204
}

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute

Build-KrOpenApiDocument
# Test and log OpenAPI document validation result
if (Test-KrOpenApiDocument) {
    Write-KrLog -Level Information -Message 'OpenAPI document built and validated successfully.'
} else {
    Write-KrLog -Level Error -Message 'OpenAPI document validation failed.'
}

# =========================================================
#                      RUN SERVER
# =========================================================


Start-KrServer -CloseLogsOnExit
