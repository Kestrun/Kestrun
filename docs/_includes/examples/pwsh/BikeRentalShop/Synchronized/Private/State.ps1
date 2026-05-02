<#
.SYNOPSIS
    Provides functions for managing the bike rental shop state in shared memory and persisting it to disk.
.DESCRIPTION
    This script initializes a shared in-memory state object before Kestrun configuration is enabled.
    Route runspaces read and update that shared object directly, while mutations persist the latest version to disk.
    The state includes shop
    metadata, bike inventory, rentals, and the last update timestamp. The script also includes helpers for
    standardized error responses and for retrieving or generating the TLS certificate used for HTTPS communication.
.EXAMPLE
    Initialize-BikeRentalStorage
#>
<#
.SYNOPSIS
    Retrieves the default initial state for the bike rental shop, including information about the shop name, currency, available bikes, rentals, and last updated timestamp.
.DESCRIPTION
    This function returns a hashtable representing the default state of the bike rental shop, which includes the shop name, currency, a list of available bikes with their details
    (such as bike ID, model, type, hourly rate, status, dock location, last service date, and current rental ID), an empty list of rentals, and a timestamp for when the state was last updated.
    This default state is used to initialize the state file if it does not already exist when the service starts, ensuring that the bike rental shop has a valid starting point for its inventory and rental information.
.EXAMPLE
    $defaultState = Get-BikeRentalDefaultState
    This example retrieves the default initial state for the bike rental shop and stores it in the variable $defaultState as a hashtable.
    The returned state includes the shop name, currency, a predefined list of available bikes with their details, an empty list of rentals, and a timestamp for when the state was last updated.
    This function is typically used to initialize the state file for the bike rental shop when the service starts, providing a baseline state for the application to work with.
#>
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

<#
.SYNOPSIS
    Converts imported shared-state objects back into plain PowerShell collections for route handlers.
.PARAMETER Value
    The value returned from Import-KrSharedState.
.DESCRIPTION
    Import-KrSharedState returns ordered dictionaries and array lists. This helper recursively converts those
    values into plain hashtables and arrays so the existing route handlers can keep using key-based access
    and mutation without caring about serializer-specific types.
.EXAMPLE
    $state = ConvertTo-BikeRentalStateValue -Value (Import-KrSharedState -Path $StatePath)
#>
function ConvertTo-BikeRentalStateValue {
    param(
        [AllowNull()]
        [object]$Value
    )

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [System.Collections.IDictionary]) {
        $converted = @{}
        foreach ($entry in $Value.GetEnumerator()) {
            $converted[[string]$entry.Key] = ConvertTo-BikeRentalStateValue -Value $entry.Value
        }

        return $converted
    }

    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        $items = foreach ($item in $Value) {
            ConvertTo-BikeRentalStateValue -Value $item
        }

        return @($items)
    }

    return $Value
}

<#
.SYNOPSIS
    Saves the current bike rental state object to disk.
.PARAMETER State
    The bike rental state object to persist.
.DESCRIPTION
    This function stamps the state with a fresh update timestamp and writes it to disk using Export-KrSharedState.
.EXAMPLE
    Save-BikeRentalState -State $BikeRentalStateStore
#>
function Save-BikeRentalState {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$State
    )

    $State['lastUpdatedUtc'] = (Get-Date).ToUniversalTime().ToString('o')
    Export-KrSharedState -InputObject $State -Path $StatePath | Out-Null
}

<#
.SYNOPSIS
    Loads the bike rental state from the persisted state file or legacy JSON seed file.
.DESCRIPTION
    This function prefers the current shared-state file. If that file does not exist but the legacy JSON
    file is still present, it reads the JSON seed once so the service can migrate forward on startup. If neither
    file exists, the default bike rental state is returned.
.EXAMPLE
    $state = Get-BikeRentalPersistedState
#>
function Get-BikeRentalPersistedState {
    if (Test-Path -LiteralPath $StatePath -PathType Leaf) {
        return ConvertTo-BikeRentalStateValue -Value (Import-KrSharedState -Path $StatePath)
    }

    if (-not [string]::IsNullOrWhiteSpace($LegacyStatePath) -and (Test-Path -LiteralPath $LegacyStatePath -PathType Leaf)) {
        return Get-Content -LiteralPath $LegacyStatePath -Raw | ConvertFrom-Json -AsHashtable
    }

    return Get-BikeRentalDefaultState
}

