<#
    Sample: OpenAPI Link Components
    Purpose: Demonstrate reusable OpenAPI link components and referencing them from CRUD responses.
    File:    10.10-OpenAPI-Component-Link.ps1
    Notes:   - Defines link components (components/links) via cmdlets.
             - Applies them via OpenApiResponseLinkRef on create/get/update operations.
             - Variables defined before Enable-KrConfiguration are available in route runspaces;
               shared mutable state must be thread-safe.
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

New-KrServer -Name 'OpenAPI Component Links'
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Component Links API' `
    -Version '1.0.0' `
    -Description 'Demonstrates reusable OpenAPI link components and applying them via attributes.'

Add-KrOpenApiTag -Name 'Operations' -Description 'Operational endpoints.'
Add-KrOpenApiTag -Name 'Users' -Description 'User-related endpoints.'

# =========================================================
#                     LINK COMPONENTS
# =========================================================

# Link component: use id from response body to fetch the user resource.
New-KrOpenApiLink -OperationId 'getUser' -Description 'Get the created/returned user.' `
    -Parameters @{ userId = '$response.body#/id' } `
    -Extensions ([ordered]@{
        'x-kestrun-demo' = [ordered]@{
            relation = 'get'
            targetOperation = 'getUser'
            parameterSource = '$response.body#/id'
            note = 'Demonstrates runtime expressions inside reusable link components.'
        }
    }) |
    Add-KrOpenApiComponent -Name 'GetUserLink'

# Link component: use id from response body and pass the nested user object as request body.
New-KrOpenApiLink -OperationId 'updateUser' -Description 'Update the created/returned user.' `
    -Parameters @{ userId = '$response.body#/id' } `
    -RequestBody '$response.body#/user' `
    -Extensions ([ordered]@{
        'x-kestrun-demo' = [ordered]@{
            relation = 'update'
            targetOperation = 'updateUser'
            parameterSource = '$response.body#/id'
            requestBodySource = '$response.body#/user'
            note = 'Shows requestBody mapping via runtime expression.'
        }
    }) |
    Add-KrOpenApiComponent -Name 'UpdateUserLink'

# Link component: use id from response body to delete the user resource.
New-KrOpenApiLink -OperationId 'deleteUser' -Description 'Delete the created/returned user.' `
    -Parameters @{ userId = '$response.body#/id' } `
    -Extensions ([ordered]@{
        'x-kestrun-demo' = [ordered]@{
            relation = 'delete'
            targetOperation = 'deleteUser'
            parameterSource = '$response.body#/id'
            note = 'Links let clients discover follow-up operations after a response.'
        }
    }) |
    Add-KrOpenApiComponent -Name 'DeleteUserLink'

# =========================================================
#                      COMPONENT SCHEMAS
# =========================================================

[OpenApiSchemaComponent(Description = 'User payload (create/update).', RequiredProperties = ('firstName', 'lastName', 'email'))]
class UserPayload {
    [OpenApiProperty(Description = 'First name', Example = 'John')]
    [string]$firstName

    [OpenApiProperty(Description = 'Last name', Example = 'Doe')]
    [string]$lastName

    [OpenApiProperty(Format = 'email', Description = 'Email address', Example = 'john.doe@example.com')]
    [string]$email
}

[OpenApiSchemaComponent(
    Description = 'Response payload for user operations. Contains an id and nested user object for link mapping.',
    RequiredProperties = ('id', 'user')
)]
class UserResourceResponse {
    [OpenApiProperty(Format = 'int32', Description = 'User identifier.', Example = 123)]
    [int]$id

    [OpenApiProperty(Description = 'User payload.')]
    [UserPayload]$user
}

# =========================================================
#                         EXAMPLES
# =========================================================

New-KrOpenApiExample -Summary 'User resource response' -Value ([ordered]@{
        id = 123
        user = [ordered]@{ firstName = 'John'; lastName = 'Doe'; email = 'john.doe@example.com' }
    }) | Add-KrOpenApiComponent -Name 'UserResourceResponseExample'

# =========================================================
#                IN-MEMORY STORE (THREAD-SAFE)
# =========================================================

# Variables defined before Enable-KrConfiguration are automatically available to routes.
# Because routes may execute concurrently, shared mutable state must be thread-safe.
$Users = [hashtable]::Synchronized(@{})
$UserCounters = [hashtable]::Synchronized(@{ NextUserId = 0 })

# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration
Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

