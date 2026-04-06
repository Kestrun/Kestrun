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

$script:DataRoot = Join-Path $PSScriptRoot 'data'
$script:LogsRoot = Join-Path $PSScriptRoot 'logs'
$script:CertificateRoot = Join-Path $script:DataRoot 'certs'
$script:StatePath = Join-Path $script:DataRoot 'bike-rental-state.json'
$script:CertificatePath = Join-Path $script:CertificateRoot 'bike-rental-shop-devcert.pfx'
$script:CertificatePassword = 'bike-rental-demo'
$script:StaffScheme = 'BikeRentalStaffApiKey'
$script:StaffApiKey = 'bike-shop-demo-key'
$script:StateMutex = [System.Threading.Mutex]::new($false, 'Kestrun.BikeRentalShop.State')

function Invoke-BikeRentalStateLock {
    param([scriptblock]$Action)

    $lockTaken = $false
    try {
        $lockTaken = $script:StateMutex.WaitOne([TimeSpan]::FromSeconds(15))
        if (-not $lockTaken) {
            throw 'Timed out waiting for the bike rental state file lock.'
        }

        & $Action
    } finally {
        if ($lockTaken) {
            [void]$script:StateMutex.ReleaseMutex()
        }
    }
}

function Get-BikeRentalDefaultState {
    [ordered]@{
        shopName = 'Riverside Bike Rental'
        currency = 'USD'
        bikes = @(
            [ordered]@{
                bikeId = 'bk-100'
                model = 'City Loop 3'
                type = 'city'
                hourlyRate = 12.00
                status = 'available'
                dock = 'front-window'
                lastServiceDate = '2026-03-10'
                currentRentalId = $null
            }
            [ordered]@{
                bikeId = 'bk-205'
                model = 'Trail Runner X'
                type = 'mountain'
                hourlyRate = 18.50
                status = 'available'
                dock = 'service-bay'
                lastServiceDate = '2026-03-19'
                currentRentalId = $null
            }
            [ordered]@{
                bikeId = 'bk-310'
                model = 'Metro Glide Hybrid'
                type = 'hybrid'
                hourlyRate = 15.25
                status = 'available'
                dock = 'north-rack'
                lastServiceDate = '2026-03-28'
                currentRentalId = $null
            }
            [ordered]@{
                bikeId = 'bk-402'
                model = 'Coastline E-Bike'
                type = 'electric'
                hourlyRate = 24.00
                status = 'available'
                dock = 'charging-wall'
                lastServiceDate = '2026-04-01'
                currentRentalId = $null
            }
        )
        rentals = @()
        lastUpdatedUtc = (Get-Date).ToUniversalTime().ToString('o')
    }
}

function Save-BikeRentalStateUnsafe {
    param([hashtable]$State)

    $State['lastUpdatedUtc'] = (Get-Date).ToUniversalTime().ToString('o')
    $json = $State | ConvertTo-Json -Depth 8
    Set-Content -LiteralPath $script:StatePath -Value $json -Encoding utf8NoBOM
}

function Read-BikeRentalStateUnsafe {
    if (-not (Test-Path -LiteralPath $script:StatePath -PathType Leaf)) {
        Save-BikeRentalStateUnsafe -State (Get-BikeRentalDefaultState)
    }

    return Get-Content -LiteralPath $script:StatePath -Raw | ConvertFrom-Json -AsHashtable
}

function Initialize-BikeRentalStorage {
    foreach ($path in @($script:DataRoot, $script:LogsRoot, $script:CertificateRoot)) {
        if (-not (Test-Path -LiteralPath $path)) {
            $null = New-Item -ItemType Directory -Path $path -Force
        }
    }

    Invoke-BikeRentalStateLock {
        if (-not (Test-Path -LiteralPath $script:StatePath -PathType Leaf)) {
            Save-BikeRentalStateUnsafe -State (Get-BikeRentalDefaultState)
        }
    }
}

function Get-BikeRentalState {
    Invoke-BikeRentalStateLock {
        Read-BikeRentalStateUnsafe
    }
}

function Update-BikeRentalState {
    param([scriptblock]$Mutation)

    Invoke-BikeRentalStateLock {
        $state = Read-BikeRentalStateUnsafe
        $result = & $Mutation $state
        Save-BikeRentalStateUnsafe -State $state
        return $result
    }
}

