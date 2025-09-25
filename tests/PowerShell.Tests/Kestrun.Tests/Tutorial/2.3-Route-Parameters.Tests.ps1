param()
Describe 'Example 2.3-Route-Parameters' -Tag 'Tutorial' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '2.3-Route-Parameters.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Parameter routes return expected content (path/query/body/header/cookie)' {
        # Path parameter (GET)
        Assert-RouteContent -Uri "$($script:instance.Url)/input/demoPath" -Contains "The Path Parameter 'value' was: demoPath"
        # Query string (PATCH)
        Assert-RouteContent -Uri "$($script:instance.Url)/input?value=demoQuery" -Method Patch -Contains "The Query String 'value' was: demoQuery"
        # Body (POST)
        $body = @{ value = 'demoBody' } | ConvertTo-Json
        Assert-RouteContent -Uri "$($script:instance.Url)/input" -Method Post -Body $body -ContentType 'application/json' -Contains "The Body Parameter 'value' was: demoBody"
        # Header (PUT)
        Assert-RouteContent -Uri "$($script:instance.Url)/input" -Method Put -Headers @{ value = 'demoHeader' } -Contains "The Header Parameter 'value' was: demoHeader"
        # Cookie (DELETE)
        Assert-RouteContent -Uri "$($script:instance.Url)/input" -Method Delete -Headers @{ Cookie = 'value=demoCookie' } -Contains "The Cookie Parameter 'value' was: demoCookie"
    }
}
