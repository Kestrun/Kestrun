param()
. "./tests/PowerShell.Tests/Kestrun.Tests/PesterHelpers.ps1"

Describe 'Example 7.5-Unix-Sockets' {
    It 'Skipped (Unix sockets not available on Windows CI)' -Skip:$true { }
}
