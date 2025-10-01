param()
Describe 'Example 5.1-Simple-Logging' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '5.1-Simple-Logging.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'hello-powershell returns Hello, World!' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/hello-powershell" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'Hello, World!'
    }

    It 'hello-csharp returns Hello, World!' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/hello-csharp" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'Hello, World!'
    }

    It 'Writes log file with expected entries' {
        # Trigger both routes to ensure entries
        Invoke-WebRequest -Uri "$($script:instance.Url)/hello-powershell" -UseBasicParsing -TimeoutSec 6 -Method Get | Out-Null
        Invoke-WebRequest -Uri "$($script:instance.Url)/hello-csharp" -UseBasicParsing -TimeoutSec 6 -Method Get | Out-Null
        $dir = $script:instance.ScriptDirectory
        $logDir = Join-Path $dir 'logs'
        $logPath = Join-Path $logDir 'sample.log'
        # Rolling hourly means file may have timestamp appended. Earlier examples use pattern sample*.log; adapt.
        if (-not (Test-Path $logPath)) {
            $candidate = Get-ChildItem -Path $logDir -Filter 'sample*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($candidate) { $logPath = $candidate.FullName }
        }
        $deadline = [DateTime]::UtcNow.AddSeconds(8)
        $foundHello = $false
        while (-not $foundHello -and [DateTime]::UtcNow -lt $deadline) {
            if (Test-Path $logPath) {
                $tail = Get-Content $logPath -Tail 120 -ErrorAction SilentlyContinue
                $hasPs = ($tail | Where-Object { $_ -match 'Handling /hello-powershell' })
                $hasCs = ($tail | Where-Object { $_ -match 'hello-csharp' })
                if ($hasPs -and $hasCs) { $foundHello = $true; break }
            }
            Start-Sleep -Milliseconds 300
        }
        if (-not $foundHello -and (Test-Path $logPath)) { Write-Host '--- sample.log tail ---'; Get-Content $logPath -Tail 60 }
        $foundHello | Should -BeTrue -Because "Expected log lines for both PowerShell and C# routes in $logPath"
    }
}
