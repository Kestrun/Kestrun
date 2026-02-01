param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 5.7-Apache-Common-Access-Log' -Tag 'Tutorial', 'Logging', 'AccessLog' {
    BeforeAll { $script:instance = Start-ExampleScript -Name '5.7-ApacheLog.ps1' }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /hello returns expected text' {
        try {
            $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/hello" -UseBasicParsing -TimeoutSec 10 -Method Get -ErrorAction Stop
        } catch {
            $_.Exception | Out-String | Write-Host
            throw
        }
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'Hello, World!'
    }

    It 'Writes Apache access log line for /hello' {
        # Trigger a fresh request (independent of previous It) to ensure a recent log entry
        Invoke-WebRequest -Uri "$($script:instance.Url)/hello" -UseBasicParsing -TimeoutSec 10 -Method Get -ErrorAction Stop | Out-Null

        # Candidate log paths (example directory first, then current location)
        $logDirCandidates = @()
        if ($script:instance.ScriptDirectory) { $logDirCandidates += (Join-Path $script:instance.ScriptDirectory 'logs') }
        $logDirCandidates += (Join-Path (Get-Location) 'logs')
        $logPath = $null
        foreach ($d in $logDirCandidates) {
            if (Test-Path $d) {
                $match = Get-ChildItem -Path $d -Filter 'apache_access*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
                if ($match) { $logPath = $match.FullName; break }
            }
        }
        if (-not $logPath) { $logPath = Join-Path $logDirCandidates[0] 'apache_access.log' }

        $loopbackPattern = '(?:127\.0\.0\.1|::1|localhost)'
        # Build regex for Apache access log line, broken into components for readability:
        # ^[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3} [-+][0-9:]+
        #   - ISO-ish local timestamp with ms and offset
        # \[INF\]
        #   - Log level
        # $loopbackPattern - -
        #   - Host (loopback), dashes
        # \[[0-9]{2}/[A-Za-z]{3}/[0-9]{4}:[0-9]{2}:[0-9]{2}:[0-9]{2} \+0000\]
        #   - Bracketed UTC timestamp
        # "GET /hello"
        #   - Request line
        # 200 13
        #   - Status code and size
        # 2-stage validation for robustness:
        #  (1) Find any line containing GET /hello
        #  (2) Validate the Apache access log segment (after the [INF] token) matches expected pattern
        $apacheSegmentPattern = (
            $loopbackPattern + ' - - ' +
            '\[[0-9]{2}/[A-Za-z]{3}/[0-9]{4}:[0-9]{2}:[0-9]{2}:[0-9]{2} \+0000\] ' +
            '"GET /hello(?: HTTP/1\.1)?" 200 13'
        )
        $debugSegments = @()

        $deadline = [DateTime]::UtcNow.AddSeconds(12)
        $matched = $false
        while (-not $matched -and [DateTime]::UtcNow -lt $deadline) {
            if ($logPath -and (Test-Path $logPath)) {
                $lines = Get-Content $logPath -Tail 220 -ErrorAction SilentlyContinue
                $helloLines = $lines | Where-Object { $_ -like '*GET /hello*' }
                foreach ($line in $helloLines) {
                    $segment = $line
                    $infIndex = $line.IndexOf('[INF] ')
                    if ($infIndex -ge 0) { $segment = $line.Substring($infIndex + 6) }
                    $debugSegments += $segment
                    if ($segment -match ('^' + $apacheSegmentPattern)) { $matched = $true; break }
                }
                if ($matched) { break }
            }
            Start-Sleep -Milliseconds 350
        }
        if (-not $matched) {
            if ($logPath -and (Test-Path $logPath)) {
                Write-Host "--- LOG TAIL ($logPath) ---"; Get-Content $logPath -Tail 60 | ForEach-Object { Write-Host $_ }
                if ($debugSegments.Count -gt 0) {
                    Write-Host '--- SEGMENTS INSPECTED ---'
                    $debugSegments | ForEach-Object { Write-Host $_ }
                    Write-Host '--- REGEX USED ---'
                    Write-Host ('^' + $apacheSegmentPattern)
                }
            } else {
                Write-Host "Log file not found. Searched directories: $($logDirCandidates -join ', ')"
            }
        }
        $matched | Should -BeTrue -Because "Expected an Apache access log entry for GET /hello in $logPath"
    }
}