function Write-BikeRentalError {
    param(
        [int]$StatusCode,
        [string]$Message,
        [hashtable]$Details = @{}
    )

    $payload = [ordered]@{
        status = $StatusCode
        error = $Message
        path = [string]$Context.Request.Path
        timestamp = (Get-Date).ToUniversalTime().ToString('o')
    }

    foreach ($key in $Details.Keys) {
        $payload[$key] = $Details[$key]
    }

    Write-KrJsonResponse -InputObject $payload -StatusCode $StatusCode
}

function New-BikeCatalogItemObject {
    param([hashtable]$Bike)

    return [BikeCatalogItem]@{
        bikeId = [string]$Bike['bikeId']
        model = [string]$Bike['model']
        type = [BikeType]$Bike['type']
        hourlyRate = [double]$Bike['hourlyRate']
        status = [string]$Bike['status']
        dock = [string]$Bike['dock']
    }
}

function New-RentalStatusObject {
    param(
        [hashtable]$Rental,
        [hashtable]$Bike
    )

    return [RentalStatusResponse]@{
        rentalId = [string]$Rental['rentalId']
        bikeId = [string]$Rental['bikeId']
        customerName = [string]$Rental['customerName']
        bikeModel = [string]$Bike['model']
        status = [string]$Rental['status']
        pickupCode = [string]$Rental['pickupCode']
        startedAtUtc = [string]$Rental['startedAtUtc']
        dueAtUtc = [string]$Rental['dueAtUtc']
        returnedAtUtc = [string]$Rental['returnedAtUtc']
        totalEstimate = [double]$Rental['totalEstimate']
    }
}

function Get-BikeRentalCertificate {
    if (Test-Path -LiteralPath $script:CertificatePath -PathType Leaf) {
        return Import-KrCertificate -FilePath $script:CertificatePath -Password (ConvertTo-SecureString -String $script:CertificatePassword -AsPlainText -Force)
    }

    $certificate = New-KrSelfSignedCertificate -DnsNames @('localhost', '127.0.0.1') -Exportable
    Export-KrCertificate -Certificate $certificate -FilePath ([System.IO.Path]::ChangeExtension($script:CertificatePath, $null)) -Format pfx -IncludePrivateKey -Password (ConvertTo-SecureString -String $script:CertificatePassword -AsPlainText -Force)
    return $certificate
}

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

Add-KrApiKeyAuthentication -AuthenticationScheme $script:StaffScheme -ApiKeyName 'X-Api-Key' -StaticApiKey $script:StaffApiKey

Add-KrOpenApiInfo -Title 'Riverside Bike Rental API' -Version '1.0.0' -Description 'Bike rental service bundle example with HTTPS, OpenAPI, staff authentication, and persistent data.'
#Add-KrOpenApiServer -Url ("https://{0}:{1}" -f $IPAddress.IPAddressToString, $Port) -Description 'Local HTTPS endpoint'


enum BikeType {
    city
    mountain
    hybrid
    electric
}



[OpenApiSchemaComponent(Description = 'Bike available in the rental catalog.', RequiredProperties = ('bikeId', 'model', 'type', 'hourlyRate', 'status', 'dock'))]
class BikeCatalogItem {
    [OpenApiProperty(Description = 'Unique bike identifier.', Example = 'bk-100')]
    [string]$bikeId

    [OpenApiProperty(Description = 'Display model for the bike.', Example = 'City Loop 3')]
    [string]$model

    [OpenApiProperty(Description = 'Rental category.', Example = 'city')]
    [BikeType]$type

    [OpenApiProperty(Description = 'Hourly rental rate in USD.', Example = 12.0)]
    [double]$hourlyRate

    [OpenApiProperty(Description = 'Current availability status.', Example = 'available')]
    [string]$status

    [OpenApiProperty(Description = 'Current storage dock in the shop.', Example = 'front-window')]
    [string]$dock
}

[OpenApiSchemaComponent(Array = $true, Description = 'List of bikes currently tracked by the shop.')]
class BikeCatalogResponse : BikeCatalogItem {}

