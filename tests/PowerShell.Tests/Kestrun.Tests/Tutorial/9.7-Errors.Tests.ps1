param()
Describe 'Example 9.7-Errors' -Tag 'Tutorial','Errors' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '9.7-Errors.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Error handling routes basic smoke' { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; Test-ExampleRouteSet -Instance $script:instance }

    It 'Returns 404 for missing resource' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $base = "http://127.0.0.1:$($script:instance.Port)"
        $uri = "$base/not-found-test"
        $caught = $false
        try { Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 4 -ErrorAction Stop | Out-Null } catch {
            $caught = $true
            if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode | Should -Be 404 }
        }
        $caught | Should -BeTrue -Because 'A 404 should have been returned.'
    }

    It 'Returns 500 for forced error endpoint (if present)' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $base = "http://127.0.0.1:$($script:instance.Port)"
        $uri = "$base/error/throw"
        $caught = $false
        try { Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 4 -ErrorAction Stop | Out-Null } catch {
            $caught = $true
            if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode | Should -BeIn 500,501,502 }
        }
        $caught | Should -BeTrue -Because 'An internal error should have been produced.'
    }
}
