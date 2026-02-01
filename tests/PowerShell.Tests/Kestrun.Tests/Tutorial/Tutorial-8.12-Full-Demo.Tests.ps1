param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 8.12 Authentication (Full Demo)' -Tag 'Tutorial', 'Auth' {
    It 'Skipped (full auth demo requires external providers)' -Skip:$true { }
}
