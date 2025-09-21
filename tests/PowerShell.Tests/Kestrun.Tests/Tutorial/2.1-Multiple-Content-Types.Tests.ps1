param()

Describe 'Example 2.1-Multiple-Content-Types' -Tag 'Tutorial' {
    BeforeAll {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $script:instance = Start-ExampleScript -Name '2.1-Multiple-Content-Types.ps1'
    }
    AfterAll {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }
    It 'Text/Json/Xml/Yaml endpoints return expected formats' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $p = $script:instance.Port
        Invoke-ExampleRequest -Uri "$($script:instance.Url)/hello" -ExpectStatus 200 -BodyContains 'Hello, World!' -ContentTypeContains 'text/plain'
        $jsonResp = Invoke-ExampleRequest -Uri "$($script:instance.Url)/hello-json" -ExpectStatus 200 -ContentTypeContains 'application/json' -ReturnRaw
        Assert-JsonFieldValue -Json $jsonResp.Content -Field 'message' -Expected 'Hello, World!'
        $xmlResp = Invoke-ExampleRequest -Uri "$($script:instance.Url)/hello-xml" -ExpectStatus 200 -ContentTypeContains 'application/xml' -ReturnRaw
        ($xmlResp.Content -match '<message>Hello, World!</message>') | Should -BeTrue
        $yamlResp = Invoke-ExampleRequest -Uri "$($script:instance.Url)/hello-yaml" -ExpectStatus 200 -ReturnRaw
        Assert-YamlContainsKeyValue -Yaml $yamlResp.Content -Key 'message' -Expected 'Hello, World!'
    }
}
