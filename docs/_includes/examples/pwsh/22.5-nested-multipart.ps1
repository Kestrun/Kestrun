<#!
    22.5 nested multipart/mixed (one level)

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
$options = [Kestrun.Forms.KrFormOptions]::new()
$options.DefaultUploadPath = $uploadRoot
$options.Limits.MaxNestingDepth = 1

# Opt-in: only multipart/form-data is enabled by default
$options.AllowedRequestContentTypes.Clear()
$options.AllowedRequestContentTypes.Add('multipart/mixed')

# Add Rules
# Note: nested multipart is parsed as ordered parts; rules apply when a part includes a Content-Disposition name.
$nestedRule = [Kestrun.Forms.KrPartRule]::new()
$nestedRule.Name = 'nested'
$nestedRule.MaxBytes = 1024 * 1024
$options.Rules.Add($nestedRule)

Add-KrFormRoute -Pattern '/nested' -Options $options -ScriptBlock {
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