[OpenApiSchemaComponent(Description = 'Create a new bike rental.', RequiredProperties = ('bikeId', 'customerName', 'plannedHours'))]
class CreateRentalRequest {
    [OpenApiProperty(Description = 'Bike identifier to rent.', Example = 'bk-100')]
    [string]$bikeId

    [OpenApiProperty(Description = 'Name of the rider or customer.', Example = 'Ava Flores')]
    [string]$customerName

    [OpenApiProperty(Description = 'Contact phone number for pickup coordination.', Example = '+1-202-555-0148')]
    [string]$phone

    [OpenApiProperty(Description = 'Planned rental duration in hours.', Example = 3)]
    [ValidateRange(1, 24)]
    [int]$plannedHours
}

[OpenApiSchemaComponent(Description = 'Confirmation returned after a rental is created.', RequiredProperties = ('rentalId', 'bikeId', 'customerName', 'bikeModel', 'status', 'pickupCode', 'startedAtUtc', 'dueAtUtc', 'totalEstimate'))]
class RentalStatusResponse {
    [OpenApiProperty(Description = 'Rental identifier.', Example = 'rent-6f7d2cae8a8b')]
    [string]$rentalId

    [OpenApiProperty(Description = 'Bike identifier assigned to the rental.', Example = 'bk-100')]
    [string]$bikeId

    [OpenApiProperty(Description = 'Customer display name.', Example = 'Ava Flores')]
    [string]$customerName

    [OpenApiProperty(Description = 'Model name for the rented bike.', Example = 'City Loop 3')]
    [string]$bikeModel

    [OpenApiProperty(Description = 'Rental lifecycle status.', Example = 'active')]
    [string]$status

    [OpenApiProperty(Description = 'Pickup verification code used at the counter.', Example = '418203')]
    [string]$pickupCode

    [OpenApiProperty(Description = 'Rental start time in UTC.', Example = '2026-04-06T15:45:00.0000000Z')]
    [string]$startedAtUtc

    [OpenApiProperty(Description = 'Planned return time in UTC.', Example = '2026-04-06T18:45:00.0000000Z')]
    [string]$dueAtUtc

    [OpenApiProperty(Description = 'Actual return time in UTC when completed.', Example = '2026-04-06T18:31:00.0000000Z')]
    [string]$returnedAtUtc

    [OpenApiProperty(Description = 'Estimated charge for the rental in USD.', Example = 36.0)]
    [double]$totalEstimate
}

[OpenApiSchemaComponent(Description = 'Health snapshot for the bike rental API.', RequiredProperties = ('status', 'shopName', 'availableBikes', 'activeRentals', 'timestamp'))]
class BikeRentalHealthResponse {
    [string]$status
    [string]$shopName
    [int]$availableBikes
    [int]$activeRentals
    [string]$timestamp
}

[OpenApiSchemaComponent(Description = 'Inventory totals shown on the staff dashboard.', RequiredProperties = ('total', 'available', 'rented'))]
class StaffDashboardInventoryResponse {
    [int]$total
    [int]$available
    [int]$rented
}

[OpenApiSchemaComponent(Description = 'Rental summary shown on the staff dashboard.', RequiredProperties = ('active', 'completedToday', 'activeIds'))]
class StaffDashboardRentalsResponse {
    [int]$active
    [int]$completedToday
    [string[]]$activeIds
}

[OpenApiSchemaComponent(Description = 'Authenticated staff dashboard snapshot.', RequiredProperties = ('shopName', 'generatedAtUtc', 'inventory', 'rentals'))]
class StaffDashboardResponse {
    [string]$shopName
    [string]$generatedAtUtc
    [StaffDashboardInventoryResponse]$inventory
    [StaffDashboardRentalsResponse]$rentals
}

[OpenApiSchemaComponent(Description = 'Optional details captured when a bike is returned.')]
class CloseRentalRequest {
    [OpenApiProperty(Description = 'Optional condition or maintenance notes recorded by staff.', Example = 'Rear tire pressure checked and ready for the next rider.')]
    [string]$conditionNotes
}

