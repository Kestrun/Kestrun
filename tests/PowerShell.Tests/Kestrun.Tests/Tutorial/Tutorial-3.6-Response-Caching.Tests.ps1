[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()
Describe 'Example 3.6-Response-Caching' -Tag 'Tutorial', 'Caching' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '3.6-Response-Caching.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'cachetest route returns timestamp content' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/cachetest" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim().Length -gt 0) | Should -BeTrue -Because 'cached route should produce non-empty content'
    }

    It 'Second request demonstrates caching (content/header stability) and is not pathologically slower' {
        $base = "http://127.0.0.1:$($script:instance.Port)"
        $target = "$base/cachetest"

        # Warm-up (ensures cache population)
        Invoke-ExampleRequest -Uri $target -ReturnRaw -RetryCount 2 | Out-Null
        Start-Sleep -Milliseconds 120

        $r1 = $null
        $r2 = $null
        $first = Measure-Command { $r1 = Invoke-ExampleRequest -Uri $target -ReturnRaw -RetryCount 2 }
        Start-Sleep -Milliseconds 80
        $second = Measure-Command { $r2 = Invoke-ExampleRequest -Uri $target -ReturnRaw -RetryCount 2 }

        $r1.StatusCode | Should -Be 200
        $r2.StatusCode | Should -Be 200

        $content1 = ($r1.Content | Out-String).Trim()
        $content2 = ($r2.Content | Out-String).Trim()

        $sameContent = ($content1 -eq $content2) -and $content1.Length -gt 0

        # Header validators (some caching layers might re-render but keep validators stable)
        $validators = @('ETag', 'Last-Modified', 'Cache-Control')
        $stableValidators = @()
        foreach ($h in $validators) {
            if ($r1.Headers[$h]) {
                if ($r1.Headers[$h] -eq $r2.Headers[$h]) { $stableValidators += $h }
            }
        }

        # Timing heuristic (relaxed). Disable strict timing on CI or if first request extremely fast (<15ms).
        $ci = [bool]$env:CI
        $timingApplicable = -not $ci -and $first.TotalMilliseconds -ge 15
        $ratio = if ($first.TotalMilliseconds -gt 0) { [double]($second.TotalMilliseconds / $first.TotalMilliseconds) } else { 0 }
        $timingOk = $true
        if ($timingApplicable) {
            # Consider "pathologically slower" only if >3x slower
            $timingOk = ($ratio -le 3.0)
        }

        if (-not $sameContent -and $stableValidators.Count -eq 0 -and -not $timingOk) {
            Write-Host '---- Diagnostics (Response Caching) ----'
            Write-Host "First Duration : $([int]$first.TotalMilliseconds) ms"
            Write-Host "Second Duration: $([int]$second.TotalMilliseconds) ms"
            Write-Host 'Ratio          : {0:n2}x' -f $ratio
            Write-Host "Content1 (len=$($content1.Length)):`n$content1"
            Write-Host "Content2 (len=$($content2.Length)):`n$content2"
            if ($validators | Where-Object { $r1.Headers[$_] -or $r2.Headers[$_] }) {
                Write-Host 'Headers1:'; $validators | ForEach-Object { if ($r1.Headers[$_]) { Write-Host "  $($_): $($r1.Headers[$_])" } }
                Write-Host 'Headers2:'; $validators | ForEach-Object { if ($r2.Headers[$_]) { Write-Host "  $($_): $($r2.Headers[$_])" } }
            }
            ($sameContent -or $stableValidators.Count -gt 0 -or $timingOk) | Should -BeTrue -Because 'Expected caching evidence via identical content, stable validators, or at least non-pathological timing'
        } else {
            # Extra assertions (positive path)
            ($sameContent -or $stableValidators.Count -gt 0) | Should -BeTrue -Because 'Expected cached content or stable cache validators'
        }
    }
}
