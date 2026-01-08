param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'OpenAPI Petstore Example' -Tag 'OpenApi', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.14-OpenAPI-PetStore.ps1'
    }

    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'OpenAPI JSON equals expected petstore-api.json' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200

        $actualNormalized = Get-NormalizedJson $result.Content
        $expectedPath = Join-Path -Path (Get-TutorialExamplesDirectory) -ChildPath 'Assets' -AdditionalChildPath 'OpenAPI', 'petstore-api.json'

        $expectedContent = Get-Content -Path $expectedPath -Raw
        $expectedNormalized = Get-NormalizedJson $expectedContent

        $actualNormalized | Should -Be $expectedNormalized
    }
}

