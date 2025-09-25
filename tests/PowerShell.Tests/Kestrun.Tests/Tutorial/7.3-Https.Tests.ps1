param()
Describe 'Example 7.3-Https' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '7.3-Https.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }


    It 'GET /unsecured returns hello on primary listener (HTTP)' {

        $uri = "$($script:instance.Url)/unsecured"
        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Be 'Unsecured hello'
    }

    It 'GET /secure returns hello on secondary listener (HTTPS)' {
        $uri = ($script:instance.Https ? 'https' : 'http') + "://$($script:instance.Host):$($script:instance.Port+443)/secure"

        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop -SkipCertificateCheck
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Be 'Secure hello'
    }
}