[OpenApiSchemaComponent(Description = 'Request payload used to add a bike to shop inventory.', RequiredProperties = ('bikeId', 'model', 'type', 'hourlyRate', 'dock'))]
class AddBikeRequest {
    [OpenApiProperty(Description = 'Unique bike identifier.', Example = 'bk-550')]
    [ValidatePattern('^bk-\d{3,}$')]
    [string]$bikeId

    [OpenApiProperty(Description = 'Display model for the bike.', Example = 'Harbor Cruiser 7')]
    [ValidateNotNullOrEmpty()]
    [ValidateLength(3, 80)]
    [string]$model

    [OpenApiProperty(Description = 'Rental category.', Example = 'city')]
    [BikeType]$type

    [OpenApiProperty(Description = 'Hourly rental rate in USD.', Example = 14.5)]
    [ValidateRange(0.01, 500.0)]
    [double]$hourlyRate

    [OpenApiProperty(Description = 'Storage dock or rack location.', Example = 'south-rack')]
    [ValidateNotNullOrEmpty()]
    [ValidateLength(2, 60)]
    [string]$dock

    [OpenApiProperty(Description = 'Last service date in ISO 8601 date format.', Example = '2026-04-06')]
    [string]$lastServiceDate
}

[OpenApiSchemaComponent(Description = 'Standard API error returned by the bike rental service.', RequiredProperties = ('status', 'error', 'path', 'timestamp'))]
class BikeRentalErrorResponse {
    [int]$status
    [string]$error
    [string]$path
    [string]$timestamp
    [string]$bikeId
    [string]$rentalId
    [string]$currentStatus
}

[OpenApiResponseComponent(Description = 'Authenticated staff dashboard returned.', ContentType = 'application/json')]
[StaffDashboardResponse]$StaffDashboardOk = NoDefault

[OpenApiResponseComponent(Description = 'Rental closed and bike returned to inventory.', ContentType = 'application/json')]
[RentalStatusResponse]$StaffReturnRentalOk = NoDefault

[OpenApiResponseComponent(Description = 'Rental was not found.', ContentType = 'application/json')]
[BikeRentalErrorResponse]$StaffReturnRentalNotFound = NoDefault

[OpenApiResponseComponent(Description = 'Rental is already closed.', ContentType = 'application/json')]
[BikeRentalErrorResponse]$StaffReturnRentalConflict = NoDefault

[OpenApiResponseComponent(Description = 'Bike added to inventory.', ContentType = 'application/json')]
[BikeCatalogItem]$StaffAddBikeCreated = NoDefault

[OpenApiResponseComponent(Description = 'Bike payload is invalid.', ContentType = 'application/json')]
[BikeRentalErrorResponse]$StaffAddBikeBadRequest = NoDefault

[OpenApiResponseComponent(Description = 'Bike identifier already exists.', ContentType = 'application/json')]
[BikeRentalErrorResponse]$StaffAddBikeConflict = NoDefault

[OpenApiResponseComponent(Description = 'Bike removed from inventory.', ContentType = 'application/json')]
[BikeCatalogItem]$StaffRemoveBikeOk = NoDefault

[OpenApiResponseComponent(Description = 'Bike was not found.', ContentType = 'application/json')]
[BikeRentalErrorResponse]$StaffRemoveBikeNotFound = NoDefault

[OpenApiResponseComponent(Description = 'Bike cannot be removed while it is rented.', ContentType = 'application/json')]
[BikeRentalErrorResponse]$StaffRemoveBikeConflict = NoDefault

Add-KrMapRoute -Verbs Get -Pattern '/' -AllowAnonymous -ScriptBlock {
    Write-KrJsonResponse -InputObject ([ordered]@{
        service = 'Riverside Bike Rental'
        openApi = '/openapi/v3.1/openapi.json'
        docs = '/docs/swagger'
        publicEndpoints = @('/api/bikes', '/api/rentals', '/api/rentals/{rentalId}', '/api/shop-health')
        staffEndpoints = @('/api/staff/dashboard', '/api/staff/bikes', '/api/staff/bikes/{bikeId}', '/api/staff/rentals/{rentalId}/return')
        demoApiKeyHeader = 'X-Api-Key: bike-shop-demo-key'
        packageCommand = 'New-KrServicePackage -SourceFolder .\examples\PowerShell\BikeRentalShop -OutputPath .\bike-rental-shop-1.0.0.krpack'
    }) -StatusCode 200
}

