param()
Describe 'Example 15.7 OpenAPI Tags' -Tag 'Tutorial', 'Slow' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '15.7-OpenAPI-Tags.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Check OpenAPI Tags' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $tags = $json.tags
        $tags | Should -Not -BeNullOrEmpty
        $userTag = $tags | Where-Object { $_.name -eq 'orders' }
        $userTag | Should -Not -BeNullOrEmpty
        $userTag.description | Should -Be 'Operations for listing orders'

        $productTag = $tags | Where-Object { $_.name -eq 'health' }
        $productTag | Should -Not -BeNullOrEmpty
    }
}
