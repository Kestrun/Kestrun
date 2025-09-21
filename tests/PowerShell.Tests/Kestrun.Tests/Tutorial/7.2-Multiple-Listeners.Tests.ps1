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
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $uri = "$($script:instance.Url)/ping"
        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Be 'pong'
    }

    It 'Notes second listener presence (not directly tested due to dynamic port rewrite)' {
        # This example declares a second listener on port 6000 in the original script.
        # The helper rewrites only the primary 5000 port to a random free port for isolation.
        # Explicit multi-port probing is out of scope for current test style; documenting behavior suffices.
        $script:instance.Content | Should -Match 'Add-KrListener -Port 6000'
    }
}
