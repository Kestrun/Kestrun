param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}
Describe 'Example 3.1-Static-Routes' {
    BeforeAll { $script:instance = Start-ExampleScript -Name '3.1-Static-Routes.ps1' }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Serves index.html content' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/assets/index.html" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        ($resp.Content -like '*<html*') | Should -BeTrue -Because 'Index HTML should contain markup'
    }

    It 'Serves a nested file (about.html)' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/assets/about.html" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        ($resp.Content -like '*About*') | Should -BeTrue -Because 'about.html should contain About'
    }
}
