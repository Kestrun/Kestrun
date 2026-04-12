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
    New-KrServicePackage -SourceFolder .\examples\PowerShell\BikeRentalShop\Synchronized -OutputPath .\bike-rental-shop-1.0.0.krpack
.EXAMPLE
    pwsh .\examples\PowerShell\BikeRentalShop\Synchronized\Service.ps1 -Port 5443

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
    $kestrunPath = Get-Item -LiteralPath $scriptPath
    while ($kestrunPath -and -not (Test-Path -LiteralPath (Join-Path $kestrunPath.FullName 'Kestrun.sln') -PathType Leaf)) {
        $parentPath = Split-Path -Parent -Path $kestrunPath.FullName
        if ([string]::IsNullOrWhiteSpace($parentPath) -or ($parentPath -eq $kestrunPath.FullName)) {
            break
        }

        $kestrunPath = Get-Item -LiteralPath $parentPath
    }

    if (-not $kestrunPath) {
        throw 'Unable to locate the repository root.'
    }

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

# If the certificate Path doesn't exist, create the directory. The certificate will be created on demand by Get-BikeRentalCertificate and saved to this path for reuse across restarts, so it needs to be writable.
if (-not (Test-Path -Path $CertificateRoot -PathType Container)) {
    New-Item -Path $CertificateRoot -ItemType Directory -Force | Out-Null
}
$CertificatePassword = 'bike-rental-demo'
$CertificatePath = Join-Path $CertificateRoot 'bike-rental-shop-devcert.pfx'


$StatePath = Join-Path $DataRoot 'bike-rental-state.clixml'
$LegacyStatePath = Join-Path $DataRoot 'bike-rental-state.json'
$BikeRentalStateStore = $null
$BikeRentalStateLockKey = 'BikeRentalShop.State'

$StaffScheme = 'BikeRentalStaffApiKey'
$StaffApiKey = 'bike-shop-demo-key'

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

$routesPath = Join-Path $PSScriptRoot 'Private/Routes.ps1'
if (-not (Test-Path -LiteralPath $routesPath -PathType Leaf)) {
    Write-Error 'Required service file not found: Private/Routes.ps1'
    exit 1
}

# Keep split OpenAPI declarations dot-sourced via literal paths so the annotation scanner can discover components.
. "$PSScriptRoot/Private/State.ps1"
. "$PSScriptRoot/Private/OpenApi.ps1"



# Get or create the certificate before starting the server so it's available for HTTPS configuration and we can fail early if there's an issue with the certificate setup.
$certificate = Get-BikeRentalCertificate -CertificatePath $CertificatePath -CertificatePassword (ConvertTo-SecureString -String $CertificatePassword -AsPlainText -Force)
if (-not (Test-KrCertificate -Certificate $certificate)) {
    Write-Error 'Bike rental shop certificate validation failed.'
    exit 1
}

$BikeRentalStateStore = Initialize-BikeRentalStorage

$routesPath = Join-Path $PSScriptRoot 'Private/Routes.ps1'
if (-not (Test-Path -LiteralPath $routesPath -PathType Leaf)) {
    Write-Error 'Required service file not found: Private/Routes.ps1'
    exit 1
}
# The service descriptor is defined in the .psd1 file with the same base name as this script, so it can be automatically discovered by Kestrun when packaging.

# Configure logging, server, middleware, and OpenAPI documentation before defining routes so they're available globally.
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkFile -Path (Join-Path $LogsRoot 'bike-rental-shop.log') -RollingInterval Day |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'DefaultLogger' -SetAsDefault

New-KrServer -Name 'Riverside Bike Rental'
Set-KrServerOptions -DenyServerHeader
Set-KrServerLimit -MaxRequestBodySize 1048576 -MaxConcurrentConnections 200 -MaxRequestHeaderCount 100 -KeepAliveTimeoutSeconds 120
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -X509Certificate $certificate -Protocols Http1AndHttp2AndHttp3
Add-KrCompressionMiddleware -EnableForHttps -MimeTypes @('application/json', 'text/plain')
Add-KrFaviconMiddleware

Add-KrApiKeyAuthentication -AuthenticationScheme $StaffScheme -ApiKeyName 'X-Api-Key' -StaticApiKey $StaffApiKey

Add-KrOpenApiInfo -Title 'Riverside Bike Rental API' -Version '1.0.0' -Description 'Bike rental service bundle example with HTTPS, OpenAPI, staff authentication, and persistent data.'
#Add-KrOpenApiServer -Url ("https://{0}:{1}" -f $IPAddress.IPAddressToString, $Port) -Description 'Local HTTPS endpoint'

# The route definitions are split into a separate file for clarity, but they could also be defined here. Keep the dot-sourcing via literal path so the annotation scanner can discover API components.
. $routesPath

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc
Add-KrApiDocumentationRoute -DocumentType Rapidoc
Add-KrApiDocumentationRoute -DocumentType Scalar
Add-KrApiDocumentationRoute -DocumentType Elements

Add-KrOpenApiRoute

$null = Build-KrOpenApiDocument
if (-not (Test-KrOpenApiDocument)) {
    Write-KrLog -Level Error -Message 'Bike rental OpenAPI validation failed.'
}

Write-KrLog -Level Information -Message 'Bike rental shop ready on https://{address}:{port}' -Values $IPAddress.IPAddressToString, $Port
Start-KrServer -CloseLogsOnExit
