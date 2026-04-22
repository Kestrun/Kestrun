<#
.SYNOPSIS
    Provides functions for managing a concurrent bike rental shop database in memory and persisting it to disk.
.DESCRIPTION
    This script initializes a concurrent in-memory database before Kestrun configuration is enabled.
    The top-level database, bike catalog, and rental catalog are all concurrent dictionaries keyed by identifier.
    Route runspaces read directly from those shared dictionaries. Mutations are still wrapped in a Kestrun lock
    so multi-record updates and persistence remain consistent.
.EXAMPLE
    Initialize-BikeRentalStorage
#>
function Get-BikeRentalDefaultState {
    [ordered]@{
        shopName = 'Riverside Bike Rental Concurrent'
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

<#
.SYNOPSIS
    Creates a case-insensitive concurrent dictionary for the bike rental database.
.DESCRIPTION
    This helper returns a new `ConcurrentDictionary[string, object]` configured with
    ordinal case-insensitive key comparison so state records can be accessed consistently
    across route runspaces.
.EXAMPLE
    $dict = New-BikeRentalConcurrentDictionary
#>
function New-BikeRentalConcurrentDictionary {
    return [System.Collections.Concurrent.ConcurrentDictionary[string, object]]::new([System.StringComparer]::OrdinalIgnoreCase)
}

<#
.SYNOPSIS
    Converts a hashtable-style record into a concurrent dictionary record.
.PARAMETER Record
    A bike or rental record represented as an `IDictionary`.
.DESCRIPTION
    This helper copies a plain record into a concurrent dictionary so nested bike and
    rental items can participate in the concurrent in-memory database model.
.EXAMPLE
    $bike = ConvertTo-BikeRentalConcurrentRecord -Record $bikeRecord
#>
function ConvertTo-BikeRentalConcurrentRecord {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$Record
    )

    $converted = New-BikeRentalConcurrentDictionary
    foreach ($entry in $Record.GetEnumerator()) {
        $converted[[string]$entry.Key] = $entry.Value
    }

    return $converted
}

<#
.SYNOPSIS
    Converts a list or map of records into a keyed concurrent dictionary collection.
.PARAMETER Value
    The source collection of records or keyed dictionary.
.PARAMETER IdKey
    The property name used as the stable identifier for each record.
.DESCRIPTION
    This helper normalizes persisted bike and rental collections into concurrent
    dictionaries keyed by bike or rental identifier.
.EXAMPLE
    $bikes = ConvertTo-BikeRentalConcurrentCollection -Value $state.bikes -IdKey 'bikeId'
#>
function ConvertTo-BikeRentalConcurrentCollection {
    param(
        [AllowNull()]
        [object]$Value,

        [Parameter(Mandatory = $true)]
        [string]$IdKey
    )

    $collection = New-BikeRentalConcurrentDictionary
    if ($null -eq $Value) {
        return $collection
    }

    if ($Value -is [System.Collections.IDictionary] -and -not $Value.Contains($IdKey)) {
        foreach ($entry in $Value.GetEnumerator()) {
            $record = ConvertTo-BikeRentalConcurrentRecord -Record ([System.Collections.IDictionary]$entry.Value)
            $collection[[string]$entry.Key] = $record
        }

        return $collection
    }

    foreach ($item in @($Value)) {
        $record = ConvertTo-BikeRentalConcurrentRecord -Record ([System.Collections.IDictionary]$item)
        $collection[[string]$record[$IdKey]] = $record
    }

    return $collection
}

<#
.SYNOPSIS
    Converts a persisted state object into the concurrent in-memory database shape.
.PARAMETER State
    The persisted state to convert.
.DESCRIPTION
    This helper builds the top-level concurrent database and converts the bike and
    rental collections into keyed concurrent dictionaries.
.EXAMPLE
    $database = ConvertTo-BikeRentalConcurrentState -State $persistedState
#>
function ConvertTo-BikeRentalConcurrentState {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$State
    )

    $database = New-BikeRentalConcurrentDictionary
    $database['shopName'] = [string]$State['shopName']
    $database['currency'] = [string]$State['currency']
    $database['lastUpdatedUtc'] = [string]$State['lastUpdatedUtc']
    $database['bikes'] = ConvertTo-BikeRentalConcurrentCollection -Value $State['bikes'] -IdKey 'bikeId'
    $database['rentals'] = ConvertTo-BikeRentalConcurrentCollection -Value $State['rentals'] -IdKey 'rentalId'
    return $database
}

