param()
Describe 'Example 5.2-Multiple-Loggers-Levels' -Tag 'Tutorial', 'Logging' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '5.2-Multiple-Loggers-Levels.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'GET /info returns info text' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/info" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'info'
    }

    It 'GET /debug returns debug text' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/debug" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'debug'
    }

    It 'GET /audit returns audit text' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/audit" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'audit'
    }

    It 'Writes entries to app.log and audit.log' {
        # Hit routes to generate log lines
        Invoke-WebRequest -Uri "$($script:instance.Url)/info" -UseBasicParsing -TimeoutSec 6 -Method Get | Out-Null
        Invoke-WebRequest -Uri "$($script:instance.Url)/debug" -UseBasicParsing -TimeoutSec 6 -Method Get | Out-Null
        Invoke-WebRequest -Uri "$($script:instance.Url)/audit" -UseBasicParsing -TimeoutSec 6 -Method Get | Out-Null
        $dir = $script:instance.ScriptDirectory
        $logDir = Join-Path $dir 'logs'
        $appLog = Get-ChildItem -Path $logDir -Filter 'app*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $auditLog = Get-ChildItem -Path $logDir -Filter 'audit*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        $ok = $false
        $attempt = 0
        while (-not $ok -and [DateTime]::UtcNow -lt $deadline) {
            $attempt++
            # Always re-glob in case rotation happened mid-test
            $appLog = Get-ChildItem -Path $logDir -Filter 'app*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            $auditLog = Get-ChildItem -Path $logDir -Filter 'audit*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($appLog -and $auditLog -and (Test-Path $appLog.FullName) -and (Test-Path $auditLog.FullName)) {
                $appTail = Get-Content $appLog.FullName -Tail 150 -ErrorAction SilentlyContinue
                $auditTail = Get-Content $auditLog.FullName -Tail 150 -ErrorAction SilentlyContinue
                $hasInfo = $appTail -match 'Info route handled'
                $hasAuditEvent = $auditTail -match 'Audit event recorded'
                # Accept either explicit debug write or initial audit activation line as proof debug-level logging works
                $hasAuditDebug = ($auditTail -match 'Audit debug \(written\)') -or ($auditTail -match 'Audit logger active')
                if ($hasInfo -and $hasAuditEvent -and $hasAuditDebug) { $ok = $true; break }
            }
            Start-Sleep -Milliseconds 400
        }
        if (-not $ok) {
            Write-Host "Attempts: $attempt"
            if ($appLog) { Write-Host "--- app log tail ($($appLog.FullName)) ---"; Get-Content $appLog.FullName -Tail 100 }
            if ($auditLog) { Write-Host "--- audit log tail ($($auditLog.FullName)) ---"; Get-Content $auditLog.FullName -Tail 100 }
        }
        $ok | Should -BeTrue -Because 'Expected log lines in app and audit logs (info handled + audit event + debug/audit activation)'
    }
}
