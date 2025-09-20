param()
Describe 'Example 1.1-Hello-World' -Tag 'Tutorial' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '1.1-Hello-World.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Hello World route returns expected response' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $port = $script:instance.Port
        $resp = Invoke-WebRequest -Uri "http://127.0.0.1:$port/hello" -UseBasicParsing -TimeoutSec 8
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'Hello, World!'
    }
}
