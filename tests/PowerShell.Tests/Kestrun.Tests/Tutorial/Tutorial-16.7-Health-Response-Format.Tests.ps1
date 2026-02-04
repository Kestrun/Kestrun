param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 16.7-Health-Response-Format' -Tag 'Tutorial', 'Health' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '16.7-Health-Response-Format.ps1'
    }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    Context 'Auto(Json) format' {
        It 'GET /healthz (JSON) includes Database and Cache probes for core tag' {
            $headers = @{ Accept = 'application/json' }
            $resp = Invoke-WebRequest -Uri "$( $script:instance.Url)/healthz" -UseBasicParsing -TimeoutSec 10 -Method Get -Headers $headers -SkipHttpErrorCheck
            ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 503) | Should -BeTrue
            $parsed = $resp.Content | ConvertFrom-Json
            $probes = @()
            foreach ($name in 'probes', 'checks', 'results') { if ($parsed.PSObject.Properties.Name -contains $name -and $parsed.$name) { $probes += $parsed.$name } }
            $probes.Count | Should -BeGreaterOrEqual 2
            $names = $probes | ForEach-Object { $_.name }
            $names | Should -Contain 'Database'
            $names | Should -Contain 'Cache'
        }
    }

    Context 'YAML format' {
        It 'GET /healthz (Yaml) includes database and cache keys' {
            $headers = @{ Accept = 'application/yaml' }
            $resp = Invoke-WebRequest -Uri "$( $script:instance.Url)/healthz" -UseBasicParsing -TimeoutSec 10 -Method Get -Headers $headers -SkipHttpErrorCheck
            ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 503) | Should -BeTrue
            $body = [string]::new($resp.Content)
            # Normalize potential numeric-line YAML edge cases via helper if needed
            ($body -match '(?i)statusText:') | Should -BeTrue -Because 'YAML should include statusText key'
            ($body -match '(?i)database') | Should -BeTrue -Because 'YAML should contain a Database probe section'
            ($body -match '(?i)cache') | Should -BeTrue -Because 'YAML should contain a Cache probe section'
        }
    }

    Context 'XML format' {
        It 'GET /healthz (Xml) includes Database and Cache elements' {
            $headers = @{ Accept = 'application/xml' }
            $resp = Invoke-WebRequest -Uri "$( $script:instance.Url)/healthz" -UseBasicParsing -TimeoutSec 10 -Method Get -Headers $headers -SkipHttpErrorCheck
            ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 503) | Should -BeTrue
            $xml = [xml]$resp.Content
            $xml | Should -Not -BeNullOrEmpty
            $xml.Response.Probes.ChildNodes.Count | Should -Be 2
            $xml.Response.Probes.ChildNodes.name -contains 'Cache' | Should -BeTrue
            $xml.Response.Probes.ChildNodes.name -contains 'Database' | Should -BeTrue
        }
    }
}
