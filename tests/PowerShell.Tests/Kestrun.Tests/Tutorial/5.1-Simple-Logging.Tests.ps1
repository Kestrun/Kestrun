param()
Describe 'Example 5.1-Simple-Logging' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '5.1-Simple-Logging.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Simple logging routes return Hello World text' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        # Two routes /hello-powershell and /hello-csharp should both emit Hello, World!
        $expect = @{ '/hello-powershell' = 'Hello, World!'; '/hello-csharp' = 'Hello, World!' }
        Test-ExampleRouteSet -Instance $script:instance -ContentExpectations $expect
    }
}