<#
.SYNOPSIS
    List bikes in the rental catalog.
.DESCRIPTION
    Returns the current bike inventory and supports filtering by availability status and bike type.
.PARAMETER status
    Availability filter for the bike catalog. Use all to return every bike regardless of status.
.PARAMETER type
    Optional bike category filter that narrows the catalog to a single rental type.
#>
function listBikes {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/api/bikes', Tags = 'Catalog')]
    [OpenApiResponse(StatusCode = '200', Description = 'Bike catalog returned.', Schema = [BikeCatalogResponse])]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Query, Example = 'available')]
        [ValidateSet('all', 'available', 'rented')]
        [string]$status = 'available',

        [OpenApiParameter(In = [OaParameterLocation]::Query, Example = 'city')]
        [Nullable[BikeType]]$type
    )

    $state = Get-BikeRentalState
    $bikes = @($state['bikes'])

    if ($status -ne 'all') {
        $bikes = @($bikes | Where-Object { [string]$_['status'] -eq $status })
    }

    if ($null -ne $type) {
        $selectedType = $type.ToString()
        $bikes = @($bikes | Where-Object { [string]$_['type'] -eq $selectedType })
    }

    $response = foreach ($bike in $bikes) {
        New-BikeCatalogItemObject -Bike $bike
    }

    Write-KrJsonResponse -InputObject @($response) -StatusCode 200
}

<#
.SYNOPSIS
    Create a new rental booking.
.DESCRIPTION
    Starts a rental for an available bike, assigns a pickup code, and records the reservation in persisted state.
.PARAMETER Body
    Rental request payload containing the bike selection, customer identity, contact phone, and planned rental hours.
#>
function createRental {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/api/rentals', Tags = 'Rentals')]
    [OpenApiResponse(StatusCode = '201', Description = 'Rental created.', Schema = [RentalStatusResponse])]
    [OpenApiResponse(StatusCode = '404', Description = 'Bike was not found.')]
    [OpenApiResponse(StatusCode = '409', Description = 'Bike is not available.')]
    param(
        [OpenApiRequestBody(Required = $true, ContentType = 'application/json')]
        [CreateRentalRequest]$Body
    )

    $result = Update-BikeRentalState {
        param([hashtable]$State)

        $bike = @($State['bikes']) | Where-Object { [string]$_['bikeId'] -eq $Body.bikeId } | Select-Object -First 1
        if ($null -eq $bike) {
            return [ordered]@{
                StatusCode = 404
                Payload = [ordered]@{
                    error = 'Bike not found.'
                    bikeId = $Body.bikeId
                }
            }
        }

        if ([string]$bike['status'] -ne 'available') {
            return [ordered]@{
                StatusCode = 409
                Payload = [ordered]@{
                    error = 'Bike is not available for checkout.'
                    bikeId = $Body.bikeId
                    currentStatus = [string]$bike['status']
                }
            }
        }

        $now = (Get-Date).ToUniversalTime()
        $due = $now.AddHours($Body.plannedHours)
        $rentalId = 'rent-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
        $pickupCode = (Get-Random -Minimum 100000 -Maximum 999999).ToString()
        $estimate = [Math]::Round(([double]$bike['hourlyRate'] * $Body.plannedHours), 2)

        $rental = [ordered]@{
            rentalId = $rentalId
            bikeId = [string]$bike['bikeId']
            customerName = [string]$Body.customerName
            phone = [string]$Body.phone
            plannedHours = [int]$Body.plannedHours
            startedAtUtc = $now.ToString('o')
            dueAtUtc = $due.ToString('o')
            returnedAtUtc = $null
            pickupCode = $pickupCode
            status = 'active'
            totalEstimate = $estimate
        }

        $bike['status'] = 'rented'
        $bike['currentRentalId'] = $rentalId
        $State['rentals'] = @($State['rentals']) + $rental

        Write-KrLog -Level Information -Message 'Created rental {rentalId} for bike {bikeId}.' -Values $rentalId, $bike['bikeId']

        return [ordered]@{
            StatusCode = 201
            Payload = New-RentalStatusObject -Rental $rental -Bike $bike
        }
    }

    if ($result['StatusCode'] -ne 201) {
        Write-BikeRentalError -StatusCode $result['StatusCode'] -Message $result['Payload']['error'] -Details $result['Payload']
        return
    }

    Write-KrJsonResponse -InputObject $result['Payload'] -StatusCode 201
}