<#
.SYNOPSIS
    Converts the concurrent in-memory database into a serializer-friendly object graph.
.PARAMETER State
    The concurrent in-memory database.
.DESCRIPTION
    This helper materializes bikes and rentals as ordered record arrays so the database
    can be exported to disk with `Export-KrSharedState`.
.EXAMPLE
    $persisted = ConvertFrom-BikeRentalConcurrentState -State $BikeRentalStateStore
#>
function ConvertFrom-BikeRentalConcurrentState {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$State
    )

    $bikes = foreach ($bike in @($State['bikes'].Values)) {
        [ordered]@{
            bikeId = [string]$bike['bikeId']
            model = [string]$bike['model']
            type = [string]$bike['type']
            hourlyRate = [double]$bike['hourlyRate']
            status = [string]$bike['status']
            dock = [string]$bike['dock']
            lastServiceDate = [string]$bike['lastServiceDate']
            currentRentalId = $bike['currentRentalId']
        }
    }

    $rentals = foreach ($rental in @($State['rentals'].Values)) {
        [ordered]@{
            rentalId = [string]$rental['rentalId']
            bikeId = [string]$rental['bikeId']
            customerName = [string]$rental['customerName']
            phone = [string]$rental['phone']
            plannedHours = [int]$rental['plannedHours']
            startedAtUtc = [string]$rental['startedAtUtc']
            dueAtUtc = [string]$rental['dueAtUtc']
            returnedAtUtc = $rental['returnedAtUtc']
            pickupCode = [string]$rental['pickupCode']
            status = [string]$rental['status']
            totalEstimate = [double]$rental['totalEstimate']
            conditionNotes = $rental['conditionNotes']
        }
    }

    return [ordered]@{
        shopName = [string]$State['shopName']
        currency = [string]$State['currency']
        bikes = @($bikes | Sort-Object bikeId)
        rentals = @($rentals | Sort-Object rentalId)
        lastUpdatedUtc = [string]$State['lastUpdatedUtc']
    }
}

<#
.SYNOPSIS
    Saves the current concurrent bike rental state to disk.
.PARAMETER State
    The concurrent bike rental database to persist.
.DESCRIPTION
    This function updates the `lastUpdatedUtc` timestamp and writes a serialized copy
    of the concurrent in-memory database to the configured state file.
.EXAMPLE
    Save-BikeRentalState -State $BikeRentalStateStore
#>
function Save-BikeRentalState {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$State
    )

    $State['lastUpdatedUtc'] = (Get-Date).ToUniversalTime().ToString('o')
    Export-KrSharedState -InputObject (ConvertFrom-BikeRentalConcurrentState -State $State) -Path $StatePath | Out-Null
}

<#
.SYNOPSIS
    Loads the concurrent bike rental state from disk or default seed data.
.DESCRIPTION
    This function reads the current persisted state file when present, falls back to
    the legacy JSON seed file when necessary, and otherwise returns the default shop state.
.EXAMPLE
    $state = Get-BikeRentalPersistedState
#>
function Get-BikeRentalPersistedState {
    if (Test-Path -LiteralPath $StatePath -PathType Leaf) {
        return ConvertTo-BikeRentalConcurrentState -State ([System.Collections.IDictionary](Import-KrSharedState -Path $StatePath))
    }

    if (-not [string]::IsNullOrWhiteSpace($LegacyStatePath) -and (Test-Path -LiteralPath $LegacyStatePath -PathType Leaf)) {
        return ConvertTo-BikeRentalConcurrentState -State (Get-Content -LiteralPath $LegacyStatePath -Raw | ConvertFrom-Json -AsHashtable)
    }

    return ConvertTo-BikeRentalConcurrentState -State (Get-BikeRentalDefaultState)
}

<#
.SYNOPSIS
    Initializes storage folders and returns the concurrent in-memory database.
.DESCRIPTION
    This function creates the required data, log, and certificate folders, loads the
    current persisted state once, persists the initial state when needed, and returns
    the concurrent database object for assignment in `Service.ps1`.
.EXAMPLE
    $BikeRentalStateStore = Initialize-BikeRentalStorage
#>
function Initialize-BikeRentalStorage {
    foreach ($path in @($DataRoot, $LogsRoot, $CertificateRoot)) {
        if (-not (Test-Path -LiteralPath $path)) {
            $null = New-Item -ItemType Directory -Path $path -Force
        }
    }

    $stateStore = Get-BikeRentalPersistedState
    if (-not (Test-Path -LiteralPath $StatePath -PathType Leaf)) {
        Save-BikeRentalState -State $stateStore
    }

    return $stateStore
}

