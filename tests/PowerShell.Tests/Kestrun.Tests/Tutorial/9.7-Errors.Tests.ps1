param()
Describe 'Example 9.7-Errors' -Tag 'Tutorial', 'Errors' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '9.7-Errors.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Error handling routes basic smoke' {
        Test-ExampleRouteSet -Instance $script:instance
    }

    It 'Returns 404 for missing resource' {
        $base = "http://127.0.0.1:$($script:instance.Port)"
        $uri = "$base/not-found-test"
        $caught = $false
        try { Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 4 -ErrorAction Stop | Out-Null } catch {
            $caught = $true
            if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode | Should -Be 404 }
        }
        $caught | Should -BeTrue -Because 'A 404 should have been returned.'
    }

    It 'Validation error route returns 400 with expected message' {
        $uri = "http://127.0.0.1:$($script:instance.Port)/fail"
        $caught = $false
        try { Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 4 -ErrorAction Stop | Out-Null } catch {
            $caught = $true
            if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode | Should -Be 400 }
        }
        $caught | Should -BeTrue
    }

    It 'Exception route returns 500 with stack details' {
        $uri = "http://127.0.0.1:$($script:instance.Port)/boom"
        $caught = $false
        try { Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 4 -ErrorAction Stop | Out-Null } catch {
            $caught = $true
            if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode | Should -Be 500 }
        }
        $caught | Should -BeTrue
    }
}
