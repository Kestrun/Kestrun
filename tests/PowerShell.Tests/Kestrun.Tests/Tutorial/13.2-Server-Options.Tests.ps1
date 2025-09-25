param()
Describe 'Example 13.2-Server-Options' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '13.2-Server-Options.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Server options routes respond with 200' { Test-ExampleRouteSet -Instance $script:instance }
}
