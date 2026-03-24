param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 8.x Authentication (Claims-Policies)' -Tag 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '8.7-Claims-Policies.ps1'
        $creds = 'admin:password'
        $script:basic = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($creds))
        $script:badBasic = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes('admin:wrong-password'))
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /policy/read returns 401 without credentials' {
        $result = Invoke-TestRequest -Uri "$($script:instance.Url)/policy/read" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 401
    }

    It 'GET /policy/read returns 401 with invalid credentials' {
        $result = Invoke-TestRequest -Uri "$($script:instance.Url)/policy/read" -SkipCertificateCheck -SkipHttpErrorCheck -Headers @{ Authorization = $script:badBasic }
        $result.StatusCode | Should -Be 401
    }

    It 'GET /policy/read returns 200 with CanRead policy claim' {
        $result = Invoke-TestRequest -Uri "$($script:instance.Url)/policy/read" -SkipCertificateCheck -SkipHttpErrorCheck -Headers @{ Authorization = $script:basic }
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Be 'Read OK'
        $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
    }

    It 'GET /policy/write returns 200 with CanWrite policy claim' {
        $result = Invoke-TestRequest -Uri "$($script:instance.Url)/policy/write" -SkipCertificateCheck -SkipHttpErrorCheck -Headers @{ Authorization = $script:basic }
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Be 'Write OK'
        $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
    }

    It 'GET /policy/delete returns 403 when CanDelete claim is missing' {
        $result = Invoke-TestRequest -Uri "$($script:instance.Url)/policy/delete" -SkipCertificateCheck -SkipHttpErrorCheck -Headers @{ Authorization = $script:basic }
        $result.StatusCode | Should -Be 403
    }
}
