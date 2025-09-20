param()
Describe 'Example 2.4-Route-Options' -Tag 'Tutorial' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '2.4-Route-Options.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Xml/Yaml/Json/Txt routes respond with 200' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $p = $script:instance.Port
        (Invoke-WebRequest -Uri "http://127.0.0.1:$p/xml/demoText" -UseBasicParsing -TimeoutSec 5 -Method Get).StatusCode | Should -Be 200
        (Invoke-WebRequest -Uri "http://127.0.0.1:$p/yaml?message=fromYaml" -UseBasicParsing -TimeoutSec 5 -Method Get).StatusCode | Should -Be 200
        (Invoke-WebRequest -Uri "http://127.0.0.1:$p/json" -UseBasicParsing -TimeoutSec 5 -Method Get -Headers @{ message = 'fromHeader' }).StatusCode | Should -Be 200
        (Invoke-WebRequest -Uri "http://127.0.0.1:$p/txt?message=fromQuery" -UseBasicParsing -TimeoutSec 5 -Method Get).StatusCode | Should -Be 200
    }
}
