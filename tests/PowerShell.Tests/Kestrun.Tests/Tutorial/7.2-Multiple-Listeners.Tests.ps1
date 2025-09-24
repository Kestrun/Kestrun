param()
Describe 'Example 7.2-Multiple-Listeners' {
    BeforeAll {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $script:instance = Start-ExampleScript -Name '7.2-Multiple-Listeners.ps1'
    }
    AfterAll {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'GET /ping returns pong on primary listener' {

        $uri = "$($script:instance.Url)/ping"
        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Be 'pong'
    }

    It 'Notes second listener presence (not directly tested due to dynamic port rewrite)' {
        $uri = ($script:instance.Https ? 'https' : 'http') + "://$($script:instance.Host):$($script:instance.Port+433)/ping"

        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Be 'pong'
    }
}
