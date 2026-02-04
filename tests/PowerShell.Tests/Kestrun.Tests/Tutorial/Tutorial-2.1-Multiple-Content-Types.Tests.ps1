param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}
Describe 'Example 2.1-Multiple-Content-Types' -Tag 'Tutorial' {
    BeforeAll { $script:instance = Start-ExampleScript -Name '2.1-Multiple-Content-Types.ps1' }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }
    It 'Expected Text response from /hello' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/hello" -UseBasicParsing -TimeoutSec 5 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'Hello, World!'
        $resp.Headers.'Content-Type' | Should -Match 'text/plain'
    }
    It 'Expected Json response from /hello-json' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/hello-json" -UseBasicParsing -TimeoutSec 5 -Method Get
        $resp.StatusCode | Should -Be 200
        ($resp.Content | ConvertFrom-Json).message | Should -Be 'Hello, World!'
        $resp.Headers.'Content-Type' | Should -Match 'application/json'
    }
    It 'Expected Xml response from /hello-xml' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/hello-xml" -UseBasicParsing -TimeoutSec 5 -Method Get
        $resp.StatusCode | Should -Be 200
        ([xml]$resp.Content).Response.message | Should -Be 'Hello, World!'
        $resp.Headers.'Content-Type' | Should -Match 'application/xml'
    }
    It 'Expected Yaml response from /hello-yaml' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/hello-yaml" -UseBasicParsing -TimeoutSec 5 -Method Get
        $resp.StatusCode | Should -Be 200
        ([string]::new($resp.Content) | ConvertFrom-KrYaml).message | Should -Be 'Hello, World!'
        $resp.Headers.'Content-Type' | Should -Match 'application/yaml'
    }
}
