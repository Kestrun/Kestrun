param()
Describe 'Example 3.3-Static-OverrideRoutes' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '3.3-Static-OverrideRoutes.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Serves static asset index.html under /assets' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/assets/index.html" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        ($resp.Content -like '*<html*') | Should -BeTrue
    }

    It 'Override route returns expected JSON payload' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/assets/override/pwsh" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $obj = $resp.Content | ConvertFrom-Json
        $obj.status | Should -Be 'ok'
        $obj.message | Should -Be 'Static override works!'
    }
}
