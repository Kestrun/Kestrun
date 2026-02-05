param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 8.13 Auth + Status Codes' -Tag 'Tutorial', 'Auth', 'StatusCodes', 'OpenApi', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '8.13-Auth-StatusCodes.ps1'

        $script:adminAuth = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('admin:password'))
        $script:userAuth = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('user:password'))
        $script:badAuth = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('admin:wrong'))
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /public returns 200' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/public" -SkipCertificateCheck -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'OK'
    }

    It 'GET /secure/hello returns 401 without credentials' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/hello" -SkipCertificateCheck -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 401
    }

    It 'GET /secure/hello returns 401 with bad credentials' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/hello" -Headers @{ Authorization = $script:badAuth } -SkipCertificateCheck -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 401
    }

    It 'GET /secure/hello returns 200 with valid credentials' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/hello" -Headers @{ Authorization = $script:adminAuth } -SkipCertificateCheck -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 200

        $json = $resp.Content | ConvertFrom-Json
        $json.message | Should -Be 'hello'
        $json.user | Should -Be 'admin'
    }

    It 'DELETE /secure/resource/{id} returns 403 when missing CanDelete policy' {
        $resp = Invoke-WebRequest -Method Delete -Uri "$($script:instance.Url)/secure/resource/1" -Headers @{ Authorization = $script:adminAuth } -SkipCertificateCheck -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 403

        $resp2 = Invoke-WebRequest -Method Delete -Uri "$($script:instance.Url)/secure/resource/1" -Headers @{ Authorization = $script:userAuth } -SkipCertificateCheck -SkipHttpErrorCheck
        $resp2.StatusCode | Should -Be 403
    }

    It 'POST /json/echo returns 415 when Content-Type is not application/json' {
        $resp = Invoke-WebRequest -Method Post -Uri "$($script:instance.Url)/json/echo" -Body 'hi' -ContentType 'text/plain' -SkipCertificateCheck -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 415
    }

    It 'POST /json/echo returns 400 when JSON is invalid' {
        $resp = Invoke-WebRequest -Method Post -Uri "$($script:instance.Url)/json/echo" -Body '{"a":' -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 400
    }

    It 'POST /json/echo returns 422 when JSON is valid but fails validation' {
        $resp = Invoke-WebRequest -Method Post -Uri "$($script:instance.Url)/json/echo" -Body '{"name":"","quantity":0}' -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 422
    }

    It 'POST /json/echo returns 201 when JSON is valid' {
        $resp = Invoke-WebRequest -Method Post -Uri "$($script:instance.Url)/json/echo" -Body '{"name":"widget","quantity":2,"priority":"normal"}' -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 201

        $json = $resp.Content | ConvertFrom-Json
        $json.received.name | Should -Be 'widget'
        $json.received.quantity | Should -Be 2
        $json.received.priority | Should -Be 'normal'
    }

    It 'POST /only-get returns 405 (method not allowed)' {
        $resp = Invoke-WebRequest -Method Post -Uri "$($script:instance.Url)/only-get" -Body '' -ContentType 'text/plain' -SkipCertificateCheck -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 405
    }

    It 'GET /does-not-exist returns 404' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/does-not-exist" -SkipCertificateCheck -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 404
    }

    It 'OpenAPI lists the endpoints' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.2/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 200

        $doc = $resp.Content | ConvertFrom-Json
        $doc.paths.'/public'.get | Should -Not -BeNullOrEmpty
        $doc.paths.'/secure/hello'.get | Should -Not -BeNullOrEmpty
        $doc.paths.'/secure/resource/{id}'.delete | Should -Not -BeNullOrEmpty
        $doc.paths.'/json/echo'.post | Should -Not -BeNullOrEmpty
        $doc.paths.'/only-get'.get | Should -Not -BeNullOrEmpty
    }
}