<#
.SYNOPSIS
    Returns the initialized concurrent bike rental database.
.DESCRIPTION
    This helper exposes the shared in-memory database created during service startup
    and throws if startup did not complete.
.EXAMPLE
    $state = Get-BikeRentalState
#>
function Get-BikeRentalState {
    if ($null -eq $BikeRentalStateStore) {
        throw 'Bike rental concurrent state has not been initialized.'
    }

    return $BikeRentalStateStore
}

<#
.SYNOPSIS
    Returns all bike records from the concurrent database.
.PARAMETER State
    The concurrent bike rental database.
.DESCRIPTION
    This helper returns the current bike dictionary values as an array for filtering,
    projection, and response shaping inside routes.
.EXAMPLE
    $bikes = Get-BikeRentalBikes -State $state
#>
function Get-BikeRentalBikes {
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary]$State)
    return @($State['bikes'].Values)
}

<#
.SYNOPSIS
    Returns all rental records from the concurrent database.
.PARAMETER State
    The concurrent bike rental database.
.DESCRIPTION
    This helper returns the current rental dictionary values as an array for filtering,
    aggregation, and response shaping inside routes.
.EXAMPLE
    $rentals = Get-BikeRentalRentals -State $state
#>
function Get-BikeRentalRentals {
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary]$State)
    return @($State['rentals'].Values)
}

<#
.SYNOPSIS
    Looks up a bike record by identifier in the concurrent database.
.PARAMETER State
    The concurrent bike rental database.
.PARAMETER BikeId
    The bike identifier to retrieve.
.DESCRIPTION
    This helper performs a keyed lookup against the concurrent bike catalog and returns
    the matching bike record when it exists.
.EXAMPLE
    $bike = Get-BikeRentalBikeById -State $state -BikeId 'bk-100'
#>
function Get-BikeRentalBikeById {
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$State,
        [Parameter(Mandatory = $true)][string]$BikeId
    )

    $bike = $null
    if ($State['bikes'].TryGetValue($BikeId, [ref]$bike)) {
        return $bike
    }

    return $null
}

<#
.SYNOPSIS
    Looks up a rental record by identifier in the concurrent database.
.PARAMETER State
    The concurrent bike rental database.
.PARAMETER RentalId
    The rental identifier to retrieve.
.DESCRIPTION
    This helper performs a keyed lookup against the concurrent rental catalog and returns
    the matching rental record when it exists.
.EXAMPLE
    $rental = Get-BikeRentalRentalById -State $state -RentalId 'rent-abc123'
#>
function Get-BikeRentalRentalById {
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$State,
        [Parameter(Mandatory = $true)][string]$RentalId
    )

    $rental = $null
    if ($State['rentals'].TryGetValue($RentalId, [ref]$rental)) {
        return $rental
    }

    return $null
}

<#
.SYNOPSIS
    Adds a bike record to the concurrent bike catalog.
.PARAMETER State
    The concurrent bike rental database.
.PARAMETER Bike
    The bike record to add.
.DESCRIPTION
    This helper inserts the provided bike record into the concurrent bike catalog using
    the bike identifier as the key.
.EXAMPLE
    $null = Add-BikeRentalBikeRecord -State $state -Bike $bike
#>
function Add-BikeRentalBikeRecord {
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$State,
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$Bike
    )

    return $State['bikes'].TryAdd([string]$Bike['bikeId'], $Bike)
}

<#
.SYNOPSIS
    Adds a rental record to the concurrent rental catalog.
.PARAMETER State
    The concurrent bike rental database.
.PARAMETER Rental
    The rental record to add.
.DESCRIPTION
    This helper inserts the provided rental record into the concurrent rental catalog
    using the rental identifier as the key.
.EXAMPLE
    $null = Add-BikeRentalRentalRecord -State $state -Rental $rental
#>
function Add-BikeRentalRentalRecord {
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$State,
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$Rental
    )

    return $State['rentals'].TryAdd([string]$Rental['rentalId'], $Rental)
}

<#
.SYNOPSIS
    Removes a bike record from the concurrent bike catalog.
.PARAMETER State
    The concurrent bike rental database.
.PARAMETER BikeId
    The bike identifier to remove.
.DESCRIPTION
    This helper removes the matching bike record from the concurrent bike catalog and
    returns the removed bike when it existed.
