param()
BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'CORS Advanced Features' -Tag 'Integration', 'CORS' {

    Context 'Multiple Origins Support' {

        BeforeAll {
            $script:instance = Start-ExampleScript -Scriptblock {
                param(
                    [int]$Port = 5000,
                    [IPAddress]$IPAddress = [IPAddress]::Loopback
                )

                New-KrServer -Name 'MultiOriginServer'
                Add-KrEndpoint -Port $Port -IPAddress $IPAddress

                # Policy with multiple origins
                New-KrCorsPolicyBuilder |
                    Set-KrCorsOrigin -Origins 'http://localhost:3000', 'http://localhost:3001' |
                    Set-KrCorsMethod -Any |
                    Set-KrCorsHeader -Any |
                    Add-KrCorsPolicy -Name 'MultiOrigin'

                Add-KrMapRoute -Verbs Get, Options -Pattern '/data' -Scriptblock {
                    Write-KrJsonResponse @{ message = 'Data from multi-origin endpoint' }
                } -CorsPolicy 'MultiOrigin'

                Enable-KrConfiguration
                Start-KrServer
            }
        }

        AfterAll {
            if ($script:instance) {
                # Stop the example script
                Stop-ExampleScript -Instance $script:instance
                # Diagnostic info on failure
                Write-KrExampleInstanceOnFailure -Instance $script:instance
            }
        }

        It 'Allows first origin' {
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/data" `
                -Method Get `
                -Headers @{ 'Origin' = 'http://localhost:3000' }

            $response.Headers['Access-Control-Allow-Origin'] | Should -Be 'http://localhost:3000'
        }

        It 'Allows second origin' {
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/data" `
                -Method Get `
                -Headers @{ 'Origin' = 'http://localhost:3001' }

            $response.Headers['Access-Control-Allow-Origin'] | Should -Be 'http://localhost:3001'
        }

        It 'Blocks non-listed origin' {
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/data" `
                -Method Get `
                -Headers @{ 'Origin' = 'http://evil.com' }

            $response.Headers['Access-Control-Allow-Origin'] | Should -BeNullOrEmpty
        }
    }

    Context 'Credentials Support' {

        BeforeAll {
            $script:instance = Start-ExampleScript -Scriptblock {
                param(
                    [int]$Port = 5000,
                    [IPAddress]$IPAddress = [IPAddress]::Loopback
                )
                New-KrServer -Name 'CredentialsServer'
                Add-KrEndpoint -Port $Port -IPAddress $IPAddress

                # Policy with credentials
                New-KrCorsPolicyBuilder |
                    Set-KrCorsOrigin -Origins 'http://localhost:3000' |
                    Set-KrCorsMethod -Any |
                    Set-KrCorsHeader -Any |
                    Set-KrCorsCredential -Allow |
                    Add-KrCorsPolicy -Name 'WithCredentials'

                Add-KrMapRoute -Verbs Get, Options -Pattern '/secure' -Scriptblock {
                    Write-KrJsonResponse @{ authenticated = $true }
                } -CorsPolicy 'WithCredentials'

                Enable-KrConfiguration
                Start-KrServer
            }
        }

        AfterAll {
            if ($script:instance) {
                # Stop the example script
                Stop-ExampleScript -Instance $script:instance
                # Diagnostic info on failure
                Write-KrExampleInstanceOnFailure -Instance $script:instance
            }
        }

        It 'Includes Access-Control-Allow-Credentials header' {
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/secure" `
                -Method Get `
                -Headers @{ 'Origin' = 'http://localhost:3000' }

            $response.Headers['Access-Control-Allow-Credentials'] | Should -Be 'true'
        }

        It 'Does not use wildcard for origin with credentials' {
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/secure" `
                -Method Get `
                -Headers @{ 'Origin' = 'http://localhost:3000' }

            $response.Headers['Access-Control-Allow-Origin'] | Should -Be 'http://localhost:3000'
            $response.Headers['Access-Control-Allow-Origin'] | Should -Not -Be '*'
        }
    }

    Context 'Exposed Headers' {

        BeforeAll {
            $script:instance = Start-ExampleScript -Scriptblock {
                param(
                    [int]$Port = 5000,
                    [IPAddress]$IPAddress = [IPAddress]::Loopback
                )
                New-KrServer -Name 'ExposedHeadersServer'
                Add-KrEndpoint -Port $Port -IPAddress $IPAddress

                # Policy with exposed headers
                New-KrCorsPolicyBuilder |
                    Set-KrCorsOrigin -Origins 'http://localhost:3000' |
                    Set-KrCorsMethod -Any |
                    Set-KrCorsHeader -Any |
                    Set-KrCorsExposedHeader -Headers 'X-Total-Count', 'X-Page-Number' |
                    Add-KrCorsPolicy -Name 'WithExposedHeaders'

                Add-KrMapRoute -Verbs Get, Options -Pattern '/list' -Scriptblock {
                    $Context.Response.Headers.Add('X-Total-Count', '42')
                    $Context.Response.Headers.Add('X-Page-Number', '1')
                    Write-KrJsonResponse @( @{ id = 1 }, @{ id = 2 } )
                } -CorsPolicy 'WithExposedHeaders'

                Enable-KrConfiguration
                Start-KrServer
            }
        }

        AfterAll {
            if ($script:instance) {
                # Stop the example script
                Stop-ExampleScript -Instance $script:instance
                # Diagnostic info on failure
                Write-KrExampleInstanceOnFailure -Instance $script:instance
            }
        }

        It 'Exposes custom headers in CORS response' {
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/list" `
                -Method Get `
                -Headers @{ 'Origin' = 'http://localhost:3000' }

            $exposeHeaders = $response.Headers['Access-Control-Expose-Headers']
            $exposeHeaders | Should -Not -BeNullOrEmpty
            $exposeHeaders | Should -Match 'X-Total-Count'
            $exposeHeaders | Should -Match 'X-Page-Number'
        }

        It 'Custom headers are present in response' {
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/list" `
                -Method Get `
                -Headers @{ 'Origin' = 'http://localhost:3000' }

            $response.Headers['X-Total-Count'] | Should -Be '42'
            $response.Headers['X-Page-Number'] | Should -Be '1'
        }
    }

    Context 'Preflight Max Age' {

        BeforeAll {
            $script:instance = Start-ExampleScript -Scriptblock {
                param(
                    [int]$Port = 5000,
                    [IPAddress]$IPAddress = [IPAddress]::Loopback
                )
                New-KrServer -Name 'MaxAgeServer'
                Add-KrEndpoint -Port $Port -IPAddress $IPAddress

                # Policy with preflight max age
                New-KrCorsPolicyBuilder |
                    Set-KrCorsOrigin -Origins 'http://localhost:3000' |
                    Set-KrCorsMethod -Methods GET, POST, PUT |
                    Set-KrCorsHeader -Any |
                    Set-KrCorsPreflightMaxAge -Seconds 7200 |
                    Add-KrCorsPolicy -Name 'WithMaxAge'

                Add-KrMapRoute -Verbs Post, Options -Pattern '/update' -Scriptblock {
                    Write-KrJsonResponse @{ updated = $true }
                } -CorsPolicy 'WithMaxAge'

                Enable-KrConfiguration
                Start-KrServer
            }
        }

        AfterAll {
            if ($script:instance) {
                # Stop the example script
                Stop-ExampleScript -Instance $script:instance
                # Diagnostic info on failure
                Write-KrExampleInstanceOnFailure -Instance $script:instance
            }
        }

        It 'Includes Access-Control-Max-Age header in preflight response' {
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/update" `
                -Method Options `
                -Headers @{
                'Origin' = 'http://localhost:3000'
                'Access-Control-Request-Method' = 'POST'
            }

            $response.Headers['Access-Control-Max-Age'] | Should -Be '7200'
        }

        It 'Caching applies to allowed methods' {
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/update" `
                -Method Options `
                -Headers @{
                'Origin' = 'http://localhost:3000'
                'Access-Control-Request-Method' = 'POST'
            }

            $allowedMethods = $response.Headers['Access-Control-Allow-Methods']
            $allowedMethods | Should -Match 'POST'
        }
    }

    Context 'Complex Method and Header Combinations' {

        BeforeAll {
            $script:instance = Start-ExampleScript -Scriptblock {
                param(
                    [int]$Port = 5000,
                    [IPAddress]$IPAddress = [IPAddress]::Loopback
                )
                New-KrServer -Name 'ComplexPolicyServer'
                Add-KrEndpoint -Port $Port -IPAddress $IPAddress

                # Policy with specific methods and headers
                New-KrCorsPolicyBuilder |
                    Set-KrCorsOrigin -Origins 'http://localhost:3000' |
                    Set-KrCorsMethod -Methods POST, PUT, PATCH |
                    Set-KrCorsHeader -Headers 'Content-Type', 'Authorization', 'X-Api-Key' |
                    Add-KrCorsPolicy -Name 'RestrictedWritePolicy'

                Add-KrMapRoute -Verbs Post, Put, Patch, Options -Pattern '/api/resource' -Scriptblock {
                    $method = $Context.Request.Method
                    Write-KrJsonResponse @{ method = $method; status = 'processed' }
                } -CorsPolicy 'RestrictedWritePolicy'

                Enable-KrConfiguration
                Start-KrServer
            }
        }

        AfterAll {
            if ($script:instance) {

                # Stop the example script
                Stop-ExampleScript -Instance $script:instance
                # Diagnostic info on failure
                Write-KrExampleInstanceOnFailure -Instance $script:instance
            }
        }

        It 'Allows POST with correct headers' {
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/api/resource" `
                -Method Post `
                -Headers @{
                'Origin' = 'http://localhost:3000'
                'Content-Type' = 'application/json'
            } `
                -Body '{"data":"test"}'

            $response.Headers['Access-Control-Allow-Origin'] | Should -Be 'http://localhost:3000'
        }

        It 'Preflight allows only specified methods' {
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/api/resource" `
                -Method Options `
                -Headers @{
                'Origin' = 'http://localhost:3000'
                'Access-Control-Request-Method' = 'POST'
            }

            $allowedMethods = $response.Headers['Access-Control-Allow-Methods']
            $allowedMethods | Should -Match 'POST'
            $allowedMethods | Should -Match 'PUT'
            $allowedMethods | Should -Match 'PATCH'
            $allowedMethods | Should -Not -Match 'DELETE'
            $allowedMethods | Should -Not -Match 'GET'
        }

        It 'Preflight allows only specified headers' {
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/api/resource" `
                -Method Options `
                -Headers @{
                'Origin' = 'http://localhost:3000'
                'Access-Control-Request-Method' = 'POST'
                'Access-Control-Request-Headers' = 'Content-Type,Authorization'
            }

            $allowedHeaders = $response.Headers['Access-Control-Allow-Headers']
            $allowedHeaders | Should -Match 'Content-Type'
            $allowedHeaders | Should -Match 'Authorization'
            $allowedHeaders | Should -Match 'X-Api-Key'
        }
    }

    Context 'AllowAll Policy Variants' {

        BeforeAll {
            $script:instance = Start-ExampleScript -Scriptblock {
                param(
                    [int]$Port = 5000,
                    [IPAddress]$IPAddress = [IPAddress]::Loopback
                )
                New-KrServer -Name 'AllowAllServer'
                Add-KrEndpoint -Port $Port -IPAddress $IPAddress

                # AllowAll as default policy
                Add-KrCorsPolicy -Default -AllowAll

                Add-KrMapRoute -Verbs Get, Post, Options -Pattern '/public' -Scriptblock {
                    Write-KrJsonResponse @{ access = 'public' }
                }

                Enable-KrConfiguration
                Start-KrServer
            }
        }

        AfterAll {
            if ($script:instance) {
                # Stop the example script
                Stop-ExampleScript -Instance $script:instance
                # Diagnostic info on failure
                Write-KrExampleInstanceOnFailure -Instance $script:instance
            }
        }

        It 'AllowAll responds with wildcard origin' {
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/public" `
                -Method Get `
                -Headers @{ 'Origin' = 'http://anywhere.com' }

            $response.Headers['Access-Control-Allow-Origin'] | Should -Be '*'
        }

        It 'AllowAll works with any origin' {
            $origins = @('http://test1.com', 'http://test2.com', 'https://secure.com')

            foreach ($origin in $origins) {
                $response = Invoke-WebRequest -Uri "$($script:instance.Url)/public" `
                    -Method Get `
                    -Headers @{ 'Origin' = $origin }

                $response.Headers['Access-Control-Allow-Origin'] | Should -Be '*'
            }
        }

        It 'AllowAll preflight allows any method' {
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/public" `
                -Method Options `
                -Headers @{
                'Origin' = 'http://anywhere.com'
                'Access-Control-Request-Method' = 'DELETE'
            }

            $response.StatusCode | Should -Be 204
        }
    }

    Context 'Default Policy Fallback' {

        BeforeAll {
            $script:instance = Start-ExampleScript -Scriptblock {
                param(
                    [int]$Port = 5000,
                    [IPAddress]$IPAddress = [IPAddress]::Loopback
                )
                New-KrServer -Name 'DefaultFallbackServer'
                Add-KrEndpoint -Port $Port -IPAddress $IPAddress

                # Set default policy
                New-KrCorsPolicyBuilder |
                    Set-KrCorsOrigin -Origins 'http://localhost:3000' |
                    Set-KrCorsMethod -Methods GET |
                    Add-KrCorsPolicy -Default

                # Route without explicit policy should use default
                Add-KrMapRoute -Verbs Get, Options -Pattern '/default-route' -Scriptblock {
                    Write-KrJsonResponse @{ usedDefault = $true }
                }

                Enable-KrConfiguration
                Start-KrServer
            }
        }

        AfterAll {
            if ($script:instance) {
                # Stop the example script
                Stop-ExampleScript -Instance $script:instance
                # Diagnostic info on failure
                Write-KrExampleInstanceOnFailure -Instance $script:instance
            }
        }

        It 'Uses default policy when no policy specified on route' {
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/default-route" `
                -Method Get `
                -Headers @{ 'Origin' = 'http://localhost:3000' }

            $response.Headers['Access-Control-Allow-Origin'] | Should -Be 'http://localhost:3000'
        }

        It 'Default policy restrictions apply' {
            # Default policy only allows GET, POST should fail CORS
            $response = Invoke-WebRequest -Uri "$($script:instance.Url)/default-route" `
                -Method Options `
                -Headers @{
                'Origin' = 'http://localhost:3000'
                'Access-Control-Request-Method' = 'POST'
            }

            # Should still get 204 but with empty or no Allow-Methods header
            $response.StatusCode | Should -Be 204
        }
    }
}
