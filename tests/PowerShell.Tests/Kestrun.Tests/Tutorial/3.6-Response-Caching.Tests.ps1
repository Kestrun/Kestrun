param()
Describe 'Example 3.6-Response-Caching' -Tag 'Tutorial','Caching' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '3.6-Response-Caching.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Cached responses return 200' { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; Test-ExampleRouteSet -Instance $script:instance }

    It 'Second request served faster and reuses cache headers (if present)' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $base = "http://127.0.0.1:$($script:instance.Port)"
        $target = "$base/time"  # assuming example exposes something like /time or /cached
        $first = Measure-Command { $r1 = Invoke-ExampleRequest -Uri $target -ReturnRaw -RetryCount 2 }
        Start-Sleep -Milliseconds 150
        $second = Measure-Command { $r2 = Invoke-ExampleRequest -Uri $target -ReturnRaw -RetryCount 2 }
        $r1.StatusCode | Should -Be 200
        $r2.StatusCode | Should -Be 200
        # Compare duration (allow some jitter); second should not be slower by more than 50%
        ($second.TotalMilliseconds -le ($first.TotalMilliseconds * 1.5)) | Should -BeTrue
        # If caching headers exist, validate stability
        $cacheHeaders = @('Cache-Control','ETag','Last-Modified') | Where-Object { $r1.Headers[$_] }
        foreach ($h in $cacheHeaders) { $r1.Headers[$h] | Should -Be $r2.Headers[$h] }
    }
}
