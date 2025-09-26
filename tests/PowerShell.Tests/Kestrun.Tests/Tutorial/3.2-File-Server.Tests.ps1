param()
Describe 'Example 3.2-File-Server' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '3.2-File-Server.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Root directory listing returns HTML with file links' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        ($resp.Content -like '*index.html*') | Should -BeTrue -Because 'Directory listing should mention index.html'
    }

    It 'Fetches index.html directly' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/index.html" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        ($resp.Content -like '*<html*') | Should -BeTrue -Because 'index.html should contain HTML markup'
    }
}
