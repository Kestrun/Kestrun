param()
. "./tests/PowerShell.Tests/Kestrun.Tests/PesterHelpers.ps1"
Describe 'Example 8.11 Authentication (OpenID Connect - Duende Demo)' -Tag 'Tutorial','Auth' {
    It 'Skipped (interactive OIDC login not automated in CI)' -Skip:$true { }
}
