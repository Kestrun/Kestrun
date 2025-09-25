param()
Describe 'Example 9.3-Binary-Stream' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '9.3-Binary-Stream.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Binary and stream routes return data or 404 gracefully' {
        $logo = Invoke-WebRequest -Uri "$($script:instance.Url)/logo" -UseBasicParsing -TimeoutSec 8 -ErrorAction SilentlyContinue
        if ($logo -and $logo.StatusCode -eq 200) {
            ($logo.Headers['Content-Type'] -join ';') | Should -Match 'image/png'
            ($logo.RawContentLength -gt 0) | Should -BeTrue
        } else {
            $logo.StatusCode | Should -Be 404 -Because 'Binary file may be absent in some dev environments'
        }
        $stream = Invoke-WebRequest -Uri "$($script:instance.Url)/stream" -UseBasicParsing -TimeoutSec 8 -ErrorAction SilentlyContinue
        if ($stream -and $stream.StatusCode -eq 200) {
            ($stream.Headers['Content-Type'] -join ';') | Should -Match 'text/plain'
            ($stream.Content.Trim().Length -gt 0) | Should -BeTrue
        } else {
            $stream.StatusCode | Should -Be 404 -Because 'Stream file may be absent in some dev environments'
        }
    }
}
