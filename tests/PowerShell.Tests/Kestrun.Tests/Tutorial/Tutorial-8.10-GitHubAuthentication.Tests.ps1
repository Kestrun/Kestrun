param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 8.10 Authentication (GitHub OAuth)' -Tag 'Tutorial', 'Auth' {
    It 'Skipped (requires GitHub app credentials and interactive login)' -Skip:$true { }
}
