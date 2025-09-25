param()
Describe 'Example 2.4-Route-Options' -Tag 'Tutorial' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '2.4-Route-Options.ps1' }
    AfterAll {if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Xml routes respond with 200' {
        $response = (Invoke-WebRequest -Uri "$($script:instance.Url)/xml/demoText" -UseBasicParsing -TimeoutSec 5 -Method Get)
        $response.StatusCode | Should -Be 200
        $response.Content | Should -Be '<Response><message>demoText</message></Response>'
    }
    It 'Yaml routes respond with expected content' {
        $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
        $session.Cookies.Add((New-Object System.Net.Cookie("message", "fromYaml", "/", $script:instance.host)))
        $response = (Invoke-WebRequest -Uri "$($script:instance.Url)/yaml" -UseBasicParsing -TimeoutSec 5 -Method Get -WebSession $session )
        $response.StatusCode | Should -Be 200
        ([string]::new($response.Content) | ConvertFrom-KrYaml).message | Should -Be 'fromYaml'
    }
    It 'Json routes respond with expected content' {
        $response = (Invoke-WebRequest -Uri "$($script:instance.Url)/json" -UseBasicParsing -TimeoutSec 5 -Method Get -Headers @{ message = 'fromHeader' })
        $response.StatusCode | Should -Be 200
        ($response.Content | ConvertFrom-Json).message | Should -Be "fromHeader"
    }
    It 'Txt route respond with expected content' {
        $response = (Invoke-WebRequest -Uri "$($script:instance.Url)/txt?message=fromQuery" -UseBasicParsing -TimeoutSec 5 -Method Get)
        $response.StatusCode | Should -Be 200
        $response.Content | Should -Be "message = fromQuery"
    }
}
