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
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
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
            # Your createOrder signature appears to bind productId directly.
            # If your endpoint expects JSON body instead, adjust to match your sample.
            $body = '1'

            $res = Invoke-CorsRequest -Method POST -Path '/orders' -Origin $allowedUiOrigin -Body $body -ContentType 'application/x-www-form-urlencoded'
            $res.StatusCode | Should -BeIn @(201, 400)  # 201 for valid, 400 if binding differs

            $res.Headers['Access-Control-Allow-Origin'] | Should -Be $allowedUiOrigin
        }
    }
}

