<#
.SYNOPSIS
    Provides functions for managing the state of the bike rental shop, including reading and writing the state to persistent storage, initializing storage, and handling thread-safe access to the state file.
.DESCRIPTION
    This script defines a set of functions that are used to manage the state of the bike rental shop application.
    It includes functionality for initializing the storage structure, reading and writing the state of the shop, and ensuring thread-safe access to the state file using a mutex lock.
    The state includes information about available bikes, rentals, and other relevant data that represents the current status of the bike rental shop.
    The functions in this script are used by the API endpoints defined in Service.ps1 to provide clients with up-to-date information about the shop and to persist changes made through the API.
    The script also includes a function for writing standardized error responses for the API, and a function for retrieving or generating the TLS certificate used for HTTPS communication.
.EXAMPLE
    Initialize-BikeRentalStorage
#>
function Invoke-BikeRentalStateLock {
    param([scriptblock]$Action)

    $lockTaken = $false
    try {
        $lockTaken = $StateMutex.WaitOne([TimeSpan]::FromSeconds(15))
        if (-not $lockTaken) {
            throw 'Timed out waiting for the bike rental state file lock.'
        }

        & $Action
    } finally {
        if ($lockTaken) {
            [void]$StateMutex.ReleaseMutex()
        }
    }
}

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
    Saves the provided bike rental shop state to persistent storage in a non-thread-safe manner by converting it to JSON and writing it to a file.
.PARAMETER State
    A hashtable representing the current state of the bike rental shop, including information about available bikes, rentals, and other relevant data.
.DESCRIPTION
    This function takes a hashtable representing the state of the bike rental shop, adds a timestamp for when the state was last updated, converts it to JSON format,
    and writes it to a file at the path specified by $StatePath.
    It does not acquire a lock, so it should only be called from within a locked context to ensure thread safety when accessing the state file.
    This function is used to persist changes to the bike rental shop's state after modifications have been made, allowing the service to maintain an up-to-date record of the shop's status
    that can be read by API endpoints and other functions when needed.
.EXAMPLE
    Save-BikeRentalStateUnsafe -State $updatedState
    This example saves the provided $updatedState hashtable to persistent storage by converting it to JSON and writing it to the file specified by $StatePath.
    This function should be called within a locked context to ensure thread safety when accessing the state file.
    The $updatedState should include the latest information about the bike rental shop's status, such as available bikes and current rentals, that needs to be
#>
function Save-BikeRentalStateUnsafe {
    param([hashtable]$State)

    $State['lastUpdatedUtc'] = (Get-Date).ToUniversalTime().ToString('o')
    $json = $State | ConvertTo-Json -Depth 8
    Set-Content -LiteralPath $StatePath -Value $json -Encoding utf8NoBOM
}

<#
.SYNOPSIS
    Reads the current state of the bike rental shop from persistent storage without acquiring a lock, returning it as a hashtable.
.DESCRIPTION
    This function reads the bike rental shop's state from a JSON file located at $StatePath and converts it from JSON into a hashtable for use within the service.
    It does not acquire a lock, so it should only be called from within a locked context to ensure thread safety.
    If the state file does not exist, it initializes it with default data by calling Get-BikeRentalDefaultState and saving it using Save-BikeRentalStateUnsafe.
    The returned hashtable includes information about available bikes, rentals, and other relevant data that represents the current status of the bike rental shop,
    and is used by API endpoints to provide clients with the latest state of the shop.
.EXAMPLE
    $state = Read-BikeRentalStateUnsafe
    This example reads the current state of the bike rental shop from persistent storage and stores it in the variable $state as a hashtable.
    This function should be called within a locked context to ensure thread safety when accessing the state file. If the state file does not exist, it will be initialized
#>
function Read-BikeRentalStateUnsafe {
    if (-not (Test-Path -LiteralPath $StatePath -PathType Leaf)) {
        Save-BikeRentalStateUnsafe -State (Get-BikeRentalDefaultState)
    }

    return Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json -AsHashtable
}

<#
.SYNOPSIS
    Initializes the storage for the bike rental shop by creating necessary directories and ensuring the state file is initialized with default data.
.DESCRIPTION
    This function checks for the existence of required directories for data, logs, and certificates, creating them if they do not exist.
    It then uses the Invoke-BikeRentalStateLock function to safely check if the state file exists, and if it does not,
    it initializes the state file with default data by calling Get-BikeRentalDefaultState and saving it using Save-BikeRentalStateUnsafe.
    This ensures that the bike rental shop has a valid state file to work with when the service starts, preventing errors related to missing state and allowing the API endpoints to function correctly from the outset.
