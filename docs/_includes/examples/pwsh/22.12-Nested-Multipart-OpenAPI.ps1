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

$uploadRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.5-nested-multipart'

# Add Rules
# Note: nested multipart is parsed as ordered parts; rules apply when a part includes a Content-Disposition name.
New-KrFormPartRule -Name 'outer' -Required -MaxBytes 1024 `
    -AllowOnlyOne `
    -AllowedContentTypes 'application/json' |

    New-KrFormPartRule -Name 'nested' -Required -MaxBytes (1024 * 1024) `
        -AllowOnlyOne `
        -AllowedContentTypes 'multipart/mixed' |

    # These apply only inside the nested multipart container (Scope = 'nested')
    New-KrFormPartRule -Name 'text' -Scope 'nested' -Required -MaxBytes 1024 `
        -AllowOnlyOne `
        -AllowedContentTypes 'text/plain' |

    New-KrFormPartRule -Name 'json' -Scope 'nested' -Required -MaxBytes 4096 `
        -AllowOnlyOne `
        -AllowedContentTypes 'application/json' |
    Add-KrFormOption -Name 'NestedForm' -DefaultUploadPath $uploadRoot -AllowedRequestContentTypes 'multipart/mixed' -MaxNestingDepth 1


<#.SYNOPSIS
    Upload endpoint for nested multipart/mixed
.DESCRIPTION
    Handles nested multipart/mixed payloads with file and field processing.
.PARAMETER FormPayload
    The parsed multipart/mixed payload.
#>
function nested {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/nested')]
    [KrBindForm(Template = 'NestedForm')]
    [OpenApiResponse(  StatusCode = '200', Description = 'Parsed fields and files', ContentType = 'application/json')]
    param(
        [OpenApiRequestBody(contentType = ('multipart/form-data'), Required = $true)]
        $FormPayload
    )
    $outerParts = $FormPayload.Parts
    $nestedSummary = @()
    foreach ($part in $outerParts) {
        if ($null -ne $part.NestedPayload) {
            $nestedSummary += [pscustomobject]@{
                outerContentType = $part.ContentType
                nestedCount = $part.NestedPayload.Parts.Count
            }
        }
    }
    Write-KrJsonResponse -InputObject @{ outerCount = $outerParts.Count; nested = $nestedSummary } -StatusCode 200
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
