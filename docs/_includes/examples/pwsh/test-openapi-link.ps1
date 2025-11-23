param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module -Force "$PSScriptRoot\..\..\..\..\src\PowerShell\Kestrun\Kestrun.psm1"

$sv = New-KrOpenApiServerVariable -Name 'env' -Default 'dev' -Enum @('dev', 'staging', 'prod') -Description 'Environment name'
$srv = New-KrOpenApiServer -Url 'https://{env}.api.example.com' -Description 'Target API endpoint' -Variables $sv

$params = @{ id = '$response.body#/id'; verbose = '$request.query.verbose' }
$rb = @{ email = '$request.body#/email'; locale = '$request.body#/locale' }

$link = New-KrOpenApiLink -OperationRef '#/paths/~1users~1{id}/get' -OperationId 'getUserById' -Description 'Link to fetch user details using the id from the response body.' -Parameters $params -Server $srv -RequestBody $rb

$components = [Kestrun.OpenApi.OpenApiSchemaDiscovery]::GetOpenApiTypesAuto()
$doc = [Kestrun.OpenApi.OpenApiV2Generator]::Generate($components, 'Kestrun API', '1.0.0')
if ($null -eq $doc.Components.Links) {
    $doc.Components.Links = [System.Collections.Generic.Dictionary[string, Microsoft.OpenApi.IOpenApiLink]]::new()
}
$doc.Components.Links['UserById'] = $link

$json = [Kestrun.OpenApi.OpenApiV2Generator]::ToJson($doc, $true)
$json | Set-Content -Encoding UTF8 -Path "$PSScriptRoot\openapi-test.json"

if ($json -match 'System\.Collections\.Hashtable') {
    Write-Host 'Found unwanted System.Collections.Hashtable' -ForegroundColor Red
    exit 2
}
Write-Host 'OK: No System.Collections.Hashtable in JSON' -ForegroundColor Green