.EXAMPLE
    $removedBike = Remove-BikeRentalBikeRecord -State $state -BikeId 'bk-550'
#>
function Remove-BikeRentalBikeRecord {
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$State,
        [Parameter(Mandatory = $true)][string]$BikeId
    )

    $bike = $null
    if ($State['bikes'].TryRemove($BikeId, [ref]$bike)) {
        return $bike
    }

    return $null
}

<#
.SYNOPSIS
    Updates the concurrent bike rental database and persists it to disk.
.PARAMETER Mutation
    A script block that receives the concurrent database, applies changes, and returns a result.
.DESCRIPTION
    This function executes the provided mutation while holding the bike rental lock so multi-record
    updates and persistence stay consistent for the concurrent in-memory database.
.EXAMPLE
    Update-BikeRentalState -Mutation { param($state) $state['shopName'] = 'Updated Name' }
#>
function Update-BikeRentalState {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', '')]
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Mutation
    )

    return Use-KrLock -Key $BikeRentalStateLockKey -ScriptBlock {
        $state = Get-BikeRentalState
        $result = & $Mutation $state
        Save-BikeRentalState -State $state
        return $result
    }
}

<#
.SYNOPSIS
    Writes a standardized bike rental API error response.
.PARAMETER StatusCode
    The HTTP status code to return.
.PARAMETER Message
    The error message to include in the response body.
.PARAMETER Details
    Additional fields to merge into the error response payload.
.DESCRIPTION
    This helper builds a consistent JSON error response shape for the concurrent bike
    rental sample and writes it with the requested HTTP status code.
.EXAMPLE
    Write-BikeRentalError -StatusCode 404 -Message 'Bike not found.' -Details @{ bikeId = 'bk-999' }
#>
function Write-BikeRentalError {
    param(
        [Parameter(Mandatory = $true)]
        [int]$StatusCode,
        [Parameter(Mandatory = $true)]
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

<#
.SYNOPSIS
    Retrieves or creates the TLS certificates used by the concurrent bike rental sample.
.PARAMETER CertificatePath
    The path where the PFX certificate is stored.
.PARAMETER CertificatePassword
    The password used to import or export the certificate.
.PARAMETER RootCertificatePath
    The path where the local development root certificate is stored.
.PARAMETER ParentRootCertificatePath
    The fallback path for the shared development root certificate stored under the BikeRentalShop folder.
.PARAMETER RootCertificatePassword
    The password used to import or export the development root certificate.
.DESCRIPTION
    This function prefers the local development root certificate, falls back to the shared copy
    under the BikeRentalShop folder when the local root is missing, and only creates a new root
    when neither copy exists. New roots are exported to both the local and shared locations, while
    the leaf certificate is always issued from and validated against the local root copy.
.EXAMPLE
    $material = Get-BikeRentalCertificate -CertificatePath $CertificatePath -CertificatePassword $password -RootCertificatePath $localRootPath -ParentRootCertificatePath $sharedRootPath -RootCertificatePassword $rootPassword
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
        Export-KrCertificate -Certificate $parentRootCertificate -FilePath $RootCertificatePath `
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
        Development = $true
        DnsNames = 'localhost', '127.0.0.1', '::1'
        Exportable = $true
        LeafValidDays = 30
        RootValidDays = 3650
    }

    if ($rootCertificate) {
        $bundleParameters.RootCertificate = $rootCertificate
    }

    $bundle = New-KrSelfSignedCertificate @bundleParameters

    if (-not $rootCertificate) {
        $rootCertificate = $bundle.RootCertificate
        $publicRootCertificate = $bundle.PublicRootCertificate
        Export-KrCertificate -Certificate $rootCertificate -FilePath $RootCertificatePath `
            -Format pfx -IncludePrivateKey -Password $RootCertificatePassword

        if (-not [string]::IsNullOrWhiteSpace($ParentRootCertificatePath)) {
            Export-KrCertificate -Certificate $rootCertificate -FilePath $ParentRootCertificatePath `
                -Format pfx -IncludePrivateKey -Password $RootCertificatePassword
        }
    }

    $certificate = $bundle.LeafCertificate
    Export-KrCertificate -Certificate $certificate -FilePath $CertificatePath `
        -Format pfx -IncludePrivateKey -Password $CertificatePassword

    return [pscustomobject]@{
        LeafCertificate = $certificate
        RootCertificate = $rootCertificate
        PublicRootCertificate = $publicRootCertificate
    }
}
