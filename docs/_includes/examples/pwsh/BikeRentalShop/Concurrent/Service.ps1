[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
param(
    [int]$Port = $env:PORT ?? 5443,
    [IPAddress]$IPAddress = [IPAddress]::Loopback,
    [string[]]$AllowedCorsOrigins = @()
)

if ((-not $PSBoundParameters.ContainsKey('AllowedCorsOrigins')) -and -not [string]::IsNullOrWhiteSpace($env:BIKE_RENTAL_ALLOWED_CORS_ORIGINS)) {
    $AllowedCorsOrigins = @($env:BIKE_RENTAL_ALLOWED_CORS_ORIGINS -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

if (-not (Get-Command -Name 'Initialize-KrRoot' -CommandType Function -ErrorAction SilentlyContinue)) {
    try {
        Import-Module Kestrun -ErrorAction Stop
    } catch {
        $repoModulePath = Join-Path $PSScriptRoot '..\..\..\..\..\..\src\PowerShell\Kestrun\Kestrun.psm1'
        if (Test-Path -LiteralPath $repoModulePath) {
            try {
                Import-Module $repoModulePath -Force -ErrorAction Stop
            } catch {
            }
        }
    }
}

<#
.SYNOPSIS
    Package-ready concurrent bike rental shop API.
.DESCRIPTION
    Demonstrates a realistic Kestrun PowerShell service that is ready to package as a .krpack.
    This concurrent variant keeps the in-memory database in concurrent dictionaries end to end,
    uses HTTPS, API key authentication for staff routes, OpenAPI documentation, and persists
    bike inventory and rental state under the data folder.
.EXAMPLE
    New-KrServicePackage -SourceFolder .\docs\_includes\examples\pwsh\BikeRentalShop\Concurrent -OutputPath .\bike-rental-shop-concurrent-1.0.0.krpack
.EXAMPLE
    pwsh .\docs\_includes\examples\pwsh\BikeRentalShop\Concurrent\Service.ps1 -Port 5443

    Invoke-RestMethod -Uri 'https://127.0.0.1:5443/api/bikes' -SkipCertificateCheck

    $rentalRequest = @{
        bikeId       = 'bk-100'
        customerName = 'Ava Flores'
        phone        = '+1-202-555-0148'
        plannedHours = 3
    } | ConvertTo-Json

    Invoke-RestMethod -Uri 'https://127.0.0.1:5443/api/rentals' -Method Post -ContentType 'application/json' -Body $rentalRequest -SkipCertificateCheck

    Invoke-RestMethod -Uri 'https://127.0.0.1:5443/api/staff/dashboard' -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'bike-shop-demo-key' }
.EXAMPLE
    pwsh .\docs\_includes\examples\pwsh\BikeRentalShop\Concurrent\Service.ps1 -Port 5444 -AllowedCorsOrigins @('https://127.0.0.1:5445', 'https://localhost:5445')

    Starts the concurrent backend with explicit browser origins enabled for a separate web client service.
#>

Initialize-KrRoot -Path $PSScriptRoot

$DataRoot = Join-Path $PSScriptRoot 'data'
$LogsRoot = Join-Path $PSScriptRoot 'logs'
$CertificateRoot = Join-Path $DataRoot 'certs'
$BikeRentalShopRoot = Split-Path -Parent $PSScriptRoot
$SharedCertificateRoot = Join-Path $BikeRentalShopRoot 'certs'

# If the certificate path doesn't exist, create the directory. The certificate will be created on demand
# by Get-BikeRentalCertificate and saved to this path for reuse across restarts, so it needs to be writable.
if (-not (Test-Path -Path $CertificateRoot -PathType Container)) {
    New-Item -Path $CertificateRoot -ItemType Directory -Force | Out-Null
}

if (-not (Test-Path -Path $SharedCertificateRoot -PathType Container)) {
    New-Item -Path $SharedCertificateRoot -ItemType Directory -Force | Out-Null
}

$CertificatePassword = 'bike-rental-demo'
$CertificatePath = Join-Path $CertificateRoot 'bike-rental-shop-concurrent-devcert.pfx'
$RootCertificatePassword = 'bike-rental-shared-root'
$RootCertificatePath = Join-Path $CertificateRoot 'bike-rental-shared-root.pfx'
$ParentRootCertificatePath = Join-Path $SharedCertificateRoot 'bike-rental-shared-root.pfx'

$StatePath = Join-Path $DataRoot 'bike-rental-state.clixml'
$LegacyStatePath = Join-Path $DataRoot 'bike-rental-state.json'
$BikeRentalStateStore = $null
$BikeRentalStateLockKey = 'BikeRentalShopConcurrent.State'

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

# Configure logging, server, middleware, and OpenAPI documentation before defining routes so they are
# available globally.
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkFile -Path (Join-Path $LogsRoot 'bike-rental-shop-concurrent.log') -RollingInterval Day |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'DefaultLogger' -SetAsDefault

# Keep the split state and OpenAPI declarations dot-sourced via literal paths so shared helpers and
# the annotation scanner can discover the concurrent database helpers and API components.
. "$PSScriptRoot/Private/State.ps1"
. "$PSScriptRoot/Private/OpenApi.ps1"

# Get or create the certificate set before starting the server so HTTPS setup fails early if there is a
# certificate problem.
$certificateMaterial = Get-BikeRentalCertificate `
    -CertificatePath $CertificatePath `
    -CertificatePassword (ConvertTo-SecureString -String $CertificatePassword -AsPlainText -Force) `
    -RootCertificatePath $RootCertificatePath `
    -ParentRootCertificatePath $ParentRootCertificatePath `
    -RootCertificatePassword (ConvertTo-SecureString -String $RootCertificatePassword -AsPlainText -Force)
$certificate = $certificateMaterial.LeafCertificate
$rootCertificate = $certificateMaterial.RootCertificate
$certificateValidationFailure = ''
if (-not (Test-KrCertificate -Certificate $certificate -CertificateChain $rootCertificate -FailureReasonVariable 'certificateValidationFailure')) {
    Write-Error "Bike rental shop certificate validation failed: $certificateValidationFailure"
    exit 1
}

# Load the persisted state once and keep the resulting concurrent database in memory before
# Enable-KrConfiguration so it is available to every route runspace.
$BikeRentalStateStore = Initialize-BikeRentalStorage

New-KrServer -Name 'Riverside Bike Rental Concurrent'
Set-KrServerOptions -DenyServerHeader
Set-KrServerLimit -MaxRequestBodySize 1048576 -MaxConcurrentConnections 200 -MaxRequestHeaderCount 100 -KeepAliveTimeoutSeconds 120


Add-KrEndpoint -Port $Port -IPAddress $IPAddress -X509Certificate $certificate -Protocols Http1AndHttp2AndHttp3
Add-KrCompressionMiddleware -EnableForHttps -MimeTypes @('application/json', 'text/plain')
Add-KrFaviconMiddleware

Add-KrApiKeyAuthentication -AuthenticationScheme $StaffScheme -ApiKeyName 'X-Api-Key' -StaticApiKey $StaffApiKey

if ($AllowedCorsOrigins.Count -gt 0) {
    New-KrCorsPolicyBuilder |
        Set-KrCorsOrigin -Origins $AllowedCorsOrigins |
        Set-KrCorsMethod -Methods GET, POST, DELETE |
        Set-KrCorsHeader -Any |
        Set-KrCorsPreflightMaxAge -Seconds 3600 |
        Add-KrCorsPolicy -Default
} else {
    New-KrCorsPolicyBuilder |
        Set-KrCorsOrigin -Any |
        Set-KrCorsMethod -Methods GET, POST, DELETE |
        Set-KrCorsHeader -Any |
        Set-KrCorsPreflightMaxAge -Seconds 3600 |
        Add-KrCorsPolicy -Default
}

Add-KrOpenApiInfo -Title 'Riverside Bike Rental Concurrent API' -Version '1.0.0' `
    -Description 'Bike rental service example with a concurrent in-memory database, HTTPS, OpenAPI, staff authentication, persistent data, and optional CORS for an external web client.'

# The route definitions are split into a separate file for clarity, but they could also be defined here.
. $routesPath

Enable-KrConfiguration

# Publish the documentation UIs and raw OpenAPI endpoint after the route set is complete.
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

Write-KrLog -Level Information -Message 'Bike rental concurrent shop ready on https://{address}:{port}' -Values $IPAddress.IPAddressToString, $Port
Start-KrServer -CloseLogsOnExit
