param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}
Describe 'Example 5.3-Enrichment-Correlation-IDs' -Tag 'Tutorial', 'Logging', 'Correlation' {
    BeforeAll { $script:instance = Start-ExampleScript -Name '5.3-Enrichment-Correlation-IDs.ps1' }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /ps-correlation returns correlation id' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/ps-correlation" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Match '^ps-correlation: [0-9a-f]{32}$'
    }

    It 'GET /csharp-correlation returns correlation id' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/csharp-correlation" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Match '^csharp-correlation: [0-9a-f]{32}$'
    }

    It 'Writes enrichment log with correlation ids' {
        # Hit both routes again to ensure fresh entries
        Invoke-WebRequest -Uri "$($script:instance.Url)/ps-correlation" -UseBasicParsing -TimeoutSec 6 -Method Get | Out-Null
        Invoke-WebRequest -Uri "$($script:instance.Url)/csharp-correlation" -UseBasicParsing -TimeoutSec 6 -Method Get | Out-Null
        $dir = $script:instance.ScriptDirectory
        $logDir = Join-Path $dir 'logs'
        $enrichmentLog = Get-ChildItem -Path $logDir -Filter 'enrichment*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        $hasPs = $false; $hasCs = $false
        while ([DateTime]::UtcNow -lt $deadline -and (-not ($hasPs -and $hasCs))) {
            if ($enrichmentLog -and (Test-Path $enrichmentLog.FullName)) {
                $tail = Get-Content $enrichmentLog.FullName -Tail 120 -ErrorAction SilentlyContinue
                if (-not $hasPs) { $hasPs = ($tail | Where-Object { $_ -match 'Handling /ps-correlation' }) -ne $null }
                if (-not $hasCs) { $hasCs = ($tail | Where-Object { $_ -match 'Handling /csharp-correlation' }) -ne $null }
            }
            Start-Sleep -Milliseconds 350
        }
        if (-not ($hasPs -and $hasCs) -and $enrichmentLog) { Write-Host '--- enrichment log tail ---'; Get-Content $enrichmentLog.FullName -Tail 80 }
        ($hasPs -and $hasCs) | Should -BeTrue -Because 'Expected correlation handling lines in enrichment log'
    }
}
