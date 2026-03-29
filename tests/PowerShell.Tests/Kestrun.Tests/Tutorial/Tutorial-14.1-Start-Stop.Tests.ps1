param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 14.1-Start-Stop' -Tag 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '14.1-Start-Stop.ps1' -SkipPortProbe
        $null = Wait-ExampleRoute -Instance $script:instance -Route '/online' -TimeoutSeconds 20
    }
    AfterAll { if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }
    It 'serves routes before the scripted shutdown' {
        $online = Invoke-WebRequest -Uri ("http://127.0.0.1:{0}/online" -f $script:instance.Port) -UseBasicParsing -TimeoutSec 10
        $online.StatusCode | Should -Be 200
        $online.Content | Should -Be 'OK'

        $status = Invoke-WebRequest -Uri ("http://127.0.0.1:{0}/status" -f $script:instance.Port) -UseBasicParsing -TimeoutSec 10
        $status.StatusCode | Should -Be 200
        ($status.Content | ConvertFrom-Json).status | Should -Be 'healthy'
    }

    It 'stops itself after the scripted delay' {
        $script:instance.Process.WaitForExit(35000) | Out-Null
        $script:instance.Process.HasExited | Should -BeTrue

        {
            Invoke-WebRequest -Uri ("http://127.0.0.1:{0}/status" -f $script:instance.Port) -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        } | Should -Throw
    }
}
