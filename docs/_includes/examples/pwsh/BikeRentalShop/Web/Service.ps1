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
    pwsh .\docs\_includes\examples\pwsh\BikeRentalShop\Synchronized\Service.ps1 -Port 5443 -AllowedCorsOrigins @('https://127.0.0.1:5445', 'https://localhost:5445')
    pwsh .\docs\_includes\examples\pwsh\BikeRentalShop\Web\Service.ps1 -Port 5445 -Backend Synchronized

    Starts the standalone web client against the synchronized backend sample.
.EXAMPLE
    pwsh .\docs\_includes\examples\pwsh\BikeRentalShop\Concurrent\Service.ps1 -Port 5444 -AllowedCorsOrigins @('https://127.0.0.1:5445', 'https://localhost:5445')
    pwsh .\docs\_includes\examples\pwsh\BikeRentalShop\Web\Service.ps1 -Port 5445 -Backend Concurrent

    Starts the standalone web client against the concurrent backend sample.
.EXAMPLE
    pwsh .\docs\_includes\examples\pwsh\BikeRentalShop\Web\Service.ps1 -Port 5445 -Backend Custom -ApiBaseUrl 'https://api.example.test:9443'

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
.PARAMETER RootCertificatePath
.PARAMETER ParentRootCertificatePath
    The PFX file path used as the shared fallback copy under the BikeRentalShop folder.
.PARAMETER RootCertificatePassword
    The password used to import or export the development root certificate.
.OUTPUTS
    PSCustomObject containing LeafCertificate, RootCertificate, and PublicRootCertificate.
.EXAMPLE
    Get-BikeRentalCertificate -CertificatePath '.\data\certs\bike-rental-shop-web-devcert.pfx' `
        -CertificatePassword $password `
        -RootCertificatePath '.\data\certs\bike-rental-shared-root.pfx' `
        -ParentRootCertificatePath '..\certs\bike-rental-shared-root.pfx' `
        -RootCertificatePassword $rootPassword

    Returns the existing certificate set or creates a new local root, plus a shared parent copy, if needed.
