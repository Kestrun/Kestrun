<# Validates that numeric probe data remains numeric in JSON health response
# Arrange: start a minimal server with a single script probe
# Act: call the health endpoint and parse JSON
# Assert: numeric data fields are numbers, not strings
#>
param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Health Checks numeric data' -Tag 'Health' {
    BeforeAll {
        $scriptBlock = {
            param(
                [int]$Port = 5000,
                [IPAddress]$IPAddress = [IPAddress]::Loopback
            )
            New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault
            New-KrServer -Name 'NumericTest'

            # Add a listener on the configured port and IP address
            Add-KrEndpoint -Port $Port -IPAddress $IPAddress

            # Add a simple health probe that returns numeric data
            Add-KrHealthProbe -Name 'NumProbe' -Scriptblock {
                $data = @{ connectionTimeMs = 42; latencyMs = 12.5 }
                New-KrProbeResult Healthy 'OK' -Data $data
            }

            # Add a health endpoint that returns JSON
            Add-KrHealthEndpoint -Pattern '/healthz' -ResponseContentType Json

            Enable-KrConfiguration

            # Start the server asynchronously
            Start-KrServer -CloseLogsOnExit
        }
        $script:instance = Start-ExampleScript -Scriptblock $scriptBlock
    }
    AfterAll { if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /healthz returns numeric probe data as numbers in JSON' {
        $json = Invoke-RestMethod -Uri "$( $script:instance.Url)/healthz" -Headers @{ Accept = 'application/json' } -SkipHttpErrorCheck
        $probe = $json.probes | Where-Object { $_.name -eq 'NumProbe' }
        $probe | Should -Not -BeNullOrEmpty
        ($probe.data.connectionTimeMs -is [int] -or $probe.data.connectionTimeMs -is [long]) | Should -BeTrue
        ($probe.data.latencyMs -is [double] -or $probe.data.latencyMs -is [float]) | Should -BeTrue
        # Assert numeric types (PowerShell will deserialize numbers as Int64 / Double)
        $probe.data.connectionTimeMs | Should -Be 42
        $probe.data.latencyMs | Should -Be 12.5
    }
}
