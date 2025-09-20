param()
Describe 'Example 3.3-Static-OverrideRoutes' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '3.3-Static-OverrideRoutes.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Override routes respond with 200' { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; Test-ExampleRouteSet -Instance $script:instance }
}
