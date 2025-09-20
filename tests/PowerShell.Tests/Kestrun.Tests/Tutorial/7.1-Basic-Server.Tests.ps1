param()
Describe 'Example 7.1-Basic-Server' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '7.1-Basic-Server.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Basic server routes respond with 200' { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; Test-ExampleRouteSet -Instance $script:instance }
}
