param()
Describe 'Example 10.8 OpenAPI Document Info' -Tag 'OpenApi', 'Tutorial', 'Slow' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '10.8-OpenAPI-Document-Info.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Check OpenAPI Info' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $info = $json.info
        $info.title | Should -Be 'Document Info API'
        $info.version | Should -Be '1.0.0'
        $info.description | Should -Be 'Shows how to populate document metadata.'

        $info.contact.name | Should -Be 'API Support'
        $info.contact.email | Should -Be 'support@example.com'

        $info.license.name | Should -Be 'MIT'
        $info.license.url | Should -Be 'https://opensource.org/licenses/MIT'
    }
}