<#
.SYNOPSIS
    Get the status of a rental.
.DESCRIPTION
    Looks up a rental by identifier and returns its current lifecycle state together with the assigned bike details.
.PARAMETER rentalId
    Rental identifier returned when the booking was created.
#>
function getRentalStatus {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/api/rentals/{rentalId}', Tags = 'Rentals')]
    [OpenApiResponse(StatusCode = '200', Description = 'Rental found.', Schema = [RentalStatusResponse])]
    [OpenApiResponse(StatusCode = '404', Description = 'Rental was not found.')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true, Example = 'rent-6f7d2cae8a8b')]
        [string]$rentalId
    )

    $state = Get-BikeRentalState
    $rental = @($state['rentals']) | Where-Object { [string]$_['rentalId'] -eq $rentalId } | Select-Object -First 1
    if ($null -eq $rental) {
        Write-BikeRentalError -StatusCode 404 -Message 'Rental not found.' -Details @{ rentalId = $rentalId }
        return
    }

    $bike = @($state['bikes']) | Where-Object { [string]$_['bikeId'] -eq [string]$rental['bikeId'] } | Select-Object -First 1
    Write-KrJsonResponse -InputObject (New-RentalStatusObject -Rental $rental -Bike $bike) -StatusCode 200
}

<#
.SYNOPSIS
    Report shop API health.
.DESCRIPTION
    Returns a lightweight operational snapshot including shop name, active rentals, and available bike count.
#>
function getBikeRentalHealth {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/api/shop-health', Tags = 'Operations')]
    [OpenApiResponse(StatusCode = '200', Description = 'Health response returned.', Schema = [BikeRentalHealthResponse])]
    param()

    $state = Get-BikeRentalState
    $availableBikes = @($state['bikes'] | Where-Object { [string]$_['status'] -eq 'available' }).Count
    $activeRentals = @($state['rentals'] | Where-Object { [string]$_['status'] -eq 'active' }).Count

    Write-KrJsonResponse -InputObject ([BikeRentalHealthResponse]@{
        status = 'healthy'
        shopName = [string]$state['shopName']
        availableBikes = $availableBikes
        activeRentals = $activeRentals
        timestamp = (Get-Date).ToUniversalTime().ToString('o')
    }) -StatusCode 200
}

<#
.SYNOPSIS
    Get the authenticated staff dashboard.
.DESCRIPTION
    Returns the internal operations dashboard with inventory totals, active rentals, and same-day completion counts.
#>
function getStaffDashboard {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/api/staff/dashboard', Tags = 'Staff')]
    [OpenApiResponseRef(StatusCode = '200', ReferenceId = 'StaffDashboardOk')]
    [OpenApiAuthorization(Scheme = 'BikeRentalStaffApiKey')]
    param()

    $state = Get-BikeRentalState
    $availableCount = @($state['bikes'] | Where-Object { [string]$_['status'] -eq 'available' }).Count
    $rentedCount = @($state['bikes'] | Where-Object { [string]$_['status'] -eq 'rented' }).Count
    $activeRentals = @($state['rentals'] | Where-Object { [string]$_['status'] -eq 'active' })
    $completedToday = @($state['rentals'] | Where-Object {
        -not [string]::IsNullOrWhiteSpace([string]$_['returnedAtUtc']) -and
        ([DateTimeOffset]::Parse([string]$_['returnedAtUtc']).UtcDateTime.Date -eq [DateTime]::UtcNow.Date)
    }).Count

    Write-KrJsonResponse -InputObject ([StaffDashboardResponse]@{
        shopName = [string]$state['shopName']
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        inventory = [StaffDashboardInventoryResponse]@{
            total = @($state['bikes']).Count
            available = $availableCount
            rented = $rentedCount
        }
        rentals = [StaffDashboardRentalsResponse]@{
            active = @($activeRentals).Count
            completedToday = $completedToday
            activeIds = @($activeRentals | ForEach-Object { [string]$_['rentalId'] })
        }
    }) -StatusCode 200
}

