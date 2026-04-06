[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
param(
    [int]$Port = $env:PORT ?? 5443,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

<#
.SYNOPSIS
    Package-ready bike rental shop API.
.DESCRIPTION
    Demonstrates a realistic Kestrun PowerShell service that is ready to package as a .krpack.
    The sample uses HTTPS, API key authentication for staff routes, OpenAPI documentation,
    and persists bike inventory and rental state under the data folder.
.EXAMPLE
    New-KrServicePackage -SourceFolder .\examples\PowerShell\BikeRentalShop -OutputPath .\bike-rental-shop-1.0.0.krpack
.EXAMPLE
    pwsh .\examples\PowerShell\BikeRentalShop\Service.ps1 -Port 5443

    Invoke-RestMethod -Uri 'https://127.0.0.1:5443/api/bikes' -SkipCertificateCheck

    $rentalRequest = @{
        bikeId       = 'bk-100'
        customerName = 'Ava Flores'
        phone        = '+1-202-555-0148'
        plannedHours = 3
    } | ConvertTo-Json

    Invoke-RestMethod -Uri 'https://127.0.0.1:5443/api/rentals' -Method Post -ContentType 'application/json' -Body $rentalRequest -SkipCertificateCheck

    Invoke-RestMethod -Uri 'https://127.0.0.1:5443/api/staff/dashboard' -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'bike-shop-demo-key' }
#>

try {
    $scriptPath = Split-Path -Parent -Path $MyInvocation.MyCommand.Path
    $powerShellExamplesPath = Split-Path -Parent -Path $scriptPath
    $examplesPath = Split-Path -Parent -Path $powerShellExamplesPath
    $kestrunPath = Split-Path -Parent -Path $examplesPath
    $kestrunModulePath = Join-Path $kestrunPath 'src/PowerShell/Kestrun/Kestrun.psm1'

    if (Test-Path -LiteralPath $kestrunModulePath -PathType Leaf) {
        Import-Module $kestrunModulePath -Force -ErrorAction Stop
    } else {
        Import-Module -Name 'Kestrun' -MaximumVersion 2.99 -ErrorAction Stop
    }
} catch {
    Write-Error "Failed to import Kestrun module: $_"
    exit 1
}

Initialize-KrRoot -Path $PSScriptRoot

$DataRoot = Join-Path $PSScriptRoot 'data'
$LogsRoot = Join-Path $PSScriptRoot 'logs'
$CertificateRoot = Join-Path $DataRoot 'certs'
$StatePath = Join-Path $DataRoot 'bike-rental-state.json'
$CertificatePath = Join-Path $CertificateRoot 'bike-rental-shop-devcert.pfx'
$CertificatePassword = 'bike-rental-demo'
$StaffScheme = 'BikeRentalStaffApiKey'
$StaffApiKey = 'bike-shop-demo-key'
$StateMutex = [System.Threading.Mutex]::new($false, 'Kestrun.BikeRentalShop.State')

$stateScriptPath = Join-Path $PSScriptRoot 'Private/State.ps1'
if (-not (Test-Path -LiteralPath $stateScriptPath -PathType Leaf)) {
    Write-Error 'Required service file not found: Private/State.ps1'
    exit 1
}

$openApiScriptPath = Join-Path $PSScriptRoot 'Private/OpenApi.ps1'
if (-not (Test-Path -LiteralPath $openApiScriptPath -PathType Leaf)) {
    Write-Error 'Required service file not found: Private/OpenApi.ps1'
    exit 1
}

# Keep split OpenAPI declarations dot-sourced via literal paths so the annotation scanner can discover components.
. "$PSScriptRoot/Private/State.ps1"
. "$PSScriptRoot/Private/OpenApi.ps1"

Initialize-BikeRentalStorage

$logger = New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkFile -Path '.\logs\bike-rental-shop.log' -RollingInterval Day |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'DefaultLogger' -PassThru -SetAsDefault

$certificate = Get-BikeRentalCertificate
if (-not (Test-KrCertificate -Certificate $certificate)) {
    Write-Error 'Bike rental shop certificate validation failed.'
    exit 1
}

New-KrServer -Name 'Riverside Bike Rental'
Set-KrServerOptions -DenyServerHeader
Set-KrServerLimit -MaxRequestBodySize 1048576 -MaxConcurrentConnections 200 -MaxRequestHeaderCount 100 -KeepAliveTimeoutSeconds 120
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -X509Certificate $certificate -Protocols Http1AndHttp2
Add-KrCompressionMiddleware -EnableForHttps -MimeTypes @('application/json', 'text/plain')
Add-KrFaviconMiddleware

Add-KrApiKeyAuthentication -AuthenticationScheme $StaffScheme -ApiKeyName 'X-Api-Key' -StaticApiKey $StaffApiKey

Add-KrOpenApiInfo -Title 'Riverside Bike Rental API' -Version '1.0.0' -Description 'Bike rental service bundle example with HTTPS, OpenAPI, staff authentication, and persistent data.'
#Add-KrOpenApiServer -Url ("https://{0}:{1}" -f $IPAddress.IPAddressToString, $Port) -Description 'Local HTTPS endpoint'

$routesPath = Join-Path $PSScriptRoot 'Private/Routes.ps1'
if (-not (Test-Path -LiteralPath $routesPath -PathType Leaf)) {
    Write-Error 'Required service file not found: Private/Routes.ps1'
    exit 1
}

. $routesPath

$helperFunctions = @(
    'Invoke-BikeRentalStateLock'
    'Get-BikeRentalDefaultState'
    'Save-BikeRentalStateUnsafe'
    'Read-BikeRentalStateUnsafe'
    'Initialize-BikeRentalStorage'
    'Get-BikeRentalState'
    'Update-BikeRentalState'
    'Write-BikeRentalError'
    'Get-BikeRentalCertificate'
    'New-BikeCatalogItemObject'
    'New-RentalStatusObject'
)

foreach ($helperFunction in $helperFunctions) {
    $functionInfo = Get-Command -Name $helperFunction -CommandType Function -ErrorAction SilentlyContinue
    if ($null -ne $functionInfo) {
        Set-Item -Path ("Function:\global:{0}" -f $helperFunction) -Value $functionInfo.ScriptBlock -Force
    }
}

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Redoc -OpenApiEndpoint '/openapi/v3.1/openapi.json'

Add-KrOpenApiRoute

$null = Build-KrOpenApiDocument
if (-not (Test-KrOpenApiDocument)) {
    Write-KrLog -Level Error -Message 'Bike rental OpenAPI validation failed.'
}

Write-KrLog -Level Information -Message 'Bike rental shop ready on https://{address}:{port}' -Values $IPAddress.IPAddressToString, $Port
Start-KrServer -CloseLogsOnExit