<#
.SYNOPSIS
    Initializes the bike rental storage folders and loads the shared in-memory state object.
.DESCRIPTION
    This function creates the required data, log, and certificate folders, loads the current persisted state once,
    and seeds the shared in-memory state object that is available to all Kestrun runspaces after configuration is enabled.
    When the current state file does not yet exist, the state is written once during startup.
.EXAMPLE
    Initialize-BikeRentalStorage
#>
function Initialize-BikeRentalStorage {
    foreach ($path in @($DataRoot, $LogsRoot, $CertificateRoot)) {
        if (-not (Test-Path -LiteralPath $path)) {
            $null = New-Item -ItemType Directory -Path $path -Force
        }
    }

    $state = Get-BikeRentalPersistedState
    $stateStore = [hashtable]::Synchronized($state)

    if (-not (Test-Path -LiteralPath $StatePath -PathType Leaf)) {
        Save-BikeRentalState -State $stateStore
    }

    return $stateStore
}

<#
.SYNOPSIS
    Retrieves the current bike rental state from the shared in-memory store.
.DESCRIPTION
    This function returns the shared in-memory state object that was initialized during service startup.
.EXAMPLE
    $currentState = Get-BikeRentalState
#>
function Get-BikeRentalState {
    if ($null -eq $BikeRentalStateStore) {
        throw 'Bike rental state has not been initialized.'
    }

    return $BikeRentalStateStore
}

<#
.SYNOPSIS
    Updates the shared bike rental state in memory and persists it to disk.
.PARAMETER Mutation
    A script block that receives the current state hashtable, applies changes, and returns a result.
.DESCRIPTION
    This function applies the provided mutation directly to the shared in-memory state object while holding a
    lightweight Kestrun lock, then exports the updated state to disk.
.EXAMPLE
    Update-BikeRentalState -Mutation { param($state) $state.bikes[0].status = 'rented'; return $state }
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
    Writes a standardized error response for the bike rental shop API with the specified status code, message, and optional additional details.
.PARAMETER StatusCode
    The HTTP status code to include in the error response.
.PARAMETER Message
    A descriptive error message to include in the response body.
.PARAMETER Details
    An optional hashtable of additional key-value pairs to include in the error response body for more context.
.DESCRIPTION
    This function constructs a JSON error response with a consistent structure that includes the provided status code, error message, request path, timestamp, and any additional details.
    It then writes this response using Write-KrJsonResponse with the specified status code.
    This standardized format helps clients of the bike rental shop API to easily understand and handle errors returned by the service.
.EXAMPLE
    Write-BikeRentalError -StatusCode 404 -Message 'Bike not found' -Details @{ bikeId = 'bk-999' }
    This example writes a 404 Not Found error response indicating that a bike with ID 'bk-999' was not found, including the bikeId in the additional details of the response body.
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
    Retrieves the bike rental shop TLS certificates, generating and saving a shared root and leaf certificate set when needed.
.PARAMETER CertificatePath
    The file path where the TLS certificate is stored or should be created if it does not exist.
.PARAMETER CertificatePassword
    The password used to protect the TLS certificate file when exporting or importing.
.PARAMETER RootCertificatePath
.PARAMETER ParentRootCertificatePath
    The fallback file path where the shared development root certificate is stored under the BikeRentalShop folder.
.PARAMETER RootCertificatePassword
    The password used to protect the development root certificate file.
.DESCRIPTION
    This function prefers the local development root certificate, falls back to the shared copy
    under the BikeRentalShop folder when the local root is missing, and only creates a new root
    when neither copy exists. New roots are exported to both the local and shared locations, while
    the leaf certificate is always issued from and validated against the local root copy.
.EXAMPLE
    $material = Get-BikeRentalCertificate -CertificatePath 'C:\certs\bike-rental.pfx' -CertificatePassword $leafPassword -RootCertificatePath 'C:\service\data\certs\bike-rental-root.pfx' -ParentRootCertificatePath 'C:\BikeRentalShop\certs\bike-rental-root.pfx' -RootCertificatePassword $rootPassword
    This example retrieves the bike rental certificate material from disk, generating the shared root and leaf certificates if needed.
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
