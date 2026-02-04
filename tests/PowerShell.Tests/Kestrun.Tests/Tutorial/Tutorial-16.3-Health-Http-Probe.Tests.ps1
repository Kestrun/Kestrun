param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 16.3-Health-Http-Probe' -Tag 'Tutorial', 'Health' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '16.3-Health-Http-Probe.ps1'
    }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /healthz returns XML with ComponentStatus probe' {
        $resp = Invoke-WebRequest -Uri "$( $script:instance.Url)/healthz" -UseBasicParsing -TimeoutSec 8 -Method Get -SkipHttpErrorCheck
        ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 503) | Should -BeTrue
        # Basic XML presence checks
        $resp.Content | Should -Match '<Health' -Because 'Expected XML root element'
        $resp.Content | Should -Match 'ComponentStatus'
        $resp.Content | Should -Match 'Status' -Because 'Probe status should appear'
    }
}
