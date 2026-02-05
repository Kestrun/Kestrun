<#
    Sample: Authentication failures + status codes
    Purpose: Provide a small API surface to manually try common HTTP status codes
             (401/403/404/405/415/400/200/201/204) across GET/POST/DELETE.
    File:    8.13-Auth-StatusCodes.ps1

    Credentials (Basic):
      - admin:password  (claims: can_read, can_write)
      - user:password   (claims: can_read)

    Notes:
      - The /secure/resource/{id} DELETE route requires policy CanDelete.
      - /json/echo requires Content-Type: application/json (else 415).
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', '', Justification = 'Tutorial sample uses basic auth demo credentials.')]
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

New-KrServer -Name 'Auth + Status Codes'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Auth + Status Codes' -Version '1.0.0' -Description 'Endpoints to exercise common auth failures and HTTP status codes.'
Add-KrOpenApiContact -Email 'support@example.com'

# =========================================================
#                 COMPONENT SCHEMAS
# =========================================================

[OpenApiSchemaComponent(
    Description = 'JSON echo request payload.', RequiredProperties = ('name', 'quantity')
)]
class JsonEchoRequest {
    [OpenApiProperty(Description = 'Friendly name', Example = 'widget')]
    [ValidateNotNullOrEmpty()]
    [string]$name

    [OpenApiProperty(Description = 'Quantity (1..100)', Example = 2)]
    [ValidateRange(1, 100)]
    [int]$quantity

    [OpenApiProperty(Description = 'Optional priority', Example = 'normal')]
    [ValidateSet('low', 'normal', 'high')]
    [string]$priority
}


[OpenApiSchemaComponent(Description = 'JSON echo request payload.', AdditionalPropertiesAllowed = $true, AdditionalProperties = [bool])]
class JsonEchoRequestPlus: JsonEchoRequest {}

# =========================================================
#                 AUTHN / AUTHZ
# =========================================================

$claimConfig = New-KrClaimPolicy |
    Add-KrClaimPolicy -PolicyName 'CanRead' -ClaimType 'can_read' -AllowedValues 'true' |
    Add-KrClaimPolicy -PolicyName 'CanWrite' -ClaimType 'can_write' -AllowedValues 'true' |
    Add-KrClaimPolicy -PolicyName 'CanDelete' -ClaimType 'can_delete' -AllowedValues 'true' |
    Build-KrClaimPolicy

Add-KrBasicAuthentication -AuthenticationScheme 'StatusBasic' -Realm 'StatusCodes' -AllowInsecureHttp -ScriptBlock {
    param($Username, $Password)
    Write-KrLog -Level Debug -Message "Authenticating user '$Username' with provided password '$Password'."
    if ($Password -ne 'password') { return $false }

    return ($Username -eq 'admin' -or $Username -eq 'user')
} -IssueClaimsScriptBlock {
    param($Identity)

    if ($Identity -eq 'admin') {
        return (
            Add-KrUserClaim -ClaimType 'can_read' -Value 'true' |
                Add-KrUserClaim -ClaimType 'can_write' -Value 'true'
        )
    }

    if ($Identity -eq 'user') {
        return (Add-KrUserClaim -ClaimType 'can_read' -Value 'true')
    }
} -ClaimPolicyConfig $claimConfig

# =========================================================
#                      ROUTES
# =========================================================

<#
.SYNOPSIS
    Public endpoint.
#>
function getPublic {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/public', Summary = 'Public health check', Tags = ('StatusCodes'))]
    [OpenApiResponse(StatusCode = '200', Description = 'OK', ContentType = 'text/plain', Schema = [string])]
    param()

    Write-KrTextResponse -Text 'OK' -StatusCode 200
}

<#
.SYNOPSIS
    Protected endpoint (401 when missing/invalid credentials).
#>
function getSecureHello {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/secure/hello', Summary = 'Protected GET (Basic auth)', Tags = ('Auth', 'StatusCodes'))]
    [OpenApiAuthorization(Scheme = 'StatusBasic')]
    [OpenApiResponse(StatusCode = '200', Description = 'OK', ContentType = 'application/json', Schema = [object])]
    [OpenApiResponse(StatusCode = '401', Description = 'Unauthorized')]
    param()

    Write-KrJsonResponse -InputObject @{ message = 'hello'; user = $Context.User.Identity.Name } -StatusCode 200
}

