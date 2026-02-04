param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 14.2-Full-Demo' {
    It 'Skipped (full demo is large / integration scenario)' -Skip:$true { }
}
