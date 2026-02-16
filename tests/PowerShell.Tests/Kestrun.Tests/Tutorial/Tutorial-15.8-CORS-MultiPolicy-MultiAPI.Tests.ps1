param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}


Describe 'CORS Multi-Policy Multi-API' -Tag 'Tutorial', 'Slow' {

    BeforeAll {
        $script:instance = Start-ExampleScript -Name '15.8-Cors-Multipolicy.ps1'

        # Pick origins:
        # - Allowed UI origin should match what your sample configured for PublicRead/AdminWrite/default.
        #   In most of your samples, that's the same base URL as the server.
        # - Partner origin is whatever you configured for PartnerOnly.
        #
        # If your sample uses different values, just tweak these two lines.
        $script:allowedUiOrigin = $script:instance.Url.TrimEnd('/')   # e.g. http://localhost:5000
        $script:partnerOrigin = 'http://localhost:5000'            # matches your sample default PartnerOrigin
    }

    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }



    Context 'Simple requests (GET) with Origin header' {

        It 'GET /products (PublicRead) should emit Access-Control-Allow-Origin for allowed origin' {
            $res = Invoke-CorsRequest -Method GET -Path '/products' -Origin $allowedUiOrigin
            $res.StatusCode | Should -Be 200

            $res.Headers['Access-Control-Allow-Origin'] | Should -Be $allowedUiOrigin
        }

        It 'GET /partner/inventory (PartnerOnly) should NOT emit Access-Control-Allow-Origin for non-partner origin' {
            # Choose an origin that is *not* partnerOrigin
            $notPartnerOrigin = 'http://example.invalid'

            $res = Invoke-CorsRequest -Method GET -Path '/partner/inventory' -Origin $notPartnerOrigin
            $res.StatusCode | Should -Be 200

            $res.Headers['Access-Control-Allow-Origin'] | Should -BeNullOrEmpty
        }

        It 'GET /partner/inventory (PartnerOnly) should emit Access-Control-Allow-Origin for partner origin' {
            $res = Invoke-CorsRequest -Method GET -Path '/partner/inventory' -Origin $partnerOrigin
            $res.StatusCode | Should -Be 200

            $res.Headers['Access-Control-Allow-Origin'] | Should -Be $partnerOrigin
        }

        It 'GET /nocors (no CorsPolicy on operation) should follow DEFAULT policy (expect Allow-Origin for allowed UI origin)' {
            $res = Invoke-CorsRequest -Method GET -Path '/nocors' -Origin $allowedUiOrigin
            $res.StatusCode | Should -Be 200

            # If your runtime treats "no CorsPolicy" as "no CORS", change this expectation accordingly.
            $res.Headers['Access-Control-Allow-Origin'] | Should -Be $allowedUiOrigin
        }
    }

    Context 'Preflight requests (OPTIONS)' {

        It 'OPTIONS /orders preflight for POST should succeed for allowed UI origin (AdminWrite)' {
            $res = Invoke-CorsRequest `
                -Method OPTIONS `
                -Path '/orders' `
                -Origin $allowedUiOrigin `
                -PreflightMethod 'POST' `
                -PreflightHeaders @('content-type')

            # ASP.NET Core commonly returns 204 for preflight, but some setups return 200.
            $res.StatusCode | Should -BeIn @(200, 204)

            $res.Headers['Access-Control-Allow-Origin'] | Should -Be $allowedUiOrigin
            ($res.Headers['Access-Control-Allow-Methods'] -as [string]) | Should -Match '(?i)\bPOST\b'
        }

        It 'OPTIONS /partner/inventory preflight for GET should NOT allow non-partner origin (PartnerOnly)' {
            $notPartnerOrigin = 'http://example.invalid'

            $res = Invoke-CorsRequest `
                -Method OPTIONS `
                -Path '/partner/inventory' `
                -Origin $notPartnerOrigin `
                -PreflightMethod 'GET'

            # Implementations vary: could be 200/204 without allow headers, or 403.
            $res.StatusCode | Should -BeIn @(200, 204, 403)

            $res.Headers['Access-Control-Allow-Origin'] | Should -BeNullOrEmpty
        }
    }

    Context 'Write methods and CORS headers' {

        It 'POST /orders should emit Allow-Origin for allowed UI origin (AdminWrite)' {
            # Route contract requires application/json request body.
            $body = '1'

            $res = Invoke-CorsRequest -Method POST -Path '/orders' -Origin $allowedUiOrigin -Body $body -ContentType 'application/json'
            $res.StatusCode | Should -Be 201

            $res.Headers['Access-Control-Allow-Origin'] | Should -Be $allowedUiOrigin
        }

        It 'POST /orders should NOT emit Allow-Origin for disallowed origin (AdminWrite)' {
            $body = '1'
            $notAllowedOrigin = 'http://example.invalid'

            $res = Invoke-CorsRequest -Method POST -Path '/orders' -Origin $notAllowedOrigin -Body $body -ContentType 'application/json'
            $res.StatusCode | Should -Be 201

            $res.Headers['Access-Control-Allow-Origin'] | Should -BeNullOrEmpty
        }

        It 'POST /orders should return 415 for unsupported content type' {
            $res = Invoke-CorsRequest -Method POST -Path '/orders' -Origin $allowedUiOrigin -Body '1' -ContentType 'text/plain'
            $res.StatusCode | Should -Be 415

            # CORS policy is still applied for allowed origin even on contract failures.
            $res.Headers['Access-Control-Allow-Origin'] | Should -Be $allowedUiOrigin
        }

        It 'POST /orders should return 400 for malformed JSON body' {
            $res = Invoke-CorsRequest -Method POST -Path '/orders' -Origin $allowedUiOrigin -Body '{"productId":' -ContentType 'application/json'
            $res.StatusCode | Should -Be 400
            $res.Headers['Access-Control-Allow-Origin'] | Should -Be $allowedUiOrigin
        }
    }

    Context 'OpenAPI response schema coverage' {

        It 'OpenAPI should declare schemas for key responses' {
            $res = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
            $res.StatusCode | Should -Be 200

            $doc = $res.Content | ConvertFrom-Json

            $doc.paths.'/products'.get.responses.'200'.content.'application/json'.schema.'$ref' | Should -Be '#/components/schemas/ProductListDto'
            $doc.paths.'/products/{productId}'.get.responses.'200'.content.'application/json'.schema.'$ref' | Should -Be '#/components/schemas/ProductDto'
            $doc.paths.'/partner/inventory'.get.responses.'200'.content.'application/json'.schema.'$ref' | Should -Be '#/components/schemas/PartnerInventoryList'
            $doc.paths.'/orders'.post.responses.'201'.content.'application/json'.schema.'$ref' | Should -Be '#/components/schemas/Order'
            $doc.paths.'/orders'.post.responses.'400'.content.'application/json'.schema.'$ref' | Should -Be '#/components/schemas/ApiErrorResponse'
            $doc.paths.'/nocors'.get.responses.'200'.content.'application/json'.schema.'$ref' | Should -Be '#/components/schemas/NoCorsInfoResponse'
        }

        It 'OpenAPI output matches 15.8 fixture JSON' {
            Test-OpenApiDocumentMatchesExpected -Instance $script:instance
        }
    }
}

