param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 13.1-Server-Limits' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '13.1-Server-Limits.ps1' -SkipPortProbe
        Wait-ExampleRoute -Instance $script:instance -Route '/online' -TimeoutSeconds 20 | Out-Null
    }
    AfterAll { if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }
    It 'Server limit example exposes the online and info routes' {
        $info = Invoke-ExampleRequest -Uri ("http://127.0.0.1:{0}/info" -f $script:instance.Port) -TimeoutSec 30 -RetryCount 3 -RetryDelayMs 1000 -ReturnRaw
        $info.StatusCode | Should -Be 200
        ($info.Content | ConvertFrom-Json).status | Should -Be 'ok'
    }
}
