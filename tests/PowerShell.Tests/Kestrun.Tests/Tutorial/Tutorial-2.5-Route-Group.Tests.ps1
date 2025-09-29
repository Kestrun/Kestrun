param()
Describe 'Example 2.5-Route-Group' -Tag 'Tutorial' {
    BeforeAll {. (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '2.5-Route-Group.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Grouped parameter routes return expected content for Get' {
        # Path (GET)
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/input/demoPath" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be "The Path Parameter 'value' was: demoPath"
    }
    It 'Grouped parameter routes return expected content for PATCH' {
        # Query (PATCH)
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/input?value=demoQuery" -UseBasicParsing -TimeoutSec 6 -Method Patch
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be "The Query String 'value' was: demoQuery"
    }
    It 'Grouped parameter routes return expected content for POST' {
        # Body (POST)
        $body = @{ value = 'demoBody' } | ConvertTo-Json
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/input" -UseBasicParsing -TimeoutSec 6 -Method Post -Body $body -ContentType 'application/json'
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be "The Body Parameter 'value' was: demoBody"
    }
    It 'Grouped parameter routes return expected content for PUT' {
        # Header (PUT)
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/input" -UseBasicParsing -TimeoutSec 6 -Method Put -Headers @{ value = 'demoHeader' }
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be "The Header Parameter 'value' was: demoHeader"
    }
    It 'Grouped parameter routes return expected content for DELETE' {
        # Cookie (DELETE)
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/input" -UseBasicParsing -TimeoutSec 6 -Method Delete -Headers @{ Cookie = 'value=demoCookie' }
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be "The Cookie Parameter 'value' was: demoCookie"
    }
}
