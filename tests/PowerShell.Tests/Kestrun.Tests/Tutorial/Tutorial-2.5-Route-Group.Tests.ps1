param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}
Describe 'Example 2.5-Route-Group' -Tag 'Tutorial' {
    BeforeAll { $script:instance = Start-ExampleScript -Name '2.5-Route-Group.ps1' }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }
    It 'Grouped parameter routes return expected content for Get' {
        # Path (GET)
        $resp = Invoke-ExampleRequest -Uri "$($script:instance.Url)/input/demoPath" -Method Get -TimeoutSec 15 -RetryCount 2 -RetryDelayMs 1000 -ReturnRaw
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be "The Path Parameter 'value' was: demoPath"
    }
    It 'Grouped parameter routes return expected content for PATCH' {
        # Query (PATCH)
        $resp = Invoke-ExampleRequest -Uri "$($script:instance.Url)/input?value=demoQuery" -Method Patch -TimeoutSec 15 -RetryCount 2 -RetryDelayMs 1000 -ReturnRaw
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be "The Query String 'value' was: demoQuery"
    }
    It 'Grouped parameter routes return expected content for POST' {
        # Body (POST)
        $body = @{ value = 'demoBody' } | ConvertTo-Json
        $resp = Invoke-ExampleRequest -Uri "$($script:instance.Url)/input" -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 15 -RetryCount 2 -RetryDelayMs 1000 -ReturnRaw
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be "The Body Parameter 'value' was: demoBody"
    }
    It 'Grouped parameter routes return expected content for PUT' {
        # Header (PUT)
        $resp = Invoke-ExampleRequest -Uri "$($script:instance.Url)/input" -Method Put -Headers @{ value = 'demoHeader' } -TimeoutSec 15 -RetryCount 2 -RetryDelayMs 1000 -ReturnRaw
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be "The Header Parameter 'value' was: demoHeader"
    }
    It 'Grouped parameter routes return expected content for DELETE' {
        # Cookie (DELETE)
        $resp = Invoke-ExampleRequest -Uri "$($script:instance.Url)/input" -Method Delete -Headers @{ Cookie = 'value=demoCookie' } -TimeoutSec 15 -RetryCount 2 -RetryDelayMs 1000 -ReturnRaw
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be "The Cookie Parameter 'value' was: demoCookie"
    }
}
