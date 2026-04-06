function global:Invoke-BikeRentalStateLock {
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

function global:Get-BikeRentalDefaultState {
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

function global:Save-BikeRentalStateUnsafe {
    param([hashtable]$State)

    $State['lastUpdatedUtc'] = (Get-Date).ToUniversalTime().ToString('o')
    $json = $State | ConvertTo-Json -Depth 8
    Set-Content -LiteralPath $StatePath -Value $json -Encoding utf8NoBOM
}

function global:Read-BikeRentalStateUnsafe {
    if (-not (Test-Path -LiteralPath $StatePath -PathType Leaf)) {
        Save-BikeRentalStateUnsafe -State (Get-BikeRentalDefaultState)
    }

    return Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json -AsHashtable
}

function global:Initialize-BikeRentalStorage {
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

function global:Get-BikeRentalState {
    Invoke-BikeRentalStateLock {
        Read-BikeRentalStateUnsafe
    }
}

function global:Update-BikeRentalState {
    param([scriptblock]$Mutation)

    Invoke-BikeRentalStateLock {
        $state = Read-BikeRentalStateUnsafe
        $result = & $Mutation $state
        Save-BikeRentalStateUnsafe -State $state
        return $result
    }
}

function global:Write-BikeRentalError {
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

function global:Get-BikeRentalCertificate {
    if (Test-Path -LiteralPath $CertificatePath -PathType Leaf) {
        return Import-KrCertificate -FilePath $CertificatePath -Password (ConvertTo-SecureString -String $CertificatePassword -AsPlainText -Force)
    }

    $certificate = New-KrSelfSignedCertificate -DnsNames @('localhost', '127.0.0.1') -Exportable
    Export-KrCertificate -Certificate $certificate -FilePath ([System.IO.Path]::ChangeExtension($CertificatePath, $null)) -Format pfx -IncludePrivateKey -Password (ConvertTo-SecureString -String $CertificatePassword -AsPlainText -Force)
    return $certificate
}
