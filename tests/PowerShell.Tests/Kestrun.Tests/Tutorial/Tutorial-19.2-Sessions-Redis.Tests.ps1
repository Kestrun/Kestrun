param()

Describe 'Example 19.2-Sessions-Redis' -Tag 'Tutorial', 'Middleware', 'Sessions' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '19.2-Sessions-Redis.ps1'; }

    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    Context 'hello endpoint (non-session)' {
        It 'GET /hello returns greeting text' {
            $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/hello" -UseBasicParsing -TimeoutSec 15 -SkipCertificateCheck
            $resp.StatusCode | Should -Be 200
            $resp.Content | Should -Match 'Hello, Session World!'
        }
    }

    Context 'session counter' {
        It 'increments within the same session and resets for a new session' {
            # Session A
            $sessA = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
            $r1 = Invoke-WebRequest -Uri "$($script:instance.Url)/session/counter" -UseBasicParsing -TimeoutSec 15 -WebSession $sessA -SkipCertificateCheck
            $r1.StatusCode | Should -Be 200
            ($r1.Content | ConvertFrom-Json).counter | Should -Be 1

            $r2 = Invoke-WebRequest -Uri "$($script:instance.Url)/session/counter" -UseBasicParsing -TimeoutSec 15 -WebSession $sessA -SkipCertificateCheck
            $r2.StatusCode | Should -Be 200
            ($r2.Content | ConvertFrom-Json).counter | Should -Be 2

            # Session B (fresh cookies)
            $sessB = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
            $r3 = Invoke-WebRequest -Uri "$($script:instance.Url)/session/counter" -UseBasicParsing -TimeoutSec 15 -WebSession $sessB -SkipCertificateCheck
            $r3.StatusCode | Should -Be 200
            ($r3.Content | ConvertFrom-Json).counter | Should -Be 1
        }
    }

    Context 'login/whoami/logout flow' {
        It 'requires login for whoami, then returns user and clears on logout' {
            $sess = [Microsoft.PowerShell.Commands.WebRequestSession]::new()

            # whoami without login -> 401

            # Invoke-WebRequest throws on non-200 sometimes; probe status code explicitly via headers helper
            $probe = Invoke-WebRequest -Uri "$($script:instance.Url)/session/whoami" -UseBasicParsing -TimeoutSec 15 -WebSession $sess -SkipCertificateCheck -SkipHttpErrorCheck
            $probe.StatusCode | Should -Be 401


            # login
            $login = Invoke-WebRequest -Uri "$($script:instance.Url)/session/login?user=admin" -UseBasicParsing -TimeoutSec 15 -WebSession $sess -SkipCertificateCheck
            $login.StatusCode | Should -Be 200
            ($login.Content | ConvertFrom-Json).user | Should -Be 'admin'

            # whoami after login -> 200 with user
            $who2 = Invoke-WebRequest -Uri "$($script:instance.Url)/session/whoami" -UseBasicParsing -TimeoutSec 15 -WebSession $sess -SkipCertificateCheck
            $who2.StatusCode | Should -Be 200
            ($who2.Content | ConvertFrom-Json).user | Should -Be 'admin'

            # logout clears session
            $logout = Invoke-WebRequest -Uri "$($script:instance.Url)/session/logout" -UseBasicParsing -TimeoutSec 15 -WebSession $sess -SkipCertificateCheck
            $logout.StatusCode | Should -Be 200

            # whoami after logout -> 401
            $probe2 = Invoke-WebRequest -Uri "$($script:instance.Url)/session/whoami" -UseBasicParsing -TimeoutSec 15 -WebSession $sess -SkipCertificateCheck -SkipHttpErrorCheck
            $probe2.StatusCode | Should -Be 401
        }
    }

    Context 'generic set/get' {
        It 'stores and retrieves arbitrary keys within session; not visible across sessions' {
            # Session C
            $sessC = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
            $set = Invoke-WebRequest -Uri "$($script:instance.Url)/session/set?key=color&value=purple" -UseBasicParsing -TimeoutSec 15 -WebSession $sessC -SkipCertificateCheck
            $set.StatusCode | Should -Be 200
            $setObj = $set.Content | ConvertFrom-Json
            $setObj.key | Should -Be 'color'
            $setObj.value | Should -Be 'purple'

            $get = Invoke-WebRequest -Uri "$($script:instance.Url)/session/get?key=color" -UseBasicParsing -TimeoutSec 15 -WebSession $sessC -SkipCertificateCheck
            $get.StatusCode | Should -Be 200
            ($get.Content | ConvertFrom-Json).value | Should -Be 'purple'

            # New session should not see previous key
            $sessD = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
            $probe = Invoke-WebRequest -Uri "$($script:instance.Url)/session/get?key=color" -UseBasicParsing -TimeoutSec 15 -WebSession $sessD -SkipCertificateCheck -SkipHttpErrorCheck
            $probe.StatusCode | Should -Be 404
        }
    }
}
