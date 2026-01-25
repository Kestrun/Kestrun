<#!
    22.3 application/x-www-form-urlencoded forms

    Client example (PowerShell):
        $body = 'name=Kestrun&role=admin&role=maintainer'
        Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$Port/form" -ContentType 'application/x-www-form-urlencoded' -Body $body

    Cleanup:
        Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.3-urlencoded')
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

$uploadRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.3-urlencoded'
$options = [Kestrun.Forms.KrFormOptions]::new()
$options.DefaultUploadPath = $uploadRoot

# Opt-in: only multipart/form-data is enabled by default
$options.AllowedRequestContentTypes.Clear()
$options.AllowedRequestContentTypes.Add('application/x-www-form-urlencoded')

# Add Rules
$nameRule = [Kestrun.Forms.KrPartRule]::new()
$nameRule.Name = 'name'
$nameRule.Required = $true
$options.Rules.Add($nameRule)

$roleRule = [Kestrun.Forms.KrPartRule]::new()
$roleRule.Name = 'role'
$roleRule.Required = $false
$options.Rules.Add($roleRule)

Add-KrFormRoute -Pattern '/form' -Options $options -ScriptBlock {
    $fields = @{}
    foreach ($key in $FormPayload.Fields.Keys) {
        $fields[$key] = $FormPayload.Fields[$key]
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
