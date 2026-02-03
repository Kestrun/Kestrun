<#!
    22.12-Nested-Multipart-OpenAPI.ps1
    Example PowerShell script for KestRun demonstrating nested multipart/mixed payload parsing
    using OpenAPI documentation and Add-KrFormRoute.

    Client example (PowerShell):
        $outer = 'outer-boundary'
        $inner = 'inner-boundary'
        $innerBody = @(
            "--$inner",
            "Content-Type: text/plain",
            "",
            "inner-1",
            "--$inner",
            "Content-Type: application/json",
            "",
            '{"nested":true}',
            "--$inner--",
            ""
        ) -join "`r`n"
        $outerBody = @(
            "--$outer",
            "Content-Type: application/json",
            "",
            '{"stage":"outer"}',
            "--$outer",
            "Content-Type: multipart/mixed; boundary=$inner",
            "",
            $innerBody,
            "--$outer--",
            ""
        ) -join "`r`n"
        Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$Port/nested" -ContentType "multipart/mixed; boundary=$outer" -Body $outerBody

    Cleanup:
        Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.5-nested-multipart')
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.5'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Uploads 22.5 - Nested Multipart' `
    -Version '1.0.0' `
    -Description 'Nested multipart/mixed payload parsing using Add-KrFormRoute.'

Add-KrOpenApiContact -Email 'support@example.com'

# Set default upload path for form parts
$uploadRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.5-nested-multipart'
Set-KrServerOptions -DefaultUploadPath $uploadRoot

[OpenApiSchemaComponent(Description = 'Inner nested multipart payload.')]
class NonARule {
    [KrPartAttribute(Required = $true, MaxBytes = 1024, ContentTypes = 'text/plain'    )]
    [OpenApiProperty(Description = 'Inner text part.')]
    [string] $someText

    [KrPartAttribute(Required = $true, MaxBytes = 4096, ContentTypes = 'application/json')]
    [OpenApiProperty(Description = 'Inner JSON part.')]
    [string] $somejson  # or a real class if you want strict JSON
}

[OpenApiSchemaComponent(Description = 'Nested multipart request body.' , AdditionalPropertiesAllowed = $true )]
class OuterControl {}

[OpenApiSchemaComponent(Description = 'Inner nested multipart payload.')]
class NestedParts {

    [KrPartAttribute(Required = $true, MaxBytes = 1024, ContentTypes = 'text/plain'    )]
    [OpenApiProperty(Description = 'Inner text part.')]
    [string] $text

    [KrPartAttribute(Required = $true, MaxBytes = 4096, ContentTypes = 'application/json')]
    [OpenApiProperty(Description = 'Inner JSON part.')]
    [string] $json  # or a real class if you want strict JSON
}

[OpenApiSchemaComponent(Description = 'Nested multipart request body.')]
[KrBindForm( MaxNestingDepth = 1)]
class NestedMultipartRequest {
    [KrPart(Required = $true, MaxBytes = 1024, ContentTypes = 'application/json')]
    [OpenApiProperty(Description = 'Outer JSON control object.')]
    [OuterControl] $outer  # or a class

    [KrPart(Required = $true, MaxBytes = 1048576, ContentTypes = 'multipart/mixed')]
    [OpenApiProperty(Description = 'Nested multipart container.')]
    [NestedParts[]] $nested # Nested multipart payload definition
}

<#
.SYNOPSIS
    Upload endpoint for nested multipart/mixed
.DESCRIPTION
    Handles nested multipart/mixed payloads with file and field processing.
.PARAMETER FormPayload
    The parsed multipart/mixed payload.
#>
function nested {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/nested')]
    # [KrBindForm( MaxNestingDepth = 1)]
    [OpenApiResponse(  StatusCode = '200', Description = 'Parsed fields and files', ContentType = 'application/json')]

    param(
        [OpenApiRequestBody(contentType = ('multipart/mixed'), Required = $true )]
        [NestedMultipartRequest]$FormPayload
    )
    $FormPayload | ConvertTo-Json -Depth 5 | Write-Host
    # If you want to return counts, compute from bound model:
    $outerParts = $FormPayload.Parts
    $nestedSummary = @()
    foreach ($part in $outerParts) {
        if ($null -ne $part.NestedPayload) {
            $nestedSummary += [ordered]@{
                outerContentType = $part.ContentType
                nestedCount = $part.NestedPayload.Parts.Count
            }
        }
    }
    Write-KrJsonResponse -InputObject @{ outerCount = $outerParts.Count; nested = $nestedSummary; formPayload = $FormPayload } -StatusCode 200
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
