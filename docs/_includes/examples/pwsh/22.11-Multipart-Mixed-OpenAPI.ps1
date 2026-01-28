<#!
    22.4 multipart/mixed (ordered parts)

    Client example (PowerShell):
        $boundary = 'mixed-boundary'
        $body = @(
            "--$boundary",
            "Content-Type: text/plain",
            "",
            "first",
            "--$boundary",
            "Content-Type: application/json",
            "",
            '{"value":42}',
            "--$boundary--",
            ""
        ) -join "`r`n"
        Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$Port/mixed" -ContentType "multipart/mixed; boundary=$boundary" -Body $body

    Cleanup:
        Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.4-multipart-mixed')
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.4'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Uploads 22.4 - Multipart Mixed' `
    -Version '1.0.0' `
    -Description 'multipart/mixed parsing using Add-KrFormRoute.'

Add-KrOpenApiContact -Email 'support@example.com'

$uploadRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.4-multipart-mixed'

# Add Rules
# Note: multipart/mixed is parsed as ordered parts. Rules apply when a part includes a Content-Disposition name.
# Opt-in: only multipart/form-data is enabled by default
New-KrFormPartRule -Name 'text' -MaxBytes 1024 |
    New-KrFormPartRule -Name 'json' -MaxBytes 1024 |
    Add-KrFormOption -Name 'MixedForm' -DefaultUploadPath $uploadRoot -AllowedRequestContentTypes 'multipart/mixed'

<#.SYNOPSIS
    Form route for multipart/mixed using OpenAPI annotations.
.DESCRIPTION
    This function defines a form route that accepts multipart/mixed data using OpenAPI annotations.
    It responds with a JSON object containing the parsed parts' content types.
.PARAMETER FormPayload
    The form data payload containing the uploaded parts.
#>
function mixed {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/mixed')]
    [KrBindForm(Template = 'MixedForm')]
    [OpenApiResponse(  StatusCode = '200', Description = 'Parsed fields and files', ContentType = 'application/json')]
    param(
        [OpenApiRequestBody(contentType = ('multipart/mixed'), Required = $true)]
         $FormPayload
    )
    $contentTypes = $FormPayload.Parts | ForEach-Object { $_.ContentType }
    Write-KrJsonResponse -InputObject @{ count = $FormPayload.Parts.Count; contentTypes = $contentTypes } -StatusCode 200
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
