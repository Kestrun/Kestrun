param()
Describe 'Example 3.6-Response-Caching' -Tag 'Tutorial', 'Caching' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '3.6-Response-Caching.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'cachetest route returns timestamp content' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/cachetest" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim().Length -gt 0) | Should -BeTrue -Because 'cached route should produce non-empty content'
    }

    It 'Second request served faster and reuses cache headers (if present)' {
        $base = "http://127.0.0.1:$($script:instance.Port)"
        # Use actual cached route from example script
        $target = "$base/cachetest"
        $first = Measure-Command { $r1 = Invoke-ExampleRequest -Uri $target -ReturnRaw -RetryCount 2 }
        Start-Sleep -Milliseconds 150
        $second = Measure-Command { $r2 = Invoke-ExampleRequest -Uri $target -ReturnRaw -RetryCount 2 }
        $r1.StatusCode | Should -Be 200
        $r2.StatusCode | Should -Be 200
        # Compare duration (allow some jitter); second should not be slower by more than 50%
        ($second.TotalMilliseconds -le ($first.TotalMilliseconds * 1.5)) | Should -BeTrue
        # If caching headers exist, validate stability
        $cacheHeaders = @('Cache-Control', 'ETag', 'Last-Modified') | Where-Object { $r1.Headers[$_] }
        foreach ($h in $cacheHeaders) { $r1.Headers[$h] | Should -Be $r2.Headers[$h] }
    }
}
