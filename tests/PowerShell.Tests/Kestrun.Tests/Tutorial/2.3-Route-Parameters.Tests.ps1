param()
Describe 'Example 2.3-Route-Parameters' -Tag 'Tutorial' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '2.3-Route-Parameters.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Parameter routes return expected content (path/query/body/header/cookie)' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $port = $script:instance.Port
        # Path parameter (GET)
        Assert-RouteContent -Uri "http://127.0.0.1:$port/input/demoPath" -Contains "The Path Parameter 'value' was: demoPath"
        # Query string (PATCH)
        Assert-RouteContent -Uri "http://127.0.0.1:$port/input?value=demoQuery" -Method Patch -Contains "The Query String 'value' was: demoQuery"
        # Body (POST)
        $body = @{ value = 'demoBody' } | ConvertTo-Json
        Assert-RouteContent -Uri "http://127.0.0.1:$port/input" -Method Post -Body $body -ContentType 'application/json' -Contains "The Body Parameter 'value' was: demoBody"
        # Header (PUT)
        Assert-RouteContent -Uri "http://127.0.0.1:$port/input" -Method Put -Headers @{ value = 'demoHeader' } -Contains "The Header Parameter 'value' was: demoHeader"
        # Cookie (DELETE)
        Assert-RouteContent -Uri "http://127.0.0.1:$port/input" -Method Delete -Headers @{ Cookie = 'value=demoCookie' } -Contains "The Cookie Parameter 'value' was: demoCookie"
    }
}
