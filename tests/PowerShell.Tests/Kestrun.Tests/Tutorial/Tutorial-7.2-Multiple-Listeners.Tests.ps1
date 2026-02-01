param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 7.2-Multiple-Listeners' {
    BeforeAll { $script:instance = Start-ExampleScript -Name '7.2-Multiple-Listeners.ps1' }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /ping returns pong on primary listener' {

        $uri = "$($script:instance.Url)/ping"
        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Be 'pong'
    }

    It 'GET /ping returns pong on secondary listener' {
        $uri = ($script:instance.Https ? 'https' : 'http') + "://$($script:instance.Host):$($script:instance.Port + 433)/ping"

        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Be 'pong'
    }
}
