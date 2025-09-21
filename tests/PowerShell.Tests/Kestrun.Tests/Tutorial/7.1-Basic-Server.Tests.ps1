param()
Describe 'Example 7.1-Basic-Server' {
    BeforeAll {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $script:instance = Start-ExampleScript -Name '7.1-Basic-Server.ps1'
    }
    AfterAll {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'GET /hello returns expected greeting' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $uri = "$($script:instance.Url)/hello"
        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Be 'Hello from basic server'
    }
}