<#
.SYNOPSIS
    Add a bike to staff inventory.
.DESCRIPTION
    Creates a new inventory entry for a bike, marks it as available, and persists the updated catalog for future rentals.
.PARAMETER Body
    Bike definition containing the identifier, model, type, hourly rate, dock location, and optional last service date.
#>
function addBike {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/api/staff/bikes', Tags = 'Staff')]
    [OpenApiResponseRef(StatusCode = '201', ReferenceId = 'StaffAddBikeCreated')]
    [OpenApiResponseRef(StatusCode = '400', ReferenceId = 'StaffAddBikeBadRequest')]
    [OpenApiResponseRef(StatusCode = '409', ReferenceId = 'StaffAddBikeConflict')]
    [OpenApiAuthorization(Scheme = 'BikeRentalStaffApiKey')]
    param(
        [OpenApiRequestBody(Required = $true, ContentType = 'application/json')]
        [AddBikeRequest]$Body
    )

    if (-not [string]::IsNullOrWhiteSpace($Body.lastServiceDate)) {
        $parsedDate = [datetime]::MinValue
        if (-not [datetime]::TryParseExact([string]$Body.lastServiceDate, 'yyyy-MM-dd', [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$parsedDate)) {
            Write-BikeRentalError -StatusCode 400 -Message 'Last service date must use yyyy-MM-dd format.' -Details @{ bikeId = $Body.bikeId }
            return
        }
    }

    $result = Update-BikeRentalState {
        param([hashtable]$State)

        $existingBike = @($State['bikes']) | Where-Object { [string]$_['bikeId'] -eq $Body.bikeId } | Select-Object -First 1
        if ($null -ne $existingBike) {
            return [ordered]@{
                StatusCode = 409
                Payload = [ordered]@{
                    error = 'Bike identifier already exists.'
                    bikeId = $Body.bikeId
                    currentStatus = [string]$existingBike['status']
                }
            }
        }

        $lastServiceDate = if ([string]::IsNullOrWhiteSpace($Body.lastServiceDate)) {
            (Get-Date).ToString('yyyy-MM-dd')
        } else {
            [string]$Body.lastServiceDate
        }

        $bike = [ordered]@{
            bikeId = [string]$Body.bikeId
            model = [string]$Body.model
            type = $Body.type.ToString()
            hourlyRate = [double]$Body.hourlyRate
            status = 'available'
            dock = [string]$Body.dock
            lastServiceDate = $lastServiceDate
            currentRentalId = $null
        }

        $State['bikes'] = @($State['bikes']) + $bike

        Write-KrLog -Level Information -Message 'Added bike {bikeId} to inventory.' -Values $bike['bikeId']

        return [ordered]@{
            StatusCode = 201
            Payload = New-BikeCatalogItemObject -Bike $bike
        }
    }

    if ($result['StatusCode'] -ne 201) {
        Write-BikeRentalError -StatusCode $result['StatusCode'] -Message $result['Payload']['error'] -Details $result['Payload']
        return
    }

    Write-KrJsonResponse -InputObject $result['Payload'] -StatusCode 201
}

<#
.SYNOPSIS
    Remove a bike from staff inventory.
.DESCRIPTION
    Deletes a bike from the persisted catalog when it exists and is not currently rented.
.PARAMETER bikeId
    Bike identifier for the inventory entry to remove.
#>
function removeBike {
    [OpenApiPath(HttpVerb = 'delete', Pattern = '/api/staff/bikes/{bikeId}', Tags = 'Staff')]
    [OpenApiResponseRef(StatusCode = '200', ReferenceId = 'StaffRemoveBikeOk')]
    [OpenApiResponseRef(StatusCode = '404', ReferenceId = 'StaffRemoveBikeNotFound')]
    [OpenApiResponseRef(StatusCode = '409', ReferenceId = 'StaffRemoveBikeConflict')]
    [OpenApiAuthorization(Scheme = 'BikeRentalStaffApiKey')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true, Example = 'bk-550')]
        [string]$bikeId
    )

    $result = Update-BikeRentalState {
        param([hashtable]$State)

        $bikes = @($State['bikes'])
        $bike = $bikes | Where-Object { [string]$_['bikeId'] -eq $bikeId } | Select-Object -First 1
        if ($null -eq $bike) {
            return [ordered]@{
                StatusCode = 404
                Payload = [ordered]@{
                    error = 'Bike not found.'
                    bikeId = $bikeId
                }
            }
        }

        if ([string]$bike['status'] -eq 'rented') {
            return [ordered]@{
                StatusCode = 409
                Payload = [ordered]@{
                    error = 'Bike cannot be removed while it is rented.'
                    bikeId = $bikeId
                    currentStatus = [string]$bike['status']
                }
            }
        }

        $State['bikes'] = @($bikes | Where-Object { [string]$_['bikeId'] -ne $bikeId })

        Write-KrLog -Level Information -Message 'Removed bike {bikeId} from inventory.' -Values $bikeId

        return [ordered]@{
            StatusCode = 200
            Payload = New-BikeCatalogItemObject -Bike $bike
        }
    }

    if ($result['StatusCode'] -ne 200) {
        Write-BikeRentalError -StatusCode $result['StatusCode'] -Message $result['Payload']['error'] -Details $result['Payload']
        return
    }

    Write-KrJsonResponse -InputObject $result['Payload'] -StatusCode 200
}

