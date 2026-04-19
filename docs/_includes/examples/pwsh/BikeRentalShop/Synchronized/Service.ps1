[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
param(
    [int]$Port = $env:PORT ?? 5443,
    [IPAddress]$IPAddress = [IPAddress]::Loopback,
    [string[]]$AllowedCorsOrigins = @()
)

if ((-not $PSBoundParameters.ContainsKey('AllowedCorsOrigins')) -and -not [string]::IsNullOrWhiteSpace($env:BIKE_RENTAL_ALLOWED_CORS_ORIGINS)) {
    $AllowedCorsOrigins = @($env:BIKE_RENTAL_ALLOWED_CORS_ORIGINS -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

<#
.SYNOPSIS
    Package-ready bike rental shop API.
.DESCRIPTION
    Demonstrates a realistic Kestrun PowerShell service that is ready to package as a .krpack.
    The sample uses HTTPS, API key authentication for staff routes, OpenAPI documentation,
    and persists bike inventory and rental state under the data folder.
.EXAMPLE
    New-KrServicePackage -SourceFolder .\docs\_includes\examples\pwsh\BikeRentalShop\Synchronized -OutputPath .\bike-rental-shop-1.0.0.krpack
.EXAMPLE
    pwsh .\docs\_includes\examples\pwsh\BikeRentalShop\Synchronized\Service.ps1 -Port 5443

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
    pwsh .\docs\_includes\examples\pwsh\BikeRentalShop\Synchronized\Service.ps1 -Port 5443 -AllowedCorsOrigins @('https://127.0.0.1:5445', 'https://localhost:5445')

    Starts the backend with explicit browser origins enabled for a separate web client service.
#>

Initialize-KrRoot -Path $PSScriptRoot

$DataRoot = Join-Path $PSScriptRoot 'data'
$LogsRoot = Join-Path $PSScriptRoot 'logs'
$CertificateRoot = Join-Path $DataRoot 'certs'
$BikeRentalShopRoot = Split-Path -Parent $PSScriptRoot
$SharedCertificateRoot = Join-Path $BikeRentalShopRoot 'certs'

# If the certificate Path doesn't exist, create the directory. The certificate will be created on demand by Get-BikeRentalCertificate and saved to this path for reuse across restarts, so it needs to be writable.
if (-not (Test-Path -Path $CertificateRoot -PathType Container)) {
    New-Item -Path $CertificateRoot -ItemType Directory -Force | Out-Null
}
if (-not (Test-Path -Path $SharedCertificateRoot -PathType Container)) {
    New-Item -Path $SharedCertificateRoot -ItemType Directory -Force | Out-Null
}
$CertificatePassword = 'bike-rental-demo'
$CertificatePath = Join-Path $CertificateRoot 'bike-rental-shop-devcert.pfx'
$RootCertificatePassword = 'bike-rental-shared-root'
$RootCertificatePath = Join-Path $CertificateRoot 'bike-rental-shared-root.pfx'
$ParentRootCertificatePath = Join-Path $SharedCertificateRoot 'bike-rental-shared-root.pfx'


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

# Configure logging, server, middleware, and OpenAPI documentation before defining routes so they're available globally.
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkFile -Path (Join-Path $LogsRoot 'bike-rental-shop.log') -RollingInterval Day |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'DefaultLogger' -SetAsDefault

# Keep split OpenAPI declarations dot-sourced via literal paths so the annotation scanner can discover components.
. "$PSScriptRoot/Private/State.ps1"
. "$PSScriptRoot/Private/OpenApi.ps1"



# Get or create the certificate set before starting the server so it's available for HTTPS configuration and we can fail early if there's an issue with the certificate setup.
$certificateMaterial = Get-BikeRentalCertificate `
    -CertificatePath $CertificatePath `
    -CertificatePassword (ConvertTo-SecureString -String $CertificatePassword -AsPlainText -Force) `
    -RootCertificatePath $RootCertificatePath `
    -ParentRootCertificatePath $ParentRootCertificatePath `
    -RootCertificatePassword (ConvertTo-SecureString -String $RootCertificatePassword -AsPlainText -Force)
$certificate = $certificateMaterial.LeafCertificate
$rootCertificate = $certificateMaterial.RootCertificate
$certificateValidationFailure = ''
if (-not (Test-KrCertificate `
            -Certificate $certificate `
            -CertificateChain $rootCertificate `
            -FailureReasonVariable 'certificateValidationFailure')) {
    Write-Error "Bike rental shop certificate validation failed: $certificateValidationFailure"
    exit 1
}

$BikeRentalStateStore = Initialize-BikeRentalStorage

$routesPath = Join-Path $PSScriptRoot 'Private/Routes.ps1'
if (-not (Test-Path -LiteralPath $routesPath -PathType Leaf)) {
    Write-Error 'Required service file not found: Private/Routes.ps1'
    exit 1
}
# The service descriptor is defined in the .psd1 file with the same base name as this
# script, so it can be automatically discovered by Kestrun when packaging.



New-KrServer -Name 'Riverside Bike Rental'
Set-KrServerOptions -DenyServerHeader
Set-KrServerLimit `
    -MaxRequestBodySize 1048576 `
    -MaxConcurrentConnections 200 `
    -MaxRequestHeaderCount 100 `
    -KeepAliveTimeoutSeconds 120
Add-KrEndpoint `
    -Port $Port `
    -IPAddress $IPAddress `
    -X509Certificate $certificate `
    -Protocols Http1AndHttp2AndHttp3
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
}

Add-KrOpenApiInfo `
    -Title 'Riverside Bike Rental API' `
    -Version '1.0.0' `
    -Description 'Bike rental service bundle example with HTTPS, OpenAPI, staff authentication, persistent data, and optional CORS for an external web client.'
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
