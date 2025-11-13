param()
Describe 'Example 4.2-Shared-State' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '4.2-Shared-State.ps1'
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
        }
    }

    Context 'Basic Routes' {
        It 'GET /info returns application info with 200' {
            $url = "$($script:instance.Url)/info"
            $response = Invoke-RestMethod -Uri $url -Method Get
            $response | Should -Not -BeNullOrEmpty
            $response.appName | Should -Be 'SharedStateDemo'
            $response.maxVisits | Should -Be 100
            $response.uptimeSeconds | Should -BeGreaterThan 0
            $response.runspace | Should -Not -BeNullOrEmpty
        }

        It 'GET /visits returns current visit count with 200' {
            $url = "$($script:instance.Url)/visits"
            $response = Invoke-RestMethod -Uri $url -Method Get
            $response | Should -Not -BeNullOrEmpty
            $response.count | Should -BeGreaterOrEqual 0
            $response.runspace | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Visit Counter Operations' {
        BeforeEach {
            # Reset counter before each test
            $resetUrl = "$($script:instance.Url)/reset"
            Invoke-RestMethod -Uri $resetUrl -Method Post | Out-Null
        }

        It 'POST /visit increments visit counter' {
            $url = "$($script:instance.Url)/visit"
            $response = Invoke-RestMethod -Uri $url -Method Post
            $response.count | Should -Be 1
            $response.maxVisits | Should -Be 100
            $response.remaining | Should -Be 99
        }

        It 'POST /visit increments counter sequentially' {
            $url = "$($script:instance.Url)/visit"
            1..5 | ForEach-Object {
                $response = Invoke-RestMethod -Uri $url -Method Post
                $response.count | Should -Be $_
                $response.remaining | Should -Be (100 - $_)
            }
        }

        It 'POST /reset resets counter to zero' {
            # Increment a few times
            $visitUrl = "$($script:instance.Url)/visit"
            1..3 | ForEach-Object { Invoke-RestMethod -Uri $visitUrl -Method Post | Out-Null }

            # Reset
            $resetUrl = "$($script:instance.Url)/reset"
            $response = Invoke-RestMethod -Uri $resetUrl -Method Post
            $response.message | Should -Be 'Visit counter reset'
            $response.count | Should -Be 0

            # Verify counter is actually reset
            $checkUrl = "$($script:instance.Url)/visits"
            $checkResponse = Invoke-RestMethod -Uri $checkUrl -Method Get
            $checkResponse.count | Should -Be 0
        }

        It 'Concurrent POST /visit requests maintain accurate count' {
            $resetUrl = "$($script:instance.Url)/reset"
            Invoke-RestMethod -Uri $resetUrl -Method Post | Out-Null

            $url = "$($script:instance.Url)/visit"
            $jobs = 1..20 | ForEach-Object {
                Start-Job -ScriptBlock {
                    param($uri)
                    Invoke-RestMethod -Uri $uri -Method Post
                } -ArgumentList $url
            }

            $results = $jobs | Wait-Job | Receive-Job
            $jobs | Remove-Job
            start-sleep -Seconds 2  # Allow slight delay for state update
            # Verify final count
            $checkUrl = "$($script:instance.Url)/visits"
            $finalResponse = Invoke-RestMethod -Uri $checkUrl -Method Get
            $finalResponse.count | Should -Be 20
        }
    }

    Context 'Configuration Management' {
        BeforeAll {
            # Save original config
            $infoUrl = "$($script:instance.Url)/info"
            $script:originalConfig = Invoke-RestMethod -Uri $infoUrl -Method Get
        }

        AfterAll {
            # Restore original config
            $configUrl = "$($script:instance.Url)/config"
            $body = @{
                maxVisits = $script:originalConfig.maxVisits
                appName = $script:originalConfig.appName
            } | ConvertTo-Json
            Invoke-RestMethod -Uri $configUrl -Method Put -Body $body -ContentType 'application/json' | Out-Null
        }

        It 'PUT /config updates maxVisits' {
            $configUrl = "$($script:instance.Url)/config"
            $body = @{ maxVisits = 200 } | ConvertTo-Json
            $response = Invoke-RestMethod -Uri $configUrl -Method Put -Body $body -ContentType 'application/json'

            $response.message | Should -Be 'Configuration updated'
            [int]$response.config.MaxVisits | Should -Be 200
        }

        It 'PUT /config updates appName' {
            $configUrl = "$($script:instance.Url)/config"
            $body = @{ appName = 'UpdatedDemo' } | ConvertTo-Json
            $response = Invoke-RestMethod -Uri $configUrl -Method Put -Body $body -ContentType 'application/json'

            $response.message | Should -Be 'Configuration updated'
            $response.config.AppName | Should -Be 'UpdatedDemo'
        }

        It 'PUT /config updates both maxVisits and appName' {
            $configUrl = "$($script:instance.Url)/config"
            $body = @{
                maxVisits = 250
                appName = 'FullUpdate'
            } | ConvertTo-Json
            $response = Invoke-RestMethod -Uri $configUrl -Method Put -Body $body -ContentType 'application/json'

            $response.config.MaxVisits | Should -Be 250
            $response.config.AppName | Should -Be 'FullUpdate'

            # Verify persistence via /info
            $infoUrl = "$($script:instance.Url)/info"
            $info = Invoke-RestMethod -Uri $infoUrl -Method Get
            $info.maxVisits | Should -Be 250
            $info.appName | Should -Be 'FullUpdate'
        }
    }

    Context 'Shared State Query Operations' {
        It 'GET /state/Config returns configuration state' {
            $url = "$($script:instance.Url)/state/Config"
            $response = Invoke-RestMethod -Uri $url -Method Get
            $response.name | Should -Be 'Config'
            $response.value | Should -Not -BeNullOrEmpty
            [int]$response.value.MaxVisits | Should -BeGreaterThan 0
            $response.value.AppName | Should -BeOfType [string]
            $response.type | Should -Match 'Hashtable'
        }

        It 'GET /state/Visits returns visit state' {
            $url = "$($script:instance.Url)/state/Visits"
            $response = Invoke-RestMethod -Uri $url -Method Get
            $response.name | Should -Be 'Visits'
            $response.value | Should -Not -BeNullOrEmpty
            [int]$response.value.Count | Should -BeGreaterOrEqual 0
        }

        It 'GET /state/StartTime returns server start time' {
            $url = "$($script:instance.Url)/state/StartTime"
            $response = Invoke-RestMethod -Uri $url -Method Get
            $response.name | Should -Be 'StartTime'
            $response.value | Should -Not -BeNullOrEmpty
            $response.value.Time | Should -Not -BeNullOrEmpty
            # Verify it's a valid datetime string
            { [datetime]$response.value.Time } | Should -Not -Throw
        }

        It 'GET /state/{nonexistent} returns 404' {
            $url = "$($script:instance.Url)/state/NonExistent"
            { Invoke-RestMethod -Uri $url -Method Get -ErrorAction Stop } | Should -Throw
            try {
                Invoke-RestMethod -Uri $url -Method Get -ErrorAction Stop
            } catch {
                $_.Exception.Response.StatusCode.value__ | Should -Be 404
            }
        }
    }

    Context 'Shared State Removal Operations' {
        It 'DELETE /state/{name} for core variables returns 403' {
            @('Visits', 'Config', 'StartTime') | ForEach-Object {
                $url = "$($script:instance.Url)/state/$_"
                { Invoke-RestMethod -Uri $url -Method Delete -ErrorAction Stop } | Should -Throw
                try {
                    Invoke-RestMethod -Uri $url -Method Delete -ErrorAction Stop
                } catch {
                    $_.Exception.Response.StatusCode.value__ | Should -Be 403
                }
            }
        }

        It 'DELETE /state/{name} for non-existent variable returns 404' {
            $url = "$($script:instance.Url)/state/NonExistent"
            { Invoke-RestMethod -Uri $url -Method Delete -ErrorAction Stop } | Should -Throw
            try {
                Invoke-RestMethod -Uri $url -Method Delete -ErrorAction Stop
            } catch {
                $_.Exception.Response.StatusCode.value__ | Should -Be 404
            }
        }
    }

    Context 'Cross-Runspace State Sharing' {
        It 'State is shared across different runspaces' {
            # Reset counter
            $resetUrl = "$($script:instance.Url)/reset"
            Invoke-RestMethod -Uri $resetUrl -Method Post | Out-Null

            # Make concurrent requests
            $url = "$($script:instance.Url)/visit"
            $jobs = 1..10 | ForEach-Object {
                Start-Job -ScriptBlock {
                    param($uri)
                    try {
                        Invoke-RestMethod -Uri $uri -Method Post -ErrorAction Stop
                    } catch {
                        Write-Error "Request failed: $_"
                        $null
                    }
                } -ArgumentList $url
            }

            $results = $jobs | Wait-Job | Receive-Job -ErrorAction SilentlyContinue
            $jobs | Remove-Job

            # Collect unique runspace names (filter out null/empty results)
            $validResults = $results | Where-Object { $null -ne $_ -and $null -ne $_.PSObject.Properties['runspace'] }

            # Should have received valid results
            $validResults.Count | Should -BeGreaterThan 0

            $runspaces = @($validResults | Select-Object -ExpandProperty runspace | Where-Object { $_ } | Sort-Object -Unique)

            # Should have used multiple runspaces
            $runspaces.Count | Should -BeGreaterThan 1

            # But final count should be accurate (all increments were atomic)
            $checkUrl = "$($script:instance.Url)/visits"
            $finalResponse = Invoke-RestMethod -Uri $checkUrl -Method Get
            [int]$finalResponse.count | Should -Be 10
        }
    }
}
