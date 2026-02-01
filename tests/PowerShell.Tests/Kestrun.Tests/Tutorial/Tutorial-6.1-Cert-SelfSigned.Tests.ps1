param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 6.1-Cert-SelfSigned' {
    It 'Skipped (certificate creation complexity)' -Skip:$true { }
}
