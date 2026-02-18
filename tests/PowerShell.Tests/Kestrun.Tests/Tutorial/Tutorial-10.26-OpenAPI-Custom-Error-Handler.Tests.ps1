param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.26 OpenAPI Custom Error Handler' -Tag 'OpenApi', 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.26-OpenAPI-Custom-Error-Handler.ps1'
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /orders/1 returns 200 with order JSON' {
        $r = Invoke-WebRequest -Uri "$($script:instance.Url)/orders/1" -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $r.StatusCode | Should -Be 200
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'application/json'

        $body = $r.Content | ConvertFrom-Json
        $body.id | Should -Be 1
        $body.sku | Should -Be 'SKU-RED-001'
        $body.quantity | Should -Be 2
    }

    It 'GET /orders/13 returns custom 500 problem payload' {
        $r = Invoke-WebRequest -Uri "$($script:instance.Url)/orders/13" -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $r.StatusCode | Should -Be 500
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'application/problem\+json|application/json'

        $text = if ($r.Content -is [byte[]]) { Convert-BytesToStringWithGzipScan -Bytes $r.Content } else { [string]$r.Content }
        $json = $text | ConvertFrom-Json -AsHashtable

        [int]$json.status | Should -Be 500
        [string]$json.title | Should -Be 'Request failed'
        [string]$json.detail | Should -Match 'internal server error|Order service unavailable'
        [string]$json.path | Should -Be '/orders/13'
        [string]$json.timestamp | Should -Not -BeNullOrEmpty
    }

    It 'POST /orders with text/plain returns custom 415 payload' {
        $r = Invoke-WebRequest -Uri "$($script:instance.Url)/orders" -Method Post -ContentType 'text/plain' -Headers @{ Accept = 'application/json' } -Body 'bad' -SkipCertificateCheck -SkipHttpErrorCheck
        $r.StatusCode | Should -Be 415

        $text = if ($r.Content -is [byte[]]) { Convert-BytesToStringWithGzipScan -Bytes $r.Content } else { [string]$r.Content }
        $json = $text | ConvertFrom-Json -AsHashtable

        [int]$json.status | Should -Be 415
        [string]$json.title | Should -Be 'Request failed'
        [string]$json.detail | Should -Match 'content type|Content-Type|not allowed'
        [string]$json.path | Should -Be '/orders'
    }

    It 'OpenAPI contains ApiError schema and 415/500 error responses' {
        $r = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $r.StatusCode | Should -Be 200

        $doc = $r.Content | ConvertFrom-Json
        $doc.components.schemas.ApiError | Should -Not -BeNullOrEmpty

        $get500 = $doc.paths.'/orders/{orderId}'.get.responses.'500'.content
        $get500.'application/problem+json' | Should -Not -BeNullOrEmpty

        $post415 = $doc.paths.'/orders'.post.responses.'415'.content
        $post415.'application/problem+json' | Should -Not -BeNullOrEmpty
    }
}
