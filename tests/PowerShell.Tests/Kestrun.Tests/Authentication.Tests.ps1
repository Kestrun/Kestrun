param()
BeforeAll { . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1');
    $scriptPath = (Join-Path -Path 'examples' -ChildPath 'PowerShell' -AdditionalChildPath 'Authentication', 'Authentication.ps1')
    $script:instance = Start-ExampleScript -Name $scriptPath -FromRootDirectory }
AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

Describe 'Kestrun Authentication' {

    Describe 'Basic Authentication' {
        BeforeAll {
            $creds = 'admin:password'
            $script:basic = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($creds))
        }

        It 'ps/hello in PowerShell' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/ps/hello" -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be 'Welcome, admin! You are authenticated by PowerShell Code.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'ps/hello in C#' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/cs/hello" -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be 'Welcome, admin! You are authenticated by C# Code.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'ps/policy (CanRead)' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/ps/policy" -Method GET -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by PowerShell Code because you have the 'can_read' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'ps/policy (CanDelete)' {
            { Invoke-WebRequest -Uri "$($script:instance.Url)/secure/ps/policy" -Method DELETE `
                    -SkipCertificateCheck -Headers @{Authorization = $script:basic } -ErrorAction SilentlyContinue } |
                Should -Throw -ExpectedMessage '*403*'
        }

        It 'ps/policy (CanCreate)' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/ps/policy" -Method POST -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by PowerShell Code because you have the 'can_create' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'ps/policy (CanUpdate)' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/ps/policy" -Method PUT -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by PowerShell Code because you have the 'can_write' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'ps/policy (CanPatch)' {
            { Invoke-WebRequest -Uri "$($script:instance.Url)/secure/ps/policy" -Method PATCH `
                    -SkipCertificateCheck -Headers @{Authorization = $script:basic } -ErrorAction SilentlyContinue } |
                Should -Throw -ExpectedMessage '*405*'
        }


        It 'vb/hello in VB.Net' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/vb/hello" -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be 'Welcome, admin! You are authenticated by VB.Net Code.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'vb/policy (CanRead)' {
            { Invoke-WebRequest -Uri "$($script:instance.Url)/secure/vb/policy" -Method GET `
                    -SkipCertificateCheck -Headers @{Authorization = $script:basic } -ErrorAction SilentlyContinue } |
                Should -Throw -ExpectedMessage '*403*'
        }

        It 'vb/policy (CanDelete)' {
            { Invoke-WebRequest -Uri "$($script:instance.Url)/secure/vb/policy" -Method DELETE `
                    -SkipCertificateCheck -Headers @{Authorization = $script:basic } -ErrorAction SilentlyContinue } |
                Should -Throw -ExpectedMessage '*403*'
        }

        It 'vb/policy (CanCreate)' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/vb/policy" -Method POST -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by VB.Net Code because you have the 'can_create' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'vb/policy (CanUpdate)' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/vb/policy" -Method PUT -SkipCertificateCheck -Headers @{Authorization = $script:basic }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by VB.Net Code because you have the 'can_write' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'vb/policy (CanPatch)' {
            { Invoke-WebRequest -Uri "$($script:instance.Url)/secure/vb/policy" -Method PATCH `
                    -SkipCertificateCheck -Headers @{Authorization = $script:basic } -ErrorAction SilentlyContinue } |
                Should -Throw -ExpectedMessage '*405*'
        }
    }

    Describe 'Key Authentication' {

        It 'key authentication Hello Simple mode' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/key/simple/hello" -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' }
            $result.Content | Should -Be 'Welcome, ApiKeyClient! You are authenticated using simple key matching.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'key authentication Hello in powershell' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/key/ps/hello" -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' }
            $result.Content | Should -Be 'Welcome, ApiKeyClient! You are authenticated by Key Matching PowerShell Code.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }
        It 'key authentication Hello in CSharp' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/key/cs/hello" -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' }
            $result.Content | Should -Be 'Welcome, ApiKeyClient! You are authenticated by Key Matching C# Code.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'key authentication Hello in VB.Net' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/key/vb/hello" -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' }
            $result.Content | Should -Be 'Welcome, ApiKeyClient! You are authenticated by Key Matching VB.Net Code.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }


        It 'key/policy (CanRead)' {
            { Invoke-WebRequest -Uri "$($script:instance.Url)/secure/key/ps/policy" -Method GET `
                    -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' } -ErrorAction SilentlyContinue } |
                Should -Throw -ExpectedMessage '*403*'
        }

        It 'key/policy (CanDelete)' {
            { Invoke-WebRequest -Uri "$($script:instance.Url)/secure/key/ps/policy" -Method DELETE -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' } `
                    -ErrorAction SilentlyContinue } | Should -Throw -ExpectedMessage '*403*'
        }

        It 'key/policy (CanCreate)' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/key/ps/policy" -Method Post -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, ApiKeyClient! You are authenticated by Key Matching PowerShell Code because you have the 'can_create' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'key/policy (CanUpdate)' {
            { Invoke-WebRequest -Uri "$($script:instance.Url)/secure/key/ps/policy" -Method Put `
                    -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' } -ErrorAction SilentlyContinue } |
                Should -Throw -ExpectedMessage '*403*'
        }

        It 'key/policy (CanPatch)' {
            { Invoke-WebRequest -Uri "$($script:instance.Url)/secure/key/ps/policy" -Method PATCH -SkipCertificateCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' } -ErrorAction SilentlyContinue } |
                Should -Throw -ExpectedMessage '*405*'
        }
    }

    Describe 'JWT Authentication' {

        BeforeAll {
            $creds = 'admin:password'
            $basic = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($creds))
            $script:token = (Invoke-RestMethod -Uri "$($script:instance.Url)/token/new" -SkipCertificateCheck -Headers @{ Authorization = $script:basic }).access_token
        }

        It 'New Token' {
            $Script:token | Should -Not -BeNullOrEmpty
        }

        It 'Hello JWT' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/jwt/hello" -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be 'Welcome, admin! You are authenticated by JWT Bearer Token.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }
        It 'jwt/policy (CanRead)' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/jwt/policy" -Method Get -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by Native JWT checker because you have the 'can_read' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }

        It 'jwt/policy (CanDelete)' {
            { Invoke-WebRequest -Uri "$($script:instance.Url)/secure/jwt/policy" -Method Delete -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" } } | Should -Throw -ExpectedMessage '*403*'
        }

        It 'jwt/policy (CanCreate)' {
            { Invoke-WebRequest -Uri "$($script:instance.Url)/secure/jwt/policy" -Method Post -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" } } | Should -Throw -ExpectedMessage '*403*'
        }

        It 'jwt/policy (CanUpdate)' {
            { Invoke-WebRequest -Uri "$($script:instance.Url)/secure/jwt/policy" -Method Put -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" } } | Should -Throw -ExpectedMessage '*403*'
        }

        It 'jwt/policy (CanPatch)' {
            { Invoke-WebRequest -Uri "$($script:instance.Url)/secure/jwt/policy" -Method PATCH `
                    -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" } -ErrorAction SilentlyContinue } |
                Should -Throw -ExpectedMessage '*405*'
        }

        It 'Renew Token' {
            $token2 = (Invoke-RestMethod -Uri "$($script:instance.Url)/token/renew" -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token" }).access_token
            $token2 | Should -Not -BeNullOrEmpty
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/jwt/hello" -SkipCertificateCheck -Headers @{ Authorization = "Bearer $token2" }
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be 'Welcome, admin! You are authenticated by JWT Bearer Token.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }
    }
    Describe 'Cookies Authentication' {
        BeforeAll {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/cookies/login" -SkipCertificateCheck -Method Post -Body @{ username = 'admin'; password = 'secret' } -SessionVariable lauthSession
            $script:authSession = $lauthSession
            $result.StatusCode | Should -Be 200
        }
        AfterAll {
            $script:authSession = $null
        }
        It 'Login successful' {
            $script:authSession | Should -Not -BeNullOrEmpty
            $script:authSession.Cookies.Count | Should -Be 1
        }
        It 'Can access secure cookies endpoint' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/cookies/hello" -SkipCertificateCheck -WebSession $authSession
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be 'Welcome, admin! You are authenticated by Cookies Authentication.'
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }
        It 'Can access secure cookies policy (GET)' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/cookies/policy" -Method GET -SkipCertificateCheck -WebSession $authSession
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by Cookies checker because you have the 'can_read' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }
        It 'Can access secure cookies policy (DELETE)' {
            { Invoke-WebRequest -Uri "$($script:instance.Url)/secure/cookies/policy" -Method DELETE -SkipCertificateCheck -WebSession $authSession -ErrorAction SilentlyContinue } | Should -Throw -ExpectedMessage '*404*'
        }
        It 'Can access secure cookies policy (POST)' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/cookies/policy" -Method POST -SkipCertificateCheck -WebSession $authSession
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by Cookies checker because you have the 'can_create' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }
        It 'Can access secure cookies policy (PUT)' {
            $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/cookies/policy" -Method PUT -SkipCertificateCheck -WebSession $authSession
            $result.StatusCode | Should -Be 200
            $result.Content | Should -Be "Welcome, admin! You are authenticated by Cookies checker because you have the 'can_write' permission."
            $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        }
        It 'Can access secure cookies policy (PATCH)' {
            { Invoke-WebRequest -Uri "$($script:instance.Url)/secure/cookies/policy" -Method PATCH -SkipCertificateCheck -WebSession $authSession -ErrorAction SilentlyContinue } | Should -Throw -ExpectedMessage '*405*'
        }
    }
}
