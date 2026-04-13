[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', '')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param(
    [int]$Port = $env:PORT ?? 5445,
    [IPAddress]$IPAddress = [IPAddress]::Loopback,
    [ValidateSet('Synchronized', 'Concurrent', 'Custom')]
    [string]$Backend = 'Synchronized',
    [string]$ApiBaseUrl,
    [string]$StaffApiKey = 'bike-shop-demo-key'
)

# Allow the test harness and packaged environments to override startup settings without
# rewriting the script or passing every value on the command line.
if ((-not $PSBoundParameters.ContainsKey('Backend')) -and -not [string]::IsNullOrWhiteSpace($env:BIKE_RENTAL_WEB_BACKEND)) {
    $Backend = $env:BIKE_RENTAL_WEB_BACKEND
}

if ((-not $PSBoundParameters.ContainsKey('ApiBaseUrl')) -and -not [string]::IsNullOrWhiteSpace($env:BIKE_RENTAL_API_BASE_URL)) {
    $ApiBaseUrl = $env:BIKE_RENTAL_API_BASE_URL
}

if ((-not $PSBoundParameters.ContainsKey('StaffApiKey')) -and -not [string]::IsNullOrWhiteSpace($env:BIKE_RENTAL_STAFF_API_KEY)) {
    $StaffApiKey = $env:BIKE_RENTAL_STAFF_API_KEY
}

<#
.SYNOPSIS
    Package-ready bike rental web client.
.DESCRIPTION
    Demonstrates a standalone Kestrun PowerShell Razor Pages service that talks to the bike rental
    API over HTTP. The web client stays separate from the backend samples so browser concerns such
    as static assets, page composition, and cross-origin calls do not leak into either API variant.
.EXAMPLE
    pwsh .\examples\PowerShell\BikeRentalShop\Synchronized\Service.ps1 -Port 5443 -AllowedCorsOrigins @('https://127.0.0.1:5445', 'https://localhost:5445')
    pwsh .\examples\PowerShell\BikeRentalShop\Web\Service.ps1 -Port 5445 -Backend Synchronized

    Starts the standalone web client against the synchronized backend sample.
.EXAMPLE
    pwsh .\examples\PowerShell\BikeRentalShop\Concurrent\Service.ps1 -Port 5444 -AllowedCorsOrigins @('https://127.0.0.1:5445', 'https://localhost:5445')
    pwsh .\examples\PowerShell\BikeRentalShop\Web\Service.ps1 -Port 5445 -Backend Concurrent

    Starts the standalone web client against the concurrent backend sample.
.EXAMPLE
    pwsh .\examples\PowerShell\BikeRentalShop\Web\Service.ps1 -Port 5445 -Backend Custom -ApiBaseUrl 'https://api.example.test:9443'

    Points the web client at a custom bike rental backend URL.
#>

<#
.SYNOPSIS
    Resolves the backend base URL for the standalone web client.
.DESCRIPTION
    Returns the caller-provided API base URL when one is supplied. Otherwise it maps the
    selected backend profile to the corresponding local sample endpoint.
.PARAMETER Backend
    The backend profile name selected for the web client.
.PARAMETER ApiBaseUrl
    An optional explicit backend URL that overrides the built-in profile mapping.
.OUTPUTS
    System.String
.EXAMPLE
    Resolve-BikeRentalBackendUrl -Backend Synchronized

    Returns the default synchronized backend URL.
.EXAMPLE
    Resolve-BikeRentalBackendUrl -Backend Custom -ApiBaseUrl 'https://api.example.test:9443'

    Returns the explicit custom backend URL.
#>
function Resolve-BikeRentalBackendUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Backend,
        [string]$ApiBaseUrl
    )

    if (-not [string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
        return $ApiBaseUrl.TrimEnd('/')
    }

    switch ($Backend) {
        'Synchronized' {
            return 'https://127.0.0.1:5443'
        }
        'Concurrent' {
            return 'https://127.0.0.1:5444'
        }
        default {
            throw 'ApiBaseUrl is required when Backend is set to Custom.'
        }
    }
}

<#
.SYNOPSIS
    Loads or creates the HTTPS certificate used by the web client.
.DESCRIPTION
    Reuses a previously exported development certificate when one exists at the configured path.
    If not, it creates a new self-signed certificate for localhost and 127.0.0.1, exports it as a
    PFX file, and returns the in-memory certificate for listener configuration.
.PARAMETER CertificatePath
    The PFX file path used to persist the generated development certificate.
.PARAMETER CertificatePassword
    The password used to import or export the PFX file.
.OUTPUTS
    System.Security.Cryptography.X509Certificates.X509Certificate2
