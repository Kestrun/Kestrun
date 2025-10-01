param()

Describe 'Example 5.6-Hot-Reload' -Tag 'Tutorial', 'Logging', 'HotReload' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '5.6-Hot-Reload.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'GET /log emits ok with current level' {
        try {
            $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/log" -UseBasicParsing -TimeoutSec 8 -Method Get -ErrorAction Stop
        } catch {
            $_.Exception | Out-String | Write-Host
            throw
        }
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Match '^ok - '
    }

    It 'GET /level/Warning updates level switch' {
        try {
            $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/level/Warning" -UseBasicParsing -TimeoutSec 8 -Method Get -ErrorAction Stop
        } catch {
            $_.Exception | Out-String | Write-Host
            throw
        }
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'level=Warning'
    }

    It 'GET /level/Invalid returns error (400 BadRequest)' {
        { Invoke-WebRequest -Uri "$($script:instance.Url)/level/NotALevel" -UseBasicParsing -TimeoutSec 8 -Method Get -ErrorAction Stop } | Should -Throw
    }

    It 'Writes hot-reload log lines reflecting level change' {
        # 1. Trigger initial log writes at Debug level
        Invoke-WebRequest -Uri "$($script:instance.Url)/log" -UseBasicParsing -TimeoutSec 8 -Method Get | Out-Null
        $dir = $script:instance.ScriptDirectory
        $logDir = Join-Path $dir 'logs'
        $hotLog = Get-ChildItem -Path $logDir -Filter 'hot-reload*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        Start-Sleep -Milliseconds 300
        if (-not $hotLog) { $hotLog = Get-ChildItem -Path $logDir -Filter 'hot-reload*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1 }
        $baselineDebug = 0; $baselineWarning = 0
        if ($hotLog -and (Test-Path $hotLog.FullName)) {
            $baselineTail = Get-Content $hotLog.FullName -Tail 400 -ErrorAction SilentlyContinue
            # Use array subexpression to ensure single-line matches still produce a collection with .Count
            $baselineDebug = @($baselineTail | Where-Object { $_ -match 'Debug event' }).Count
            $baselineWarning = @($baselineTail | Where-Object { $_ -match 'Warning event' }).Count
        }

        # 2. Raise level to Warning
        Invoke-WebRequest -Uri "$($script:instance.Url)/level/Warning" -UseBasicParsing -TimeoutSec 8 -Method Get | Out-Null

        # 3. Generate another sequence after level change (Debug & Information should now be suppressed)
        Invoke-WebRequest -Uri "$($script:instance.Url)/log" -UseBasicParsing -TimeoutSec 8 -Method Get | Out-Null

        $deadline = [DateTime]::UtcNow.AddSeconds(14)
        $satisfied = $false
        $attempt = 0
        while (-not $satisfied -and [DateTime]::UtcNow -lt $deadline) {
            $attempt++
            $hotLog = Get-ChildItem -Path $logDir -Filter 'hot-reload*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($hotLog -and (Test-Path $hotLog.FullName)) {
                $tail = Get-Content $hotLog.FullName -Tail 260 -ErrorAction SilentlyContinue
                $newDebug = @($tail | Where-Object { $_ -match 'Debug event' }).Count
                $newWarning = @($tail | Where-Object { $_ -match 'Warning event' }).Count
                # Conditions: we have at least one new Warning AND no increase in Debug count beyond baseline
                if ( ($newWarning -gt $baselineWarning) -and ($newDebug -le $baselineDebug) ) {
                    $satisfied = $true
                    break
                }
            }
            Start-Sleep -Milliseconds 450
        }
        if (-not $satisfied -and $hotLog) {
            Write-Host "Baseline Debug=$baselineDebug Warning=$baselineWarning"
            Write-Host '--- hot-reload log tail (failure diagnostics) ---'
            Get-Content $hotLog.FullName -Tail 180
        }
        $satisfied | Should -BeTrue -Because 'Expected Warning events to increase while Debug events are suppressed after level change'
    }
}
