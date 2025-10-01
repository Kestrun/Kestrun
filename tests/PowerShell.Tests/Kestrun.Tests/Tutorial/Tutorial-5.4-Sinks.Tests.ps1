param()
Describe 'Example 5.4-Sinks' -Tag 'Tutorial', 'Logging', 'Sinks' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '5.4-Sinks.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'GET /text returns text' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/text" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'text'
    }

    It 'GET /json returns json text' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/json" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'json'
    }

    It 'Writes sinks-text and sinks-json log files' {
        # Exercise endpoints again
        Invoke-WebRequest -Uri "$($script:instance.Url)/text" -UseBasicParsing -TimeoutSec 6 -Method Get | Out-Null
        Invoke-WebRequest -Uri "$($script:instance.Url)/json" -UseBasicParsing -TimeoutSec 6 -Method Get | Out-Null
        $dir = $script:instance.ScriptDirectory
        $logDir = Join-Path $dir 'logs'
        $textLog = Get-ChildItem -Path $logDir -Filter 'sinks-text*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $jsonLog = Get-ChildItem -Path $logDir -Filter 'sinks-json*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $deadline = [DateTime]::UtcNow.AddSeconds(8)
        $okText = $false; $okJson = $false
        while ([DateTime]::UtcNow -lt $deadline -and (-not ($okText -and $okJson))) {
            if ($textLog -and (Test-Path $textLog.FullName)) {
                $tail = Get-Content $textLog.FullName -Tail 80 -ErrorAction SilentlyContinue
                if (-not $okText) { $okText = ($tail | Where-Object { $_ -match 'Text sink example' }) -ne $null }
            }
            if ($jsonLog -and (Test-Path $jsonLog.FullName)) {
                $jsonTail = Get-Content $jsonLog.FullName -Tail 80 -ErrorAction SilentlyContinue
                if (-not $okJson) { $okJson = ($jsonTail | Where-Object { $_ -match 'Json sink example' }) -ne $null }
            }
            Start-Sleep -Milliseconds 300
        }
        if (-not ($okText -and $okJson)) {
            if ($textLog) { Write-Host '--- sinks-text tail ---'; Get-Content $textLog.FullName -Tail 40 }
            if ($jsonLog) { Write-Host '--- sinks-json tail ---'; Get-Content $jsonLog.FullName -Tail 40 }
        }
        ($okText -and $okJson) | Should -BeTrue -Because 'Expected log entries in sinks-text and sinks-json logs'
    }
}