.EXAMPLE
    Get-BikeRentalCertificate -CertificatePath '.\data\certs\bike-rental-shop-web-devcert.pfx' -CertificatePassword $password

    Returns the existing certificate or creates a new one if needed.
#>
function Get-BikeRentalCertificate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CertificatePath,

        [Parameter(Mandatory = $true)]
        [SecureString]$CertificatePassword
    )

    if (Test-Path -LiteralPath $CertificatePath -PathType Leaf) {
        return Import-KrCertificate -FilePath $CertificatePath -Password $CertificatePassword
    }

    $certificate = New-KrSelfSignedCertificate -DnsNames @('localhost', '127.0.0.1') -Exportable
    Export-KrCertificate -Certificate $certificate -FilePath ([System.IO.Path]::ChangeExtension($CertificatePath, $null)) `
        -Format pfx -IncludePrivateKey -Password $CertificatePassword
    return $certificate
}

# Import Kestrun from the repository when developing locally and fall back to the installed
# module when the sample is executed from a package or a machine without the source tree.
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

# Keep all runtime artifacts under the sample folder so the web client can be packaged cleanly
# and restarted without depending on machine-wide paths.
$DataRoot = Join-Path $PSScriptRoot 'data'
$LogsRoot = Join-Path $PSScriptRoot 'logs'
$CertificateRoot = Join-Path $DataRoot 'certs'

if (-not (Test-Path -Path $CertificateRoot -PathType Container)) {
    New-Item -Path $CertificateRoot -ItemType Directory -Force | Out-Null
}

$CertificatePassword = 'bike-rental-web-demo'
$CertificatePath = Join-Path $CertificateRoot 'bike-rental-shop-web-devcert.pfx'

# These variables are intentionally kept in script scope because the sibling Razor page model
# scripts consume them directly when building per-request page data.
$BikeRentalApiBaseUrl = Resolve-BikeRentalBackendUrl -Backend $Backend -ApiBaseUrl $ApiBaseUrl
$BikeRentalBackendLabel = if ($Backend -eq 'Custom') { 'Custom backend' } else { $Backend }
$BikeRentalBackendDocsUrl = "$BikeRentalApiBaseUrl/docs/swagger"
$BikeRentalBackendOpenApiUrl = "$BikeRentalApiBaseUrl/openapi/v3.1/openapi.json"

# Materialize the Razor-shared values once here so editor analysis can see the local reads while
# the sibling page model scripts continue consuming the original script-scoped variables.
$null = [pscustomobject]@{
    StaffApiKey = $StaffApiKey
    BackendLabel = $BikeRentalBackendLabel
    BackendDocsUrl = $BikeRentalBackendDocsUrl
    BackendOpenApiUrl = $BikeRentalBackendOpenApiUrl
}

$certificate = Get-BikeRentalCertificate -CertificatePath $CertificatePath -CertificatePassword (ConvertTo-SecureString -String $CertificatePassword -AsPlainText -Force)
if (-not (Test-KrCertificate -Certificate $certificate)) {
    Write-Error 'Bike rental web client certificate validation failed.'
    exit 1
}

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkFile -Path (Join-Path $LogsRoot 'bike-rental-shop-web.log') -RollingInterval Day |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'DefaultLogger' -SetAsDefault

New-KrServer -Name 'Riverside Bike Rental Web'
Set-KrServerOptions -DenyServerHeader
Set-KrServerLimit -MaxRequestBodySize 1048576 -MaxConcurrentConnections 200 -MaxRequestHeaderCount 100 -KeepAliveTimeoutSeconds 120
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -X509Certificate $certificate -Protocols Http1AndHttp2AndHttp3
Add-KrCompressionMiddleware -EnableForHttps -MimeTypes @('text/html', 'text/css', 'application/javascript', 'application/json', 'text/plain')
Add-KrFaviconMiddleware
Add-KrPowerShellRazorPagesRuntime
Add-KrStaticFilesMiddleware -RequestPath '/static'

Enable-KrConfiguration

# Serve the two frontend assets explicitly so the sample remains predictable even when hosted
# from a packaged service folder.
Add-KrMapRoute -Verbs Get -Pattern '/static/site.css' -ScriptBlock {
    Write-KrFileResponse -FilePath './wwwroot/site.css' -ContentType 'text/css'
}

Add-KrMapRoute -Verbs Get -Pattern '/static/app.js' -ScriptBlock {
    Write-KrFileResponse -FilePath './wwwroot/app.js' -ContentType 'application/javascript'
}

Write-KrLog -Level Information -Message 'Bike rental web client ready on https://{address}:{port} targeting {backendUrl}' -Values $IPAddress.IPAddressToString, $Port, $BikeRentalApiBaseUrl
Start-KrServer -CloseLogsOnExit
