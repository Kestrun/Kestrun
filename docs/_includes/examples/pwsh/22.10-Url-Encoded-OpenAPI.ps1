<#!
    22.10-Url-Encoded-OpenAPI application/x-www-form-urlencoded forms

    Client example (PowerShell):
        $body = 'name=Kestrun&role=admin&role=maintainer'
        Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$Port/form" -ContentType 'application/x-www-form-urlencoded' -Body $body

    Cleanup:
    Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) '22.10-Url-Encoded-OpenAPI')
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.3'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Uploads 22.3 - UrlEncoded' `
    -Version '1.0.0' `
    -Description 'application/x-www-form-urlencoded form parsing using Add-KrFormRoute.'

Add-KrOpenApiContact -Email 'support@example.com'

# Set default upload path for form parts
$uploadRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.3-urlencoded'
Set-KrServerOptions -DefaultUploadPath $uploadRoot

[OpenApiSchemaComponent(Description = 'Url-encoded form payload')]
[KrBindForm(ComputeSha256 = $false)]
class UrlEncodedForm {
    [KrPart(Required = $true)]
    [OpenApiProperty(Description = 'User name')]
    [string] $Name

    [KrPart(Required = $false, AllowMultiple = $true)]
    [OpenApiProperty(Description = 'Roles for the user')]
    [string[]] $Role
}

<#
.SYNOPSIS
    Form route for application/x-www-form-urlencoded using OpenAPI annotations.
.DESCRIPTION
    This function defines a form route that accepts application/x-www-form-urlencoded data using OpenAPI annotations.
    It responds with a JSON object containing the parsed fields.
.PARAMETER FormPayload
    The form data payload containing the submitted fields.
#>
function form {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/form')]
    [OpenApiResponse(  StatusCode = '200', Description = 'Parsed fields and files', ContentType = 'application/json')]
    param(
        [OpenApiRequestBody(contentType = ('application/x-www-form-urlencoded'), Required = $true)]
        [UrlEncodedForm] $FormPayload
    )
    $fields = @{}
    if (-not [string]::IsNullOrWhiteSpace($FormPayload.Name)) {
        $fields['name'] = @($FormPayload.Name)
    }
    if ($null -ne $FormPayload.Role) {
        $fields['role'] = @($FormPayload.Role)
    }
    Write-KrJsonResponse -InputObject @{ fields = $fields } -StatusCode 200
}

Enable-KrConfiguration

# =========================================================
#                OPENAPI DOC ROUTE / UI
# =========================================================

Add-KrOpenApiRoute -SpecVersion OpenApi3_2

Add-KrApiDocumentationRoute -DocumentType Swagger -OpenApiEndpoint '/openapi/v3.2/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Redoc -OpenApiEndpoint '/openapi/v3.2/openapi.json'

# Start the server asynchronously
Start-KrServer
