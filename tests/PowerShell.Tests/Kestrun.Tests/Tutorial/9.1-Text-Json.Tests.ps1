param()
Describe 'Example 9.1-Text-Json' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '9.1-Text-Json.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Text and JSON routes return expected payloads' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        # /ping -> pong (text)
        Assert-RouteContent -Uri "$($script:instance.Url)/ping" -Contains 'pong'
        # /created -> 201 + body substring
        Assert-RouteContent -Uri "$($script:instance.Url)/created" -Method Post -ExpectStatus 201 -Contains 'resource created'
        # /time -> JSON with version field = 1
        $timeResp = Assert-RouteContent -Uri "$($script:instance.Url)/time" -ReturnResponse -Contains 'version' # quick smoke
        Assert-JsonFieldValue -Json $timeResp.Content -Field version -Expected 1
        # /config -> JSON with nested property
        $cfgResp = Assert-RouteContent -Uri "$($script:instance.Url)/config" -ReturnResponse -Contains '"nested"'
        ($cfgResp.Content | ConvertFrom-Json).nested.a | Should -Be 1
    }
}
