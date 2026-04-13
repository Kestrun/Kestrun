param()
BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'Bike rental shop web example' {
    BeforeAll {
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
            -Name 'examples/PowerShell/BikeRentalShop/Synchronized/Service.ps1' `
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
            -Name 'examples/PowerShell/BikeRentalShop/Web/Service.ps1' `
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
            -Name 'examples/PowerShell/BikeRentalShop/Web/Service.ps1' `
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
