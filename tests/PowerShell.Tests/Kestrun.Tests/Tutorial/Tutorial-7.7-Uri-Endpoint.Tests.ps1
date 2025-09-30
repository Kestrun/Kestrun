param()
Describe 'Example 7.7-Uri-Endpoint' -Tag 'Tutorial', 'Slow' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '7.7-Uri-Endpoint.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'GET /hello returns expected greeting (example currently mirrors basic server)' {
        $uri = "$($script:instance.Url)/hello"
        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Be 'Hello from basic server'
    }
    It 'Example script content should include URI specific concepts (placeholder check)' {
        # The 7.7 example currently appears identical to 7.1 (Basic Server).
        # This test guards future divergence: expecting some form of Uri endpoint usage (e.g., Add-KrEndpoint -Uri ...)
        $script:instance.Content | Should -Match 'Add-KrEndpoint -Uri'
    }
}
