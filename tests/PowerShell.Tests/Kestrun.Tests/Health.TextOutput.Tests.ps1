<# Validates that numeric probe data remains numeric in JSON health response
# Arrange: start a minimal server with a single script probe
# Act: call the health endpoint and parse JSON
# Assert: numeric data fields are numbers, not strings
#>
param()
Describe 'Example 16.3-Health-Http-Probe' -Tag 'Tutorial', 'Health' {
    BeforeAll { . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')

        $scriptBlock = {
            param(
                [int]$Port = 5000,
                [IPAddress]$IPAddress = [IPAddress]::Loopback
            )
            New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault
            New-KrServer -Name 'TxtHealth'

            # Add a listener on the configured port and IP address
            Add-KrEndpoint -Port $Port -IPAddress $IPAddress

            Add-KrPowerShellRuntime

            # Add a simple health probe that returns numeric data
            Add-KrHealthProbe -Name 'QuickProbe' -ScriptBlock {
                New-KrProbeResult Healthy 'All good' -Data @{ latencyMs = 5 }
            }

            # Add a health endpoint that returns plain text
            Add-KrHealthEndpoint -Pattern '/healthz' -ResponseContentType Text

            Enable-KrConfiguration

            # Start the server asynchronously
            Start-KrServer -CloseLogsOnExit
        }
        $script:instance = Start-ExampleScript -ScriptBlock $scriptBlock
    }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }


    It 'GET /healthz returns numeric probe data as numbers in plain text' {
        $resp = Invoke-WebRequest -Uri "$( $script:instance.Url)/healthz" -Headers @{ Accept = 'text/plain' } -SkipHttpErrorCheck
        $resp.Content | Should -Not -BeNullOrEmpty
        $resp.Content | Should -Match 'name=QuickProbe'
        $resp.Content | Should -Match 'latencyMs=5'
        $resp.Content | Should -Match 'Status: '
        $resp.Content | Should -Match 'Probes:'
    }
}
