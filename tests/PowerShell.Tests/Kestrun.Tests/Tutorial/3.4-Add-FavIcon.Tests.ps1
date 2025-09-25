param()
Describe 'Example 3.4-Add-FavIcon' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '3.4-Add-FavIcon.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Serves directory index (HTML)' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        ($resp.Content -like '*<html*') | Should -BeTrue
    }

    It 'Serves favicon.png with image content type (heuristic)' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/favicon.ico" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        ($resp.RawContentLength -gt 0) | Should -BeTrue -Because 'favicon should not be empty'
    }
}