#>
function Get-BikeRentalCertificate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CertificatePath,
        [Parameter(Mandatory = $true)]
        [SecureString]$CertificatePassword,
        [Parameter(Mandatory = $true)]
        [string]$RootCertificatePath,
        [string]$ParentRootCertificatePath,
        [Parameter(Mandatory = $true)]
        [SecureString]$RootCertificatePassword
    )

    $rootCertificate = $null
    $publicRootCertificate = $null
    if (Test-Path -LiteralPath $RootCertificatePath -PathType Leaf) {
        $rootCertificate = Import-KrCertificate -FilePath $RootCertificatePath -Password $RootCertificatePassword
        $publicRootCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($rootCertificate.RawData)
    } elseif (-not [string]::IsNullOrWhiteSpace($ParentRootCertificatePath) -and (Test-Path -LiteralPath $ParentRootCertificatePath -PathType Leaf)) {
        $parentRootCertificate = Import-KrCertificate -FilePath $ParentRootCertificatePath -Password $RootCertificatePassword
        Export-KrCertificate -Certificate $parentRootCertificate -FilePath ([System.IO.Path]::ChangeExtension($RootCertificatePath, $null)) `
            -Format pfx -IncludePrivateKey -Password $RootCertificatePassword
        $parentRootCertificate.Dispose()
        $rootCertificate = Import-KrCertificate -FilePath $RootCertificatePath -Password $RootCertificatePassword
        $publicRootCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($rootCertificate.RawData)
    }

    if ($rootCertificate -and (Test-Path -LiteralPath $CertificatePath -PathType Leaf)) {
        $leafCertificate = Import-KrCertificate -FilePath $CertificatePath -Password $CertificatePassword
        if ($leafCertificate.Issuer -eq $rootCertificate.Subject) {
            return [pscustomobject]@{
                LeafCertificate = $leafCertificate
                RootCertificate = $rootCertificate
                PublicRootCertificate = $publicRootCertificate
            }
        }

        $leafCertificate.Dispose()
    }

    $bundleParameters = @{
        DnsNames = 'localhost', '127.0.0.1', '::1'
        Exportable = $true
        LeafValidDays = 30
        RootValidDays = 3650
    }

    if ($rootCertificate) {
        $bundleParameters.RootCertificate = $rootCertificate
    }

    $bundle = New-KrDevelopmentCertificate @bundleParameters

    if (-not $rootCertificate) {
        $rootCertificate = $bundle.RootCertificate
        $publicRootCertificate = $bundle.PublicRootCertificate
        Export-KrCertificate -Certificate $rootCertificate -FilePath ([System.IO.Path]::ChangeExtension($RootCertificatePath, $null)) `
            -Format pfx -IncludePrivateKey -Password $RootCertificatePassword

        if (-not [string]::IsNullOrWhiteSpace($ParentRootCertificatePath)) {
            Export-KrCertificate -Certificate $rootCertificate -FilePath ([System.IO.Path]::ChangeExtension($ParentRootCertificatePath, $null)) `
                -Format pfx -IncludePrivateKey -Password $RootCertificatePassword
        }
    }

    $certificate = $bundle.LeafCertificate
    Export-KrCertificate -Certificate $certificate -FilePath ([System.IO.Path]::ChangeExtension($CertificatePath, $null)) `
        -Format pfx -IncludePrivateKey -Password $CertificatePassword

    return [pscustomobject]@{
        LeafCertificate = $certificate
        RootCertificate = $rootCertificate
        PublicRootCertificate = $publicRootCertificate
    }
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
$BikeRentalShopRoot = Split-Path -Parent $PSScriptRoot
$SharedCertificateRoot = Join-Path $BikeRentalShopRoot 'certs'

if (-not (Test-Path -Path $CertificateRoot -PathType Container)) {
    New-Item -Path $CertificateRoot -ItemType Directory -Force | Out-Null
}

if (-not (Test-Path -Path $SharedCertificateRoot -PathType Container)) {
    New-Item -Path $SharedCertificateRoot -ItemType Directory -Force | Out-Null
}

$CertificatePassword = 'bike-rental-web-demo'
$CertificatePath = Join-Path $CertificateRoot 'bike-rental-shop-web-devcert.pfx'
$RootCertificatePassword = 'bike-rental-shared-root'
$RootCertificatePath = Join-Path $CertificateRoot 'bike-rental-shared-root.pfx'
$ParentRootCertificatePath = Join-Path $SharedCertificateRoot 'bike-rental-shared-root.pfx'

# These variables are intentionally kept in script scope because the sibling Razor page model
# scripts consume them directly when building per-request page data.
$BikeRentalApiBaseUrl = Resolve-BikeRentalBackendUrl -Backend $Backend -ApiBaseUrl $ApiBaseUrl
$BikeRentalBackendLabel = if ($Backend -eq 'Custom') { 'Custom backend' } else { $Backend }
$BikeRentalBackendDocsUrl = "$BikeRentalApiBaseUrl/docs/swagger"
$BikeRentalBackendOpenApiUrl = "$BikeRentalApiBaseUrl/openapi/v3.1/openapi.json"
$BikeRentalRootCertificateRoute = '/certificates/root.pem'
$RootCertificatePublicPath = Join-Path $CertificateRoot 'bike-rental-shared-root-public.pem'

# Materialize the Razor-shared values once here so editor analysis can see the local reads while
# the sibling page model scripts continue consuming the original script-scoped variables.
$null = [pscustomobject]@{
    StaffApiKey = $StaffApiKey
    BackendLabel = $BikeRentalBackendLabel
    BackendDocsUrl = $BikeRentalBackendDocsUrl
    BackendOpenApiUrl = $BikeRentalBackendOpenApiUrl
    RootCertificateUrl = $BikeRentalRootCertificateRoute
}

# Configure logging, server, middleware, and OpenAPI documentation before defining routes so they're available globally.
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkFile -Path (Join-Path $LogsRoot 'bike-rental-shop-web.log') -RollingInterval Day |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'DefaultLogger' -SetAsDefault

$certificateMaterial = Get-BikeRentalCertificate `
    -CertificatePath $CertificatePath `
    -CertificatePassword (ConvertTo-SecureString -String $CertificatePassword -AsPlainText -Force) `
    -RootCertificatePath $RootCertificatePath `
    -ParentRootCertificatePath $ParentRootCertificatePath `
    -RootCertificatePassword (ConvertTo-SecureString -String $RootCertificatePassword -AsPlainText -Force)
$certificate = $certificateMaterial.LeafCertificate
$rootCertificate = $certificateMaterial.RootCertificate
$publicRootCertificate = $certificateMaterial.PublicRootCertificate
$certificateValidationFailure = ''
if (-not (Test-KrCertificate -Certificate $certificate -CertificateChain $rootCertificate -FailureReasonVariable 'certificateValidationFailure')) {
    Write-Error "Bike rental web client certificate validation failed: $certificateValidationFailure"
    exit 1
}

Export-KrCertificate -Certificate $publicRootCertificate -FilePath ([System.IO.Path]::ChangeExtension($RootCertificatePublicPath, $null)) -Format Pem

# Create the server and configure global middleware before defining routes so they're available globally.
New-KrServer -Name 'Riverside Bike Rental Web'
Set-KrServerOptions -DenyServerHeader
Set-KrServerLimit -MaxRequestBodySize 1048576 -MaxConcurrentConnections 200 -MaxRequestHeaderCount 100 -KeepAliveTimeoutSeconds 120
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -X509Certificate $certificate -Protocols Http1AndHttp2AndHttp3
Add-KrCompressionMiddleware -EnableForHttps -MimeTypes @('text/html', 'text/css', 'application/javascript', 'application/json', 'text/plain')
Add-KrFaviconMiddleware
Add-KrPowerShellRazorPagesRuntime
Add-KrStaticFilesMiddleware -RequestPath '/static'

# Enable-KrConfiguration so it is available to every route runspace.
Enable-KrConfiguration

# Serve the two frontend assets explicitly so the sample remains predictable even when hosted
# from a packaged service folder.
Add-KrMapRoute -Verbs Get -Pattern '/static/site.css' -ScriptBlock {
    Write-KrFileResponse -FilePath './wwwroot/site.css' -ContentType 'text/css'
}

Add-KrMapRoute -Verbs Get -Pattern '/static/app.js' -ScriptBlock {
    Write-KrFileResponse -FilePath './wwwroot/app.js' -ContentType 'application/javascript'
}

Add-KrMapRoute -Verbs Get -Pattern $BikeRentalRootCertificateRoute -ScriptBlock {
    Write-KrFileResponse -FilePath './data/certs/bike-rental-shared-root-public.pem' -ContentType 'application/x-pem-file'
}

Write-KrLog -Level Information -Message 'Bike rental web client ready on https://{address}:{port} targeting {backendUrl}' -Values $IPAddress.IPAddressToString, $Port, $BikeRentalApiBaseUrl
Start-KrServer -CloseLogsOnExit