function createUser {
    <#
    .SYNOPSIS
        Create a user.
    .DESCRIPTION
        Returns an id + nested user object so response links can map:
          - $response.body#/id
          - $response.body#/user
    #>
    [OpenApiPath(HttpVerb = 'post', Pattern = '/users', Tags = 'Users')]
    [OpenApiResponse(StatusCode = '201', Schema = [UserResourceResponse], Description = 'Created', ContentType = ('application/json', 'application/xml', 'application/yaml'))]
    [OpenApiResponseExampleRef(StatusCode = '201', Key = 'default_example', ReferenceId = 'UserResourceResponseExample')]
    [OpenApiResponseLinkRef(StatusCode = '201', Key = 'get', ReferenceId = 'GetUserLink')]
    [OpenApiResponseLinkRef(StatusCode = '201', Key = 'update', ReferenceId = 'UpdateUserLink')]
    [OpenApiResponseLinkRef(StatusCode = '201', Key = 'delete', ReferenceId = 'DeleteUserLink')]
    [OpenApiResponse(StatusCode = '400', Description = 'Invalid input')]
    param(
        [OpenApiRequestBody(Required = $true, ContentType = ('application/json', 'application/xml', 'application/yaml'))]
        [UserPayload]$Body
    )
    Expand-KrObject -InputObject $Body -Label 'Creating User'
    if (-not $Body.firstName -or -not $Body.lastName -or -not $Body.email) {
        Write-KrJsonResponse @{ error = 'firstName, lastName, and email are required' } -StatusCode 400
        return
    }

    $newId = Update-KrSynchronizedCounter -Table $UserCounters -Key 'NextUserId' -By 1
    $user = [ordered]@{ firstName = $Body.firstName; lastName = $Body.lastName; email = $Body.email }

    [System.Threading.Monitor]::Enter($Users.SyncRoot)
    try {
        $Users[[string]$newId] = $user
    } finally {
        [System.Threading.Monitor]::Exit($Users.SyncRoot)
    }

    $resp = [UserResourceResponse]::new()
    $resp.id = [int]$newId
    $resp.user = [UserPayload]::new()
    $resp.user.firstName = $Body.firstName
    $resp.user.lastName = $Body.lastName
    $resp.user.email = $Body.email

    Write-KrResponse $resp -StatusCode 201
}

function getUser {
    <#
    .SYNOPSIS
        Get a user by id.
    #>
    [OpenApiPath(HttpVerb = 'get', Pattern = '/users/{userId}', Tags = 'Users')]
    [OpenApiResponse(StatusCode = '200', Schema = [UserResourceResponse], Description = 'Found', ContentType = ('application/json', 'application/xml', 'application/yaml'))]
    [OpenApiResponseLinkRef(StatusCode = '200', Key = 'update', ReferenceId = 'UpdateUserLink')]
    [OpenApiResponseLinkRef(StatusCode = '200', Key = 'delete', ReferenceId = 'DeleteUserLink')]
    [OpenApiResponse(StatusCode = '404', Description = 'Not found')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true, Description = 'User identifier')]
        [int]$userId
    )

    $user = $null
    [System.Threading.Monitor]::Enter($Users.SyncRoot)
    try {
        $user = $Users[[string]$userId]
    } finally {
        [System.Threading.Monitor]::Exit($Users.SyncRoot)
    }

    if ($null -eq $user) {
        Write-KrStatusResponse -StatusCode 404
        return
    }

    $resp = [UserResourceResponse]::new()
    $resp.id = $userId
    $resp.user = [UserPayload]::new()
    $resp.user.firstName = $user.firstName
    $resp.user.lastName = $user.lastName
    $resp.user.email = $user.email

    Write-KrResponse $resp -StatusCode 200
}

function updateUser {
    <#
    .SYNOPSIS
        Update a user.
    .DESCRIPTION
        Update a user.
    #>
    [OpenApiPath(HttpVerb = 'put', Pattern = '/users/{userId}', Tags = 'Users')]
    [OpenApiResponse(StatusCode = '200', Description = 'User updated', ContentType = ('application/json', 'application/xml', 'application/yaml'), Schema = [UserResourceResponse])]
    [OpenApiResponseLinkRef(StatusCode = '200', Key = 'get', ReferenceId = 'GetUserLink')]
    [OpenApiResponseLinkRef(StatusCode = '200', Key = 'delete', ReferenceId = 'DeleteUserLink')]
    [OpenApiResponse(StatusCode = '404', Description = 'Not found')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true, Description = 'User identifier')]
        [int]$userId,

        [OpenApiRequestBody(Required = $true, ContentType = ('application/json', 'application/xml', 'application/yaml'))]
        [UserPayload]$Body
    )

    $updated = $false
    [System.Threading.Monitor]::Enter($Users.SyncRoot)
    try {
        if ($Users.ContainsKey([string]$userId)) {
            $Users[[string]$userId] = [ordered]@{ firstName = $Body.firstName; lastName = $Body.lastName; email = $Body.email }
            $updated = $true
        }
    } finally {
        [System.Threading.Monitor]::Exit($Users.SyncRoot)
    }

    if (-not $updated) {
        Write-KrStatusResponse -StatusCode 404
        return
    }

    $resp = [UserResourceResponse]::new()
    $resp.id = $userId
    $resp.user = $Body
    Write-KrResponse $resp -StatusCode 200
}

function deleteUser {
    <#
    .SYNOPSIS
        Delete a user.
    #>
    [OpenApiPath(HttpVerb = 'delete', Pattern = '/users/{userId}', Tags = 'Users')]
    [OpenApiResponse(StatusCode = '204', Description = 'Deleted')]
    [OpenApiResponse(StatusCode = '404', Description = 'Not found')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true, Description = 'User identifier')]
        [int]$userId
    )

    $removed = $false
    [System.Threading.Monitor]::Enter($Users.SyncRoot)
    try {
        if ($Users.ContainsKey([string]$userId)) {
            $Users.Remove([string]$userId)
            $removed = $true
        }
    } finally {
        [System.Threading.Monitor]::Exit($Users.SyncRoot)
    }

    if (-not $removed) {
        Write-KrStatusResponse -StatusCode 404
        return
    }

    Write-KrStatusResponse -StatusCode 204
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