<#
.SYNOPSIS
    Return a rented bike to inventory.
.DESCRIPTION
    Closes an active rental, marks the bike as available again, and optionally stores staff condition notes.
.PARAMETER rentalId
    Rental identifier for the booking being closed.
.PARAMETER Body
    Optional return payload with staff condition notes captured during check-in.
#>
function returnRental {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/api/staff/rentals/{rentalId}/return', Tags = 'Staff')]
    [OpenApiResponseRef(StatusCode = '200', ReferenceId = 'StaffReturnRentalOk')]
    [OpenApiResponseRef(StatusCode = '404', ReferenceId = 'StaffReturnRentalNotFound')]
    [OpenApiResponseRef(StatusCode = '409', ReferenceId = 'StaffReturnRentalConflict')]
    [OpenApiAuthorization(Scheme = 'BikeRentalStaffApiKey')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true, Example = 'rent-6f7d2cae8a8b')]
        [string]$rentalId,

        [OpenApiRequestBody(Required = $false, ContentType = 'application/json')]
        [CloseRentalRequest]$Body
    )

    $conditionNotes = if ($null -ne $Body) { [string]$Body.conditionNotes } else { [string]$null }

    $result = Update-BikeRentalState {
        param([hashtable]$State)

        $rental = @($State['rentals']) | Where-Object { [string]$_['rentalId'] -eq $rentalId } | Select-Object -First 1
        if ($null -eq $rental) {
            return [ordered]@{
                StatusCode = 404
                Payload = [ordered]@{
                    error = 'Rental not found.'
                    rentalId = $rentalId
                }
            }
        }

        if ([string]$rental['status'] -ne 'active') {
            return [ordered]@{
                StatusCode = 409
                Payload = [ordered]@{
                    error = 'Rental is already closed.'
                    rentalId = $rentalId
                    currentStatus = [string]$rental['status']
                }
            }
        }

        $bike = @($State['bikes']) | Where-Object { [string]$_['bikeId'] -eq [string]$rental['bikeId'] } | Select-Object -First 1
        $returnedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        $rental['status'] = 'returned'
        $rental['returnedAtUtc'] = $returnedAtUtc
        $rental['conditionNotes'] = $conditionNotes

        if ($null -ne $bike) {
            $bike['status'] = 'available'
            $bike['currentRentalId'] = $null
        }

        Write-KrLog -Level Information -Message 'Closed rental {rentalId} for bike {bikeId}.' -Values $rentalId, $rental['bikeId']

        return [ordered]@{
            StatusCode = 200
            Payload = New-RentalStatusObject -Rental $rental -Bike $bike
        }
    }

    if ($result['StatusCode'] -ne 200) {
        Write-BikeRentalError -StatusCode $result['StatusCode'] -Message $result['Payload']['error'] -Details $result['Payload']
        return
    }

    Write-KrJsonResponse -InputObject $result['Payload'] -StatusCode 200
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