.EXAMPLE
    Initialize-BikeRentalStorage
    This example initializes the storage for the bike rental shop by creating necessary directories and ensuring that the state file is initialized with default data if it does not already exist.
    This should be called during the startup of the bike rental service to set up the required storage structure and initial state for the application to operate correctly.
#>
function Initialize-BikeRentalStorage {
    foreach ($path in @($DataRoot, $LogsRoot, $CertificateRoot)) {
        if (-not (Test-Path -LiteralPath $path)) {
            $null = New-Item -ItemType Directory -Path $path -Force
        }
    }

    Invoke-BikeRentalStateLock {
        if (-not (Test-Path -LiteralPath $StatePath -PathType Leaf)) {
            Save-BikeRentalStateUnsafe -State (Get-BikeRentalDefaultState)
        }
    }
}

<#
.SYNOPSIS
    Retrieves the current state of the bike rental shop in a thread-safe manner by acquiring a lock, reading the state from storage, and returning it as a hashtable.
.DESCRIPTION
    This function ensures that the state of the bike rental shop is accessed in a thread-safe way by using a mutex lock to prevent concurrent access to the state file.
    It acquires the lock, reads the current state from persistent storage using the Read-BikeRentalStateUnsafe function, and returns the state as a hashtable.
    The state includes information about available bikes, rentals, and other relevant data that represents the current status of the bike rental shop.
    This function is used by API endpoints to retrieve the latest state of the shop while ensuring data integrity and preventing race conditions when multiple requests are trying to access the state simultaneously.
.EXAMPLE
    $currentState = Get-BikeRentalState
    This example retrieves the current state of the bike rental shop and stores it in the variable $currentState.
    The function handles acquiring the lock and reading the state from storage to ensure thread-safe access to the shop's data.
#>
function Get-BikeRentalState {
    Invoke-BikeRentalStateLock {
        Read-BikeRentalStateUnsafe
    }
}

<#
.SYNOPSIS
    Safely updates the bike rental shop's state by acquiring a lock, reading the current state, applying a mutation function, and saving the updated state back to storage.
.PARAMETER Mutation
    A script block that takes the current state as input, applies any necessary modifications, and returns a result.
    The state passed to the mutation function is a hashtable representing the current state of the bike rental shop, including information about available bikes, rentals, and other relevant data.
.DESCRIPTION
    This function ensures that updates to the bike rental shop's state are performed in a thread-safe manner by using a mutex lock to prevent concurrent access to the state file.
    It reads the current state, executes the provided mutation function to apply changes, and then saves the updated state back to persistent storage.
    The mutation function can perform any necessary logic to modify the state, such as updating bike availability, creating new rentals, or changing rental statuses,
    while the locking mechanism ensures data integrity and prevents race conditions when multiple requests are trying to update the state simultaneously.
.EXAMPLE
    Update-BikeRentalState -Mutation { param($state) $state.bikes[0].status = 'rented'; return $state }
    This example updates the status of the first bike in the state to 'rented' by providing a mutation script block that modifies the state hashtable accordingly.
     The function will handle acquiring the lock  and saving the updated state back to storage after the mutation is applied.
#>
function Update-BikeRentalState {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', '')]
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Mutation
    )

    Invoke-BikeRentalStateLock {
        $state = Read-BikeRentalStateUnsafe
        $result = & $Mutation $state
        Save-BikeRentalStateUnsafe -State $state
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
    Retrieves the bike rental shop's TLS certificate, generating and saving a new self-signed certificate if one does not already exist at the specified path.
.PARAMETER CertificatePath
    The file path where the TLS certificate is stored or should be created if it does not exist.
.PARAMETER CertificatePassword
    The password used to protect the TLS certificate file when exporting or importing.
.DESCRIPTION
    This function checks for the existence of a TLS certificate file at the specified path.
    If the file exists, it imports and returns the certificate. If the file does not exist,
    it generates a new self-signed certificate with the specified DNS names, exports it to the specified path with the provided password,
    and returns the newly created certificate. The certificate is used to enable HTTPS for the bike rental shop's API, ensuring secure communication between clients and the server.
.EXAMPLE
    $cert = Get-BikeRentalCertificate -CertificatePath 'C:\certs\bike-rental.pfx' -CertificatePassword '    MySecurePassword'
    This example retrieves the bike rental certificate from 'C:\certs\bike-rental.pfx', generating a new self-signed certificate if the file does not exist, and stores it in the variable $cert.
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