<#
.SYNOPSIS
    Delete a resource (403 when authenticated but missing CanDelete).
.DESCRIPTION
    This endpoint deletes a resource identified by its ID.
.PARAMETER id
    The ID of the resource to delete.
#>
function deleteSecureResource {
    [OpenApiPath(HttpVerb = 'delete', Pattern = '/secure/resource/{id}', Summary = 'Protected DELETE (requires CanDelete)', Tags = ('Auth', 'StatusCodes'))]
    [OpenApiAuthorization(Scheme = 'StatusBasic', Policies = 'CanDelete')]
    [OpenApiResponse(StatusCode = '204', Description = 'Deleted')]
    [OpenApiResponse(StatusCode = '401', Description = 'Unauthorized')]
    [OpenApiResponse(StatusCode = '403', Description = 'Forbidden')]
    param(
        [OpenApiParameter(In = 'path', Required = $true, Description = 'Resource id')]
        [int]$id
    )

    Write-KrLog -Level Information -Message 'Delete requested for resource {id}' -Values $id
    Write-KrTextResponse -Text '' -StatusCode 204
}

<#
.SYNOPSIS
    JSON echo (415 when Content-Type is not application/json; 400 when body is invalid JSON).
.DESCRIPTION
    This endpoint echoes back the received JSON payload.
.PARAMETER body
    The JSON body to echo back.
#>
function postJsonEcho {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/json/echo', Summary = 'POST requires application/json', Tags = ('StatusCodes'))]
    [OpenApiResponse(StatusCode = '201', Description = 'Created', ContentType = 'application/json', Schema = [object])]
    [OpenApiResponse(StatusCode = '400', Description = 'Bad Request')]
    [OpenApiResponse(StatusCode = '422', Description = 'Unprocessable Content')]
    [OpenApiResponse(StatusCode = '415', Description = 'Unsupported Media Type')]
    param(
        [OpenApiRequestBody( ContentType = 'application/json', Required = $true)]
        [JsonEchoRequest]$body
    )

    Expand-KrObject -InputObject $body
    Write-KrJsonResponse -InputObject @{ received = $body } -StatusCode 201
}


<#
.SYNOPSIS
    JSON echo (415 when Content-Type is not application/json; 400 when body is invalid JSON).
.DESCRIPTION
    This endpoint echoes back the received JSON payload.
.PARAMETER body
    The JSON body to echo back.
#>
function postJsonEchoPlus {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/json/echoPlus', Summary = 'POST requires application/json', Tags = ('StatusCodes'))]
    [OpenApiResponse(StatusCode = '201', Description = 'Created', ContentType = 'application/json', Schema = [object])]
    [OpenApiResponse(StatusCode = '400', Description = 'Bad Request')]
    [OpenApiResponse(StatusCode = '422', Description = 'Unprocessable Content')]
    [OpenApiResponse(StatusCode = '415', Description = 'Unsupported Media Type')]
    param(
        [OpenApiRequestBody( ContentType = 'application/json', Required = $true)]
        [JsonEchoRequestPlus]$body
    )

    Expand-KrObject -InputObject $body
    Write-KrJsonResponse -InputObject @{ received = $body } -StatusCode 201
}

<#
.SYNOPSIS
    GET-only endpoint (POST will produce 405 Method Not Allowed).
.DESCRIPTION
    This endpoint only supports the GET method.
#>
function getOnlyGet {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/only-get', Summary = 'GET-only endpoint', Tags = ('StatusCodes'))]
    [OpenApiResponse(StatusCode = '200', Description = 'OK', ContentType = 'text/plain', Schema = [string])]
    param()

    Write-KrTextResponse -Text 'GET OK' -StatusCode 200
}

Enable-KrConfiguration

# =========================================================
#                OPENAPI DOC ROUTE / UI
# =========================================================

Add-KrOpenApiRoute

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

Start-KrServer -CloseLogsOnExit
