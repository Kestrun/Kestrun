param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 8.9 Authentication (OpenID Connect - Okta)' -Tag 'Tutorial', 'Auth' {
    It 'Skipped (requires interactive Okta login and secrets)' -Skip:$true { }
}
