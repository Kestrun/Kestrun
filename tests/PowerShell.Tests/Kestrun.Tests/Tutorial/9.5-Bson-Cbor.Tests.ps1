param()
Describe 'Example 9.5-Bson-Cbor' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '9.5-Bson-Cbor.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'BSON and CBOR routes return binary content types and plain JSON route is valid' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $p = $script:instance.Port
        $bson = Invoke-WebRequest -Uri "http://127.0.0.1:$p/bson" -UseBasicParsing -TimeoutSec 8
        $bson.StatusCode | Should -Be 200
        ($bson.Headers['Content-Type'] -join ';') | Should -Match 'application/bson'
        ($bson.Content.Length -gt 0) | Should -BeTrue
        $cbor = Invoke-WebRequest -Uri "http://127.0.0.1:$p/cbor" -UseBasicParsing -TimeoutSec 8
        $cbor.StatusCode | Should -Be 200
        ($cbor.Headers['Content-Type'] -join ';') | Should -Match 'application/cbor'
        ($cbor.Content.Length -gt 0) | Should -BeTrue
        $plain = Invoke-WebRequest -Uri "http://127.0.0.1:$p/plain" -UseBasicParsing -TimeoutSec 8
        $plain.StatusCode | Should -Be 200
        $plain.Content | Should -Match '"kind"\s*:\s*"json"'
    }
}
