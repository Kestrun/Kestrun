param()
Describe 'Example 7.7-Uri-Endpoint' -Tag 'Tutorial', 'Slow' {
    BeforeAll {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $script:instance = Start-ExampleScript -Name '7.7-Uri-Endpoint.ps1'
    }
    AfterAll {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }
    It 'GET /hello returns expected greeting (example currently mirrors basic server)' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $uri = "$($script:instance.Url)/hello"
        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Be 'Hello from basic server'
    }
    It 'Example script content should include URI specific concepts (placeholder check)' {
        # The 7.7 example currently appears identical to 7.1 (Basic Server).
        # This test guards future divergence: expecting some form of Uri endpoint usage (e.g., Add-KrListener -Uri ...)
        $script:instance.Content | Should -Match 'Add-KrListener -Uri'
    }
}
