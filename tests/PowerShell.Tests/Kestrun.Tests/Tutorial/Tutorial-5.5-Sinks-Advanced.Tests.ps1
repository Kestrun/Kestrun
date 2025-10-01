param()
Describe 'Example 5.5-Sinks-Advanced' -Tag 'Tutorial', 'Logging', 'Sinks' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '5.5-Sinks-Advanced.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'GET /log returns ok text' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/log" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'ok'
    }

    It 'Writes advanced-sinks log line' {
        Invoke-WebRequest -Uri "$($script:instance.Url)/log" -UseBasicParsing -TimeoutSec 6 -Method Get | Out-Null
        $dir = $script:instance.ScriptDirectory
        $logDir = Join-Path $dir 'logs'
        $advLog = Get-ChildItem -Path $logDir -Filter 'advanced-sinks*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $deadline = [DateTime]::UtcNow.AddSeconds(8)
        $ok = $false
        while (-not $ok -and [DateTime]::UtcNow -lt $deadline) {
            if ($advLog -and (Test-Path $advLog.FullName)) {
                $tail = Get-Content $advLog.FullName -Tail 80 -ErrorAction SilentlyContinue
                if ($tail | Where-Object { $_ -match 'Advanced sinks example' }) { $ok = $true; break }
            }
            Start-Sleep -Milliseconds 300
        }
        if (-not $ok -and $advLog) { Write-Host '--- advanced-sinks tail ---'; Get-Content $advLog.FullName -Tail 40 }
        $ok | Should -BeTrue -Because 'Expected Advanced sinks example log line'
    }
}
