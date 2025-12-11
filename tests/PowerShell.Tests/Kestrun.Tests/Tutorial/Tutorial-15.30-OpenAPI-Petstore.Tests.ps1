param()
Describe 'OpenAPI Petstore Example' -Tag 'OpenApi', 'Slow' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '15.30-PetStore.ps1'
        function Get-NormalizedJson {
            param([string]$Json)
            $obj = $Json | ConvertFrom-Json -Depth 100
            $obj | ConvertTo-Json -Depth 100 -Compress
        }
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\\..\\..\\..')).Path
    }

    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'OpenAPI JSON equals expected petstore-api.json' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200

        $actualNormalized = Get-NormalizedJson $result.Content
        $expectedPath = Join-Path $script:repoRoot 'docs\\_includes\\examples\\pwsh\\Assets\\OpenAPI\\petstore-api.json'
        $expectedContent = Get-Content -Path $expectedPath -Raw
        $expectedNormalized = Get-NormalizedJson $expectedContent

        $actualNormalized | Should -Be $expectedNormalized
    }
}
