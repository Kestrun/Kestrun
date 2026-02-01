param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 9.5-Bson-Cbor' {
    BeforeAll { $script:instance = Start-ExampleScript -Name '9.5-Bson-Cbor.ps1' }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'BSON route returns expected content' {
        $bson = Invoke-WebRequest -Uri "$($script:instance.Url)/bson" -UseBasicParsing -TimeoutSec 8
        $bson.StatusCode | Should -Be 200
        ($bson.Headers['Content-Type'] -join ';') | Should -Match 'application/bson'
        ($bson.Content.Length -gt 0) | Should -BeTrue
    }

    It 'CBOR route returns expected content' {
        $cbor = Invoke-WebRequest -Uri "$($script:instance.Url)/cbor" -UseBasicParsing -TimeoutSec 8
        $cbor.StatusCode | Should -Be 200
        ($cbor.Headers['Content-Type'] -join ';') | Should -Match 'application/cbor'
        ($cbor.Content.Length -gt 0) | Should -BeTrue
    }

    It 'Plain JSON route returns expected content' {
        $plain = Invoke-WebRequest -Uri "$($script:instance.Url)/plain" -UseBasicParsing -TimeoutSec 8
        $plain.StatusCode | Should -Be 200
        $plain.Content | Should -Match '"kind"\s*:\s*"json"'
    }
}
