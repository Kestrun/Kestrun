param()
BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'Bike rental shop synchronized example' {
    BeforeAll {
        $script:repoRoot = Get-ProjectRootDirectory
        $script:exampleRoot = Join-Path -Path $script:repoRoot -ChildPath 'docs' -AdditionalChildPath '_includes', 'examples', 'pwsh', 'BikeRentalShop', 'Synchronized'
        $script:scriptPath = 'docs/_includes/examples/pwsh/BikeRentalShop/Synchronized/Service.ps1'
        $script:dataRoot = Join-Path -Path $script:exampleRoot -ChildPath 'data'
        $script:statePath = Join-Path -Path $script:exampleRoot -ChildPath 'data' -AdditionalChildPath 'bike-rental-state.clixml'
        $script:legacyStatePath = Join-Path -Path $script:exampleRoot -ChildPath 'data' -AdditionalChildPath 'bike-rental-state.json'
        $script:certificateRoot = Join-Path -Path $script:dataRoot -ChildPath 'certs'
        $script:certificatePath = Join-Path -Path $script:certificateRoot -ChildPath 'bike-rental-shop-devcert.pfx'
        $script:rootCertificatePath = Join-Path -Path $script:certificateRoot -ChildPath 'bike-rental-shared-root.pfx'
        $script:sharedCertificateRoot = Join-Path -Path (Split-Path -Parent $script:exampleRoot) -ChildPath 'certs'
        $script:sharedRootCertificatePath = Join-Path -Path $script:sharedCertificateRoot -ChildPath 'bike-rental-shared-root.pfx'
        $script:leafCertificatePassword = ConvertTo-SecureString -String 'bike-rental-demo' -AsPlainText -Force
        $script:rootCertificatePassword = ConvertTo-SecureString -String 'bike-rental-shared-root' -AsPlainText -Force
        $script:staffHeaders = @{ 'X-Api-Key' = 'bike-shop-demo-key' }
        $script:dataBackup = Backup-ExamplePath -LiteralPath $script:dataRoot
        $script:sharedCertificateBackup = Backup-ExamplePath -LiteralPath $script:sharedCertificateRoot

        foreach ($path in @($script:dataRoot, $script:sharedCertificateRoot)) {
            if (Test-Path -LiteralPath $path) {
                Remove-Item -LiteralPath $path -Recurse -Force
            }
        }

        New-Item -ItemType Directory -Path $script:dataRoot -Force | Out-Null

        $state = [ordered]@{
            shopName = 'Riverside Bike Rental'
            currency = 'USD'
            bikes = @(
                [ordered]@{
                    bikeId = 'bk-100'
                    model = 'City Loop 3'
                    type = 'city'
                    hourlyRate = 12.0
                    status = 'available'
                    dock = 'front-window'
                    lastServiceDate = '2026-03-10'
                    currentRentalId = $null
                }
                [ordered]@{
                    bikeId = 'bk-205'
                    model = 'Trail Runner X'
                    type = 'mountain'
                    hourlyRate = 18.5
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
                    hourlyRate = 24.0
                    status = 'available'
                    dock = 'charging-wall'
                    lastServiceDate = '2026-04-01'
                    currentRentalId = $null
                }
            )
            rentals = @()
            lastUpdatedUtc = '2026-04-06T00:00:00.0000000Z'
        }

        Export-KrSharedState -InputObject $state -Path $script:statePath | Out-Null

        if (Test-Path -LiteralPath $script:legacyStatePath -PathType Leaf) {
            Remove-Item -LiteralPath $script:legacyStatePath -Force
        }

        $script:instance = Start-ExampleScript -Name $script:scriptPath -FromRootDirectory -RunInPlace
        $null = Wait-ExampleRoute -Instance $script:instance -Route '/' -ExpectedStatus 200 -TimeoutSeconds 30
    }

    AfterAll {
        if (Get-Variable -Name instance -Scope Script -ErrorAction SilentlyContinue) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }

        Restore-ExamplePath -Backup $script:dataBackup
        Restore-ExamplePath -Backup $script:sharedCertificateBackup
    }

    It 'recreates the expected state and certificate artifacts after cleanup' {
        Test-Path -LiteralPath $script:dataRoot -PathType Container | Should -BeTrue
        Test-Path -LiteralPath $script:statePath -PathType Leaf | Should -BeTrue
        Test-Path -LiteralPath $script:legacyStatePath -PathType Leaf | Should -BeFalse
        Test-Path -LiteralPath $script:certificateRoot -PathType Container | Should -BeTrue
        Test-Path -LiteralPath $script:certificatePath -PathType Leaf | Should -BeTrue
        Test-Path -LiteralPath $script:rootCertificatePath -PathType Leaf | Should -BeTrue
        Test-Path -LiteralPath $script:sharedCertificateRoot -PathType Container | Should -BeTrue
        Test-Path -LiteralPath $script:sharedRootCertificatePath -PathType Leaf | Should -BeTrue

        $persistedState = Import-KrSharedState -Path $script:statePath
        $persistedState.shopName | Should -Be 'Riverside Bike Rental'
        @($persistedState.bikes).Count | Should -Be 4

        $leafCertificate = $null
        $rootCertificate = $null
        $sharedRootCertificate = $null
        try {
            $leafCertificate = Import-KrCertificate -FilePath $script:certificatePath -Password $script:leafCertificatePassword
            $rootCertificate = Import-KrCertificate -FilePath $script:rootCertificatePath -Password $script:rootCertificatePassword
            $sharedRootCertificate = Import-KrCertificate -FilePath $script:sharedRootCertificatePath -Password $script:rootCertificatePassword

            $leafCertificate.Issuer | Should -Be $rootCertificate.Subject
            $rootCertificate.Thumbprint | Should -Be $sharedRootCertificate.Thumbprint
        } finally {
            foreach ($certificate in @($leafCertificate, $rootCertificate, $sharedRootCertificate)) {
                if ($null -ne $certificate) {
                    $certificate.Dispose()
                }
            }
        }
    }

    It 'serves landing metadata for the packaged service' {
        $result = Invoke-RestMethod -Uri "$($script:instance.Url)/" -SkipCertificateCheck

        $result.service | Should -Be 'Riverside Bike Rental'
        $result.openApi | Should -Be '/openapi/v3.1/openapi.json'
        $result.demoApiKeyHeader | Should -Be 'X-Api-Key: bike-shop-demo-key'
    }

    It 'returns the public bike catalog' {
        $result = Invoke-RestMethod -Uri "$($script:instance.Url)/api/bikes?status=available" -SkipCertificateCheck

        @($result).Count | Should -Be 4
        @($result.bikeId) | Should -Contain 'bk-100'
        @($result.bikeId) | Should -Contain 'bk-402'
    }

    It 'creates a rental and returns the rental status' {
        $payload = @{
            bikeId = 'bk-100'
            customerName = 'Ava Flores'
            phone = '+1-202-555-0148'
            plannedHours = 3
        } | ConvertTo-Json

        $created = Invoke-RestMethod -Uri "$($script:instance.Url)/api/rentals" -Method Post -ContentType 'application/json' -Body $payload -SkipCertificateCheck

        $created.bikeId | Should -Be 'bk-100'
        $created.customerName | Should -Be 'Ava Flores'
        $created.status | Should -Be 'active'
        $created.rentalId | Should -Not -BeNullOrEmpty
        $script:rentalId = $created.rentalId

        $status = Invoke-RestMethod -Uri "$($script:instance.Url)/api/rentals/$($script:rentalId)" -SkipCertificateCheck
        $status.rentalId | Should -Be $script:rentalId
        $status.status | Should -Be 'active'
    }

    It 'adds and removes a staff bike with API key authentication' {
        $payload = @{
            bikeId = 'bk-550'
            model = 'Harbor Cruiser 7'
            type = 'city'
            hourlyRate = 14.5
            dock = 'south-rack'
            lastServiceDate = '2026-04-06'
        } | ConvertTo-Json

        $created = Invoke-RestMethod -Uri "$($script:instance.Url)/api/staff/bikes" -Method Post -ContentType 'application/json' -Body $payload -Headers $script:staffHeaders -SkipCertificateCheck
        $created.bikeId | Should -Be 'bk-550'
        $created.model | Should -Be 'Harbor Cruiser 7'

        $removed = Invoke-RestMethod -Uri "$($script:instance.Url)/api/staff/bikes/bk-550" -Method Delete -Headers $script:staffHeaders -SkipCertificateCheck
        $removed.bikeId | Should -Be 'bk-550'
    }

    It 'returns an active rental through the staff endpoint' {
        $payload = @{
            conditionNotes = 'Returned clean and ready for the next rider.'
        } | ConvertTo-Json

        $returned = Invoke-RestMethod -Uri "$($script:instance.Url)/api/staff/rentals/$($script:rentalId)/return" `
            -Method Post -ContentType 'application/json' -Body $payload -Headers $script:staffHeaders -SkipCertificateCheck

        $returned.rentalId | Should -Be $script:rentalId
        $returned.status | Should -Be 'returned'
        $returned.returnedAtUtc | Should -Not -BeNullOrEmpty
    }

    It 'publishes the return-rental response components in OpenAPI' {
        $document = Invoke-RestMethod -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck

        $document.paths.'/api/staff/rentals/{rentalId}/return'.post.responses.'200'.'$ref' | Should -Be '#/components/responses/StaffReturnRentalOk'
        $document.paths.'/api/staff/rentals/{rentalId}/return'.post.responses.'404'.'$ref' | Should -Be '#/components/responses/StaffReturnRentalNotFound'
        $document.paths.'/api/staff/rentals/{rentalId}/return'.post.responses.'409'.'$ref' | Should -Be '#/components/responses/StaffReturnRentalConflict'
    }
}
