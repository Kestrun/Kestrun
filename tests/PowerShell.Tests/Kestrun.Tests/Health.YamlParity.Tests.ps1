<#
Validates that numeric probe data remains numeric in YAML health response.
Strategy:
  * Spin up server with YAML response preference.
  * Retrieve raw YAML via Invoke-WebRequest (Invoke-RestMethod would try to parse JSON only; YAML stays string anyway but WebRequest gives us .Content).
  * Parse with ConvertFrom-Yaml (PowerShell 7+ builtin) then assert numeric node types.
  * Mirror JSON numeric test semantics (intVal integer, floatVal floating point).
#>
param()
BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'Validates numeric probe data representation in YAML health response' -Tag 'Health' {
    BeforeAll {
        $scriptBlock = {
            param(
                [int]$Port = 5000,
                [IPAddress]$IPAddress = [IPAddress]::Loopback
            )
            New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault
            New-KrServer -Name 'YamlParity'

            # Add a listener on the configured port and IP address
            Add-KrEndpoint -Port $Port -IPAddress $IPAddress

            # Add a simple health probe that returns numeric data
            Add-KrHealthProbe -Name 'NumProbe' -Scriptblock {
                New-KrProbeResult Healthy 'OK' -Data @{ intVal = 42; floatVal = 12.5 }
            }

            # Add a health endpoint that returns YAML
            Add-KrHealthEndpoint -Pattern '/healthz' -ResponseContentType Yaml

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


    It 'GET /healthz returns numeric probe data as numbers in YAML' {
        $resp = Invoke-WebRequest -Uri "$( $script:instance.Url)/healthz" -Headers @{ Accept = 'application/yaml' } -SkipHttpErrorCheck
        $resp | Should -Not -BeNullOrEmpty
        $resp.Content | Should -Not -BeNullOrEmpty
        $yamlText = [string]::new($resp.Content)
        $yamlText | Should -Not -BeNullOrEmpty
        $parsed = ConvertFrom-KrYaml -Yaml $yamlText
        $probe = $parsed.Probes | Where-Object { $_.Name -eq 'NumProbe' }
        $probe | Should -Not -BeNullOrEmpty

        $probe.Data.intVal | Should -Not -BeNullOrEmpty
        $probe.Data.floatVal | Should -Not -BeNullOrEmpty

        ( ($probe.Data.intVal -is [int] -or $probe.Data.intVal -is [long])) | Should -BeTrue
        @('double', 'float', 'decimal') -contains $probe.Data.floatVal.GetType().Name | Should -BeTrue
    }
}
