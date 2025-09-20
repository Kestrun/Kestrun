param()
Describe 'Example 2.5-Route-Group' -Tag 'Tutorial' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '2.5-Route-Group.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Grouped parameter routes respond with 200 for all verbs' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $p = $script:instance.Port
        # Path (GET)
        (Invoke-WebRequest -Uri "http://127.0.0.1:$p/input/demoPath" -UseBasicParsing -TimeoutSec 5 -Method Get).StatusCode | Should -Be 200
        # Query (PATCH)
        (Invoke-WebRequest -Uri "http://127.0.0.1:$p/input?value=demoQuery" -UseBasicParsing -TimeoutSec 5 -Method Patch).StatusCode | Should -Be 200
        # Body (POST)
        $body = @{ value = 'demoBody' } | ConvertTo-Json
        (Invoke-WebRequest -Uri "http://127.0.0.1:$p/input" -UseBasicParsing -TimeoutSec 5 -Method Post -Body $body -ContentType 'application/json').StatusCode | Should -Be 200
        # Header (PUT)
        (Invoke-WebRequest -Uri "http://127.0.0.1:$p/input" -UseBasicParsing -TimeoutSec 5 -Method Put -Headers @{ value = 'demoHeader' }).StatusCode | Should -Be 200
        # Cookie (DELETE)
        (Invoke-WebRequest -Uri "http://127.0.0.1:$p/input" -UseBasicParsing -TimeoutSec 5 -Method Delete -Headers @{ Cookie = 'value=demoCookie' }).StatusCode | Should -Be 200
    }
}
