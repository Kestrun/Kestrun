param()
BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'Bike rental shop web example' {
    BeforeAll {
        $script:repoRoot = Get-ProjectRootDirectory
        $script:bikeRentalRoot = Join-Path -Path $script:repoRoot -ChildPath 'docs' -AdditionalChildPath '_includes', 'examples', 'pwsh', 'BikeRentalShop'
        $script:backendExampleRoot = Join-Path $script:bikeRentalRoot 'Synchronized'
        $script:backendDataRoot = Join-Path $script:backendExampleRoot 'data'
        $script:backendStatePath = Join-Path $script:backendDataRoot 'bike-rental-state.clixml'
        $script:backendCertificateRoot = Join-Path $script:backendDataRoot 'certs'
        $script:backendCertificatePath = Join-Path $script:backendCertificateRoot 'bike-rental-shop-devcert.pfx'
        $script:backendRootCertificatePath = Join-Path $script:backendCertificateRoot 'bike-rental-shared-root.pfx'
        $script:webExampleRoot = Join-Path $script:bikeRentalRoot 'Web'
        $script:webDataRoot = Join-Path $script:webExampleRoot 'data'
        $script:webCertificateRoot = Join-Path $script:webDataRoot 'certs'
        $script:webCertificatePath = Join-Path $script:webCertificateRoot 'bike-rental-shop-web-devcert.pfx'
        $script:webRootCertificatePath = Join-Path $script:webCertificateRoot 'bike-rental-shared-root.pfx'
        $script:webPublicRootCertificatePath = Join-Path $script:webCertificateRoot 'bike-rental-shared-root-public.pem'
        $script:sharedCertificateRoot = Join-Path $script:bikeRentalRoot 'certs'
        $script:sharedRootCertificatePath = Join-Path $script:sharedCertificateRoot 'bike-rental-shared-root.pfx'
        $script:backendLeafCertificatePassword = ConvertTo-SecureString -String 'bike-rental-demo' -AsPlainText -Force
        $script:webLeafCertificatePassword = ConvertTo-SecureString -String 'bike-rental-web-demo' -AsPlainText -Force
        $script:rootCertificatePassword = ConvertTo-SecureString -String 'bike-rental-shared-root' -AsPlainText -Force
        $script:backendDataBackup = Backup-ExamplePath -LiteralPath $script:backendDataRoot
        $script:webDataBackup = Backup-ExamplePath -LiteralPath $script:webDataRoot
        $script:sharedCertificateBackup = Backup-ExamplePath -LiteralPath $script:sharedCertificateRoot

        foreach ($path in @($script:backendDataRoot, $script:webDataRoot, $script:sharedCertificateRoot)) {
            if (Test-Path -LiteralPath $path) {
                Remove-Item -LiteralPath $path -Recurse -Force
            }
        }

        $portBlock = Get-FreeTcpPortBlock -Count 2
        $script:backendPort = $portBlock
        $script:webPort = $portBlock + 1
        $script:webOrigin = "https://127.0.0.1:$($script:webPort)"
        $allowedOrigins = @(
            "https://127.0.0.1:$($script:webPort)"
            "https://localhost:$($script:webPort)"
        )
        $backendEnvironmentVariables = @(
            'UPSTASH_REDIS_URL'
            'BIKE_RENTAL_ALLOWED_CORS_ORIGINS'
        )
        $webEnvironmentVariables = @(
            'UPSTASH_REDIS_URL'
            'BIKE_RENTAL_WEB_BACKEND'
            'BIKE_RENTAL_API_BASE_URL'
        )

        $env:BIKE_RENTAL_ALLOWED_CORS_ORIGINS = $allowedOrigins -join ';'
        $script:backendInstance = Start-ExampleScript `
            -Name 'docs/_includes/examples/pwsh/BikeRentalShop/Synchronized/Service.ps1' `
            -FromRootDirectory `
            -RunInPlace `
            -Port $script:backendPort `
            -EnvironmentVariables $backendEnvironmentVariables
        $null = Wait-ExampleRoute `
            -Instance $script:backendInstance `
            -Route '/' `
            -ExpectedStatus 200 `
            -TimeoutSeconds 30

        $env:BIKE_RENTAL_WEB_BACKEND = 'Custom'
        $env:BIKE_RENTAL_API_BASE_URL = $script:backendInstance.Url
        $script:webInstance = Start-ExampleScript `
            -Name 'docs/_includes/examples/pwsh/BikeRentalShop/Web/Service.ps1' `
            -FromRootDirectory `
            -RunInPlace `
            -Port $script:webPort `
            -EnvironmentVariables $webEnvironmentVariables
        $null = Wait-ExampleRoute `
            -Instance $script:webInstance `
            -Route '/' `
            -ExpectedStatus 200 `
            -TimeoutSeconds 30
    }

    AfterAll {
        Remove-Item Env:BIKE_RENTAL_WEB_BACKEND -ErrorAction SilentlyContinue
        Remove-Item Env:BIKE_RENTAL_API_BASE_URL -ErrorAction SilentlyContinue
        Remove-Item Env:BIKE_RENTAL_ALLOWED_CORS_ORIGINS -ErrorAction SilentlyContinue

        if (Get-Variable -Name webInstance -Scope Script -ErrorAction SilentlyContinue) {
            Stop-ExampleScript -Instance $script:webInstance
            Write-KrExampleInstanceOnFailure -Instance $script:webInstance
        }

        if (Get-Variable -Name backendInstance -Scope Script -ErrorAction SilentlyContinue) {
            Stop-ExampleScript -Instance $script:backendInstance
            Write-KrExampleInstanceOnFailure -Instance $script:backendInstance
        }

        Restore-ExamplePath -Backup $script:backendDataBackup
        Restore-ExamplePath -Backup $script:webDataBackup
        Restore-ExamplePath -Backup $script:sharedCertificateBackup
    }

    It 'recreates the expected backend and web certificate artifacts after cleanup' {
        Test-Path -LiteralPath $script:backendDataRoot -PathType Container | Should -BeTrue
        Test-Path -LiteralPath $script:backendStatePath -PathType Leaf | Should -BeTrue
        Test-Path -LiteralPath $script:backendCertificateRoot -PathType Container | Should -BeTrue
        Test-Path -LiteralPath $script:backendCertificatePath -PathType Leaf | Should -BeTrue
        Test-Path -LiteralPath $script:backendRootCertificatePath -PathType Leaf | Should -BeTrue
        Test-Path -LiteralPath $script:webDataRoot -PathType Container | Should -BeTrue
        Test-Path -LiteralPath $script:webCertificateRoot -PathType Container | Should -BeTrue
        Test-Path -LiteralPath $script:webCertificatePath -PathType Leaf | Should -BeTrue
        Test-Path -LiteralPath $script:webRootCertificatePath -PathType Leaf | Should -BeTrue
        Test-Path -LiteralPath $script:webPublicRootCertificatePath -PathType Leaf | Should -BeTrue
        Test-Path -LiteralPath $script:sharedCertificateRoot -PathType Container | Should -BeTrue
        Test-Path -LiteralPath $script:sharedRootCertificatePath -PathType Leaf | Should -BeTrue

        $persistedState = Import-KrSharedState -Path $script:backendStatePath
        $persistedState.shopName | Should -Be 'Riverside Bike Rental'
        @($persistedState.bikes).Count | Should -Be 4

        $backendLeafCertificate = $null
        $backendRootCertificate = $null
        $webLeafCertificate = $null
        $webRootCertificate = $null
        $sharedRootCertificate = $null
        try {
            $backendLeafCertificate = Import-KrCertificate -FilePath $script:backendCertificatePath -Password $script:backendLeafCertificatePassword
            $backendRootCertificate = Import-KrCertificate -FilePath $script:backendRootCertificatePath -Password $script:rootCertificatePassword
            $webLeafCertificate = Import-KrCertificate -FilePath $script:webCertificatePath -Password $script:webLeafCertificatePassword
            $webRootCertificate = Import-KrCertificate -FilePath $script:webRootCertificatePath -Password $script:rootCertificatePassword
            $sharedRootCertificate = Import-KrCertificate -FilePath $script:sharedRootCertificatePath -Password $script:rootCertificatePassword

            $backendLeafCertificate.Issuer | Should -Be $backendRootCertificate.Subject
            $webLeafCertificate.Issuer | Should -Be $webRootCertificate.Subject
            $backendRootCertificate.Thumbprint | Should -Be $sharedRootCertificate.Thumbprint
            $webRootCertificate.Thumbprint | Should -Be $sharedRootCertificate.Thumbprint
        } finally {
            foreach ($certificate in @($backendLeafCertificate, $backendRootCertificate, $webLeafCertificate, $webRootCertificate, $sharedRootCertificate)) {
                if ($null -ne $certificate) {
                    $certificate.Dispose()
                }
            }
        }

        $publicRootPem = Get-Content -LiteralPath $script:webPublicRootCertificatePath -Raw
        $publicRootPem | Should -Match 'BEGIN CERTIFICATE'
        $publicRootPem | Should -Match 'END CERTIFICATE'
        $publicRootPem | Should -Not -Match 'BEGIN PRIVATE KEY'
    }

    It 'serves the standalone home page with the configured backend URL' {
        $response = Invoke-WebRequest -Uri "$($script:webInstance.Url)/" -SkipCertificateCheck
        $escapedBackendUrl = [regex]::Escape($script:backendInstance.Url)

        $response.StatusCode | Should -Be 200
        $response.Headers['Content-Type'] | Should -Match 'text/html'
        $response.Content | Should -Match 'Standalone web client'
        $response.Content | Should -Match $escapedBackendUrl
    }

    It 'serves the staff operations page from the standalone service' {
        $response = Invoke-WebRequest -Uri "$($script:webInstance.Url)/Operations" -SkipCertificateCheck

        $response.StatusCode | Should -Be 200
        $response.Content | Should -Match 'Staff web console'
        $response.Content | Should -Match 'Back to booking'
    }

    It 'serves the standalone static assets' {
        $response = Invoke-WebRequest -Uri "$($script:webInstance.Url)/static/site.css" -SkipCertificateCheck

        $response.StatusCode | Should -Be 200
        $response.Headers['Content-Type'] | Should -Match 'text/css'
    }

    It 'serves the public development root certificate without the private key' {
        $response = Invoke-WebRequest -Uri "$($script:webInstance.Url)/certificates/root.pem" -SkipCertificateCheck
        $pemContent = if ($response.Content -is [string]) {
            $response.Content
        } else {
            [System.Text.Encoding]::UTF8.GetString([byte[]]@($response.Content))
        }

        $response.StatusCode | Should -Be 200
        $response.Headers['Content-Type'] | Should -Match 'application/x-pem-file'
        $pemContent | Should -Match 'BEGIN CERTIFICATE'
        $pemContent | Should -Match 'END CERTIFICATE'
        $pemContent | Should -Not -Match 'BEGIN PRIVATE KEY'
    }

    It 'allows browser preflight requests from the standalone web origin' {
        $headers = @{
            Origin = $script:webOrigin
            'Access-Control-Request-Method' = 'POST'
            'Access-Control-Request-Headers' = 'content-type,x-api-key'
        }

        $response = Invoke-WebRequest -Uri "$($script:backendInstance.Url)/api/staff/bikes" -Method Options -Headers $headers -SkipCertificateCheck

        $response.Headers['Access-Control-Allow-Origin'] | Should -Be $script:webOrigin
        $response.Headers['Access-Control-Allow-Methods'] | Should -Match 'POST'
        $response.Headers['Access-Control-Allow-Headers'] | Should -Match '(?i)x-api-key'
    }

    It 'can render the concurrent backend profile without embedding the API service' {
        $port = Get-FreeTcpPort
        $env:BIKE_RENTAL_WEB_BACKEND = 'Concurrent'
        Remove-Item Env:BIKE_RENTAL_API_BASE_URL -ErrorAction SilentlyContinue
        $concurrentEnvironmentVariables = @(
            'UPSTASH_REDIS_URL'
            'BIKE_RENTAL_WEB_BACKEND'
        )
        $instance = Start-ExampleScript `
            -Name 'docs/_includes/examples/pwsh/BikeRentalShop/Web/Service.ps1' `
            -FromRootDirectory `
            -RunInPlace `
            -Port $port `
            -EnvironmentVariables $concurrentEnvironmentVariables

        try {
            $null = Wait-ExampleRoute -Instance $instance -Route '/' -ExpectedStatus 200 -TimeoutSeconds 30
            $response = Invoke-WebRequest -Uri "$($instance.Url)/" -SkipCertificateCheck
            $response.Content | Should -Match 'https://127.0.0.1:5444'
        } finally {
            Stop-ExampleScript -Instance $instance
            Write-KrExampleInstanceOnFailure -Instance $instance
            $env:BIKE_RENTAL_WEB_BACKEND = 'Custom'
            $env:BIKE_RENTAL_API_BASE_URL = $script:backendInstance.Url
        }
    }
}
