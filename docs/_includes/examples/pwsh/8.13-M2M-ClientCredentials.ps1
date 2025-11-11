<#
    Sample: Machine-to-Machine (M2M) Authentication using Client Credentials
    Purpose: Demonstrate OAuth 2.0 client credentials grant with Duende demo server.

    What is M2M/Client Credentials?
      - Server-to-server authentication (no user involved)
      - Application authenticates as itself using client ID and secret
      - Typically used for:
        * Backend services calling APIs
        * Scheduled jobs accessing protected resources
        * Microservices communication

    Duende Demo M2M Clients:
      1. m2m (standard with client secret)
         - Client ID: m2m
         - Client Secret: secret
         - Grant Type: client_credentials
         - Auth Method: client_secret_post
         - Access Token Lifetime: 1 hour
         - Allowed Scopes: api

      2. m2m.short (short token lifetime)
         - Client ID: m2m.short
         - Client Secret: secret
         - Grant Type: client_credentials
         - Auth Method: client_secret_post
         - Access Token Lifetime: 75 seconds (1m 15s)
         - Allowed Scopes: api
         - Useful for testing token expiration/refresh scenarios

      3. m2m.dpop (DPoP enabled) ⚠️ NOT IMPLEMENTED
         - Client ID: m2m.dpop
         - Client Secret: secret
         - Grant Type: client_credentials
         - Auth Method: client_secret_post + DPoP proof
         - Access Token Lifetime: 1 hour
         - Allowed Scopes: api
         - Requires DPoP (Demonstrating Proof-of-Possession)
         - DPoP binds tokens to ephemeral client key (per-request proofs)
         - Prevents token theft and replay attacks
         - Too complex for this demo (requires ephemeral key pairs + per-request JWT proofs)

      4. m2m.jwt (JWT Bearer authentication) ✅ FULLY WORKING
         - Client ID: m2m.jwt
         - Auth Method: private_key_jwt (client_assertion)
         - Grant Type: client_credentials
         - Access Token Lifetime: 1 hour
         - Allowed Scopes: api
         - More secure than client secrets (uses asymmetric keys)
         - Client proves identity by signing JWT with private key (RS256)
         - Server validates with public key from JWKS

      5. m2m.short.jwt (JWT Bearer + short tokens) ✅ FULLY WORKING
         - Client ID: m2m.short.jwt
         - Auth Method: private_key_jwt (client_assertion)
         - Grant Type: client_credentials
         - Access Token Lifetime: 75 seconds (1m 15s)
         - Allowed Scopes: api
         - Combines JWT authentication with short-lived tokens
         - Uses RSA signing with Duende's demo private key

    Usage Examples:
      # Standard M2M with client secret (1-hour tokens)
      .\8.13-M2M-ClientCredentials.ps1
      .\8.13-M2M-ClientCredentials.ps1 -ClientId 'm2m'

      # Short-lived tokens (75 seconds) - test token expiration
      .\8.13-M2M-ClientCredentials.ps1 -ClientId 'm2m.short'

      # DPoP enabled - enhanced security with proof-of-possession
      .\8.13-M2M-ClientCredentials.ps1 -ClientId 'm2m.dpop'

      # JWT Bearer authentication - more secure than secrets
      .\8.13-M2M-ClientCredentials.ps1 -ClientId 'm2m.jwt'

      # JWT Bearer + short tokens (75 seconds)
      .\8.13-M2M-ClientCredentials.ps1 -ClientId 'm2m.short.jwt'

    Key Differences from Interactive OIDC:
      - No user login or browser redirects
      - No OpenID Connect (just OAuth 2.0)
      - Token represents the application, not a user
      - Direct token endpoint call from server code
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback,
    [string]$Authority = 'https://demo.duendesoftware.com',
    [ValidateSet('m2m', 'm2m.short', 'm2m.dpop', 'm2m.jwt', 'm2m.short.jwt')]
    [string]$ClientId = 'm2m',
    [string]$ClientSecret = 'secret',
    [string[]]$Scopes = @('api')
)

# Explain the client differences
$clientInfo = switch ($ClientId) {
    'm2m' {
        @{
            Description = 'Standard M2M with client secret authentication'
            TokenLifetime = '1 hour'
            UseDPoP = $false
            AuthMethod = 'Client Secret'
        }
    }
    'm2m.short' {
        @{
            Description = 'M2M with SHORT token lifetime (75 seconds) - useful for testing expiration'
            TokenLifetime = '75 seconds (1m 15s)'
            UseDPoP = $false
            AuthMethod = 'Client Secret'
        }
    }
    'm2m.dpop' {
        @{
            Description = 'M2M with DPoP (Demonstrating Proof-of-Possession) ⚠️ NOT IMPLEMENTED'
            TokenLifetime = '1 hour'
            UseDPoP = $true
            AuthMethod = 'Client Secret + DPoP (not implemented)'
        }
    }
    'm2m.jwt' {
        @{
            Description = 'M2M with JWT Bearer authentication (RS256) - more secure than secrets ✅'
            TokenLifetime = '1 hour'
            UseDPoP = $false
            AuthMethod = 'Private Key JWT (RS256 signed client_assertion)'
        }
    }
    'm2m.short.jwt' {
        @{
            Description = 'M2M with JWT Bearer (RS256) + SHORT token lifetime (75 seconds) ✅'
            TokenLifetime = '75 seconds (1m 15s)'
            UseDPoP = $false
            AuthMethod = 'Private Key JWT (RS256 signed client_assertion)'
        }
    }
}

Initialize-KrRoot -Path $PSScriptRoot

# 1) Logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2) Server
New-KrServer -Name 'M2M Client Credentials Demo'

# 3) HTTPS endpoint
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -SelfSignedCert

# 4) Finalize pipeline (no authentication middleware needed for M2M demo)
Enable-KrConfiguration

# 5) Landing page
Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    Write-KrHtmlResponse -Template @'
<!doctype html>
<html>
<head>
    <title>M2M Client Credentials Demo</title>
    <style>
        body { font-family: Arial, sans-serif; max-width: 800px; margin: 50px auto; padding: 20px; }
        h1 { color: #333; }
        .section { margin: 20px 0; padding: 15px; background: #f5f5f5; border-radius: 5px; }
        .endpoint { margin: 10px 0; }
        code { background: #e0e0e0; padding: 2px 5px; border-radius: 3px; }
        pre { background: #272822; color: #f8f8f2; padding: 15px; border-radius: 5px; overflow-x: auto; }
    </style>
</head>
<body>
    <h1>🔐 Machine-to-Machine (M2M) Authentication Demo</h1>

    <div class="section">
        <h2>What is M2M?</h2>
        <p>Machine-to-Machine authentication allows services to authenticate without user interaction.</p>
        <p>Uses OAuth 2.0 <strong>Client Credentials Grant</strong> - the application authenticates as itself.</p>
    </div>

        <div class="section">
        <h2>Demo Configuration</h2>
        <p><strong>Authority:</strong> {{authority}}</p>
        <p><strong>Client ID:</strong> {{clientId}}</p>
        <div class="info-box">
            <strong>{{clientDescription}}</strong><br>
            Authentication Method: {{authMethod}}<br>
            Token Lifetime: {{tokenLifetime}}<br>
            DPoP Required: {{useDPoP}}
        </div>
        <p><strong>Client Secret:</strong> {{secret}}</p>
        <p><strong>Scopes:</strong> {{scopes}}</p>
    </div>

    <div class="section">
        <h2>Try It Out</h2>
        <div class="endpoint">
            <strong><a href="/token">GET /token</a></strong> - Request an access token using client credentials
        </div>
        <div class="endpoint">
            <strong><a href="/token/decode">GET /token/decode</a></strong> - Get a token and decode its claims
        </div>
        <div class="endpoint">
            <strong><a href="/api-call">GET /api-call</a></strong> - Simulate calling a protected API with the token
        </div>
    </div>

    <div class="section">
        <h2>How It Works</h2>
        <pre>
1. POST to token endpoint with client credentials
2. Receive access_token in response
3. Use token in Authorization header for API calls
4. Token represents the application (not a user)</pre>
    </div>
</body>
</html>
'@ -Variables @{
        authority = $Authority
        clientId = $ClientId
        clientDescription = $clientInfo.Description
        authMethod = $clientInfo.AuthMethod
        tokenLifetime = $clientInfo.TokenLifetime
        useDPoP = if ($clientInfo.UseDPoP) { 'Yes (prevents token theft/replay)' } else { 'No' }
        secret = if ($ClientSecret.Length -gt 10) { $ClientSecret.Substring(0, 3) + '***' } else { '***' }
        scopes = ($Scopes -join ', ')
    }
} -Arguments @{
    Authority = $Authority
    ClientId = $ClientId
    ClientSecret = $ClientSecret
    Scopes = $Scopes
    UseJwtAuth = $ClientId -like '*.jwt*'
}

# Duende demo's RSA private key for JWT client authentication (in JWK format)
$duendePrivateKey = @{
    d = 'GmiaucNIzdvsEzGjZjd43SDToy1pz-Ph-shsOUXXh-dsYNGftITGerp8bO1iryXh_zUEo8oDK3r1y4klTonQ6bLsWw4ogjLPmL3yiqsoSjJa1G2Ymh_RY_sFZLLXAcrmpbzdWIAkgkHSZTaliL6g57vA7gxvd8L4s82wgGer_JmURI0ECbaCg98JVS0Srtf9GeTRHoX4foLWKc1Vq6NHthzqRMLZe-aRBNU9IMvXNd7kCcIbHCM3GTD_8cFj135nBPP2HOgC_ZXI1txsEf-djqJj8W5vaM7ViKU28IDv1gZGH3CatoysYx6jv1XJVvb2PH8RbFKbJmeyUm3Wvo-rgQ'
    dp = 'YNjVBTCIwZD65WCht5ve06vnBLP_Po1NtL_4lkholmPzJ5jbLYBU8f5foNp8DVJBdFQW7wcLmx85-NC5Pl1ZeyA-Ecbw4fDraa5Z4wUKlF0LT6VV79rfOF19y8kwf6MigyrDqMLcH_CRnRGg5NfDsijlZXffINGuxg6wWzhiqqE'
    dq = 'LfMDQbvTFNngkZjKkN2CBh5_MBG6Yrmfy4kWA8IC2HQqID5FtreiY2MTAwoDcoINfh3S5CItpuq94tlB2t-VUv8wunhbngHiB5xUprwGAAnwJ3DL39D2m43i_3YP-UO1TgZQUAOh7Jrd4foatpatTvBtY3F1DrCrUKE5Kkn770M'
    e = 'AQAB'
    kid = 'ZzAjSnraU3bkWGnnAqLapYGpTyNfLbjbzgAPbbW2GEA'
    kty = 'RSA'
    n = 'wWwQFtSzeRjjerpEM5Rmqz_DsNaZ9S1Bw6UbZkDLowuuTCjBWUax0vBMMxdy6XjEEK4Oq9lKMvx9JzjmeJf1knoqSNrox3Ka0rnxXpNAz6sATvme8p9mTXyp0cX4lF4U2J54xa2_S9NF5QWvpXvBeC4GAJx7QaSw4zrUkrc6XyaAiFnLhQEwKJCwUw4NOqIuYvYp_IXhw-5Ti_icDlZS-282PcccnBeOcX7vc21pozibIdmZJKqXNsL1Ibx5Nkx1F1jLnekJAmdaACDjYRLL_6n3W4wUp19UvzB1lGtXcJKLLkqB6YDiZNu16OSiSprfmrRXvYmvD8m6Fnl5aetgKw'
    p = '7enorp9Pm9XSHaCvQyENcvdU99WCPbnp8vc0KnY_0g9UdX4ZDH07JwKu6DQEwfmUA1qspC-e_KFWTl3x0-I2eJRnHjLOoLrTjrVSBRhBMGEH5PvtZTTThnIY2LReH-6EhceGvcsJ_MhNDUEZLykiH1OnKhmRuvSdhi8oiETqtPE'
    q = '0CBLGi_kRPLqI8yfVkpBbA9zkCAshgrWWn9hsq6a7Zl2LcLaLBRUxH0q1jWnXgeJh9o5v8sYGXwhbrmuypw7kJ0uA3OgEzSsNvX5Ay3R9sNel-3Mqm8Me5OfWWvmTEBOci8RwHstdR-7b9ZT13jk-dsZI7OlV_uBja1ny9Nz9ts'
    qi = 'pG6J4dcUDrDndMxa-ee1yG4KjZqqyCQcmPAfqklI2LmnpRIjcK78scclvpboI3JQyg6RCEKVMwAhVtQM6cBcIO3JrHgqeYDblp5wXHjto70HVW6Z8kBruNx1AH9E8LzNvSRL-JVTFzBkJuNgzKQfD0G77tQRgJ-Ri7qu3_9o1M4'
}

# Helper function to create JWT client assertion for private_key_jwt authentication
function New-JwtClientAssertion {
    param(
        [string]$ClientId,
        [string]$TokenEndpoint,
        [hashtable]$PrivateKey
    )

    # JWT Header (RS256 algorithm)
    $header = @{
        alg = 'RS256'
        typ = 'JWT'
        kid = $PrivateKey.kid
    } | ConvertTo-Json -Compress

    # JWT Claims (client assertion)
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $claims = @{
        iss = $ClientId              # Issuer = client_id
        sub = $ClientId              # Subject = client_id
        aud = $TokenEndpoint         # Audience = token endpoint
        jti = [Guid]::NewGuid().ToString()  # Unique JWT ID
        exp = $now + 300             # Expires in 5 minutes
        iat = $now                   # Issued at
    } | ConvertTo-Json -Compress

    # Base64Url encode header and claims
    $headerBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($header)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    $claimsBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($claims)).TrimEnd('=').Replace('+', '-').Replace('/', '_')

    # Create signing payload
    $signingPayload = "$headerBase64.$claimsBase64"

    # Convert JWK to RSA parameters and sign
    try {
        # Decode Base64Url JWK values
        $modulus = [Convert]::FromBase64String(($PrivateKey.n.Replace('-', '+').Replace('_', '/') + '=='))
        $exponent = [Convert]::FromBase64String(($PrivateKey.e.Replace('-', '+').Replace('_', '/') + '=='))
        $d = [Convert]::FromBase64String(($PrivateKey.d.Replace('-', '+').Replace('_', '/') + '=='))
        $p = [Convert]::FromBase64String(($PrivateKey.p.Replace('-', '+').Replace('_', '/') + '=='))
        $q = [Convert]::FromBase64String(($PrivateKey.q.Replace('-', '+').Replace('_', '/') + '=='))
        $dp = [Convert]::FromBase64String(($PrivateKey.dp.Replace('-', '+').Replace('_', '/') + '=='))
        $dq = [Convert]::FromBase64String(($PrivateKey.dq.Replace('-', '+').Replace('_', '/') + '=='))
        $qi = [Convert]::FromBase64String(($PrivateKey.qi.Replace('-', '+').Replace('_', '/') + '=='))

        # Create RSA from parameters
        $rsa = [System.Security.Cryptography.RSA]::Create()
        $rsaParams = [System.Security.Cryptography.RSAParameters]::new()
        $rsaParams.Modulus = $modulus
        $rsaParams.Exponent = $exponent
        $rsaParams.D = $d
        $rsaParams.P = $p
        $rsaParams.Q = $q
        $rsaParams.DP = $dp
        $rsaParams.DQ = $dq
        $rsaParams.InverseQ = $qi
        $rsa.ImportParameters($rsaParams)

        # Sign the payload with SHA256
        $dataToSign = [Text.Encoding]::UTF8.GetBytes($signingPayload)
        $signature = $rsa.SignData($dataToSign, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)

        # Base64Url encode signature
        $signatureBase64 = [Convert]::ToBase64String($signature).TrimEnd('=').Replace('+', '-').Replace('/', '_')

        # Return complete JWT
        return "$signingPayload.$signatureBase64"

    } catch {
        Write-Error "Failed to sign JWT: $_"
        return $null
    }
}

# 6) Get access token endpoint
Add-KrMapRoute -Verbs Get -Pattern '/token' -ScriptBlock {


    $tokenEndpoint = "$Authority/connect/token"
    $scope = $Scopes -join ' '

    Write-KrLog -Level Information -Message 'Requesting M2M token from {endpoint}' -Values $tokenEndpoint

    # Prepare client credentials token request
    $body = @{
        grant_type = 'client_credentials'
        client_id = $ClientId
        scope = $scope
    }

    # Check if DPoP is required (not implemented)
    if ($ClientId -eq 'm2m.dpop') {
        Write-KrLog -Level Warning -Message 'DPoP client requested but DPoP is not implemented'
        Write-KrJsonResponse @{
            success = $false
            error = 'dpop_not_implemented'
            message = 'DPoP (Demonstrating Proof-of-Possession) is not implemented in this demo'
            explanation = @{
                what_is_dpop = 'DPoP binds access tokens to a specific client key, preventing token theft'
                requirements = @(
                    'Generate ephemeral public/private key pair'
                    'Create DPoP proof JWT for each request (includes method, URL, timestamp)'
                    'Send proof in DPoP header'
                    'Use proof hash (jkt) when requesting tokens'
                )
                complexity = 'DPoP requires generating and managing ephemeral keys and per-request proofs'
                spec = 'RFC 9449 - OAuth 2.0 Demonstrating Proof of Possession (DPoP)'
            }
            suggestion = 'Use -ClientId "m2m", "m2m.short", "m2m.jwt", or "m2m.short.jwt" for working examples'
        } -StatusCode 501
        return
    }

    # Choose authentication method based on client type
    if ($UseJwtAuth) {
        # JWT Bearer authentication (private_key_jwt)
        Write-KrLog -Level Information -Message 'Using JWT Bearer authentication (private_key_jwt)'

        # Create and sign JWT client assertion
        $jwt = New-JwtClientAssertion -ClientId $ClientId -TokenEndpoint $tokenEndpoint -PrivateKey $DuendePrivateKey

        if (-not $jwt) {
            Write-KrJsonResponse @{
                success = $false
                error = 'jwt_signing_failed'
                message = 'Failed to sign JWT client assertion'
            } -StatusCode 500
            return
        }

        # Use JWT client assertion instead of client secret
        $body.client_assertion_type = 'urn:ietf:params:oauth:client-assertion-type:jwt-bearer'
        $body.client_assertion = $jwt

        Write-KrLog -Level Debug -Message 'JWT client assertion created and signed successfully'
    } else {
        # Client secret authentication (client_secret_post)
        $body.client_secret = $ClientSecret
    }

    try {
        # Request access token from token endpoint
        $response = Invoke-RestMethod -Uri $tokenEndpoint -Method Post -Body $body -ContentType 'application/x-www-form-urlencoded'

        Write-KrLog -Level Information -Message 'Successfully received access token, expires in {seconds}s' -Values $response.expires_in

        Write-KrJsonResponse @{
            success = $true
            grant_type = 'client_credentials'
            token_type = $response.token_type
            expires_in = $response.expires_in
            scope = $response.scope
            access_token = $response.access_token
            note = 'This token represents the application itself, not a user. Use it in the Authorization header: Bearer <token>'
        }
    } catch {
        Write-KrLog -Level Error -Message 'Failed to obtain M2M token: {error}' -Values $_.Exception.Message

        Write-KrJsonResponse @{
            success = $false
            error = $_.Exception.Message
            details = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message | ConvertFrom-Json } else { $null }
        } -StatusCode 500
    }
} -Arguments @{
    Authority = $Authority
    ClientId = $ClientId
    ClientSecret = $ClientSecret
    Scopes = $Scopes
    UseJwtAuth = $ClientId -like '*.jwt*'
    DuendePrivateKey = $duendePrivateKey
}

# 7) Get and decode token endpoint
Add-KrMapRoute -Verbs Get -Pattern '/token/decode' -ScriptBlock {


    $tokenEndpoint = "$Authority/connect/token"
    $scope = $Scopes -join ' '

    # Prepare client credentials token request
    $body = @{
        grant_type = 'client_credentials'
        client_id = $ClientId
        scope = $scope
    }

    # Choose authentication method
    if ($UseJwtAuth) {
        $jwt = New-JwtClientAssertion -ClientId $ClientId -TokenEndpoint $tokenEndpoint -PrivateKey $DuendePrivateKey
        if (-not $jwt) {
            Write-KrJsonResponse @{ success = $false; error = 'jwt_signing_failed' } -StatusCode 500
            return
        }
        $body.client_assertion_type = 'urn:ietf:params:oauth:client-assertion-type:jwt-bearer'
        $body.client_assertion = $jwt
    } else {
        $body.client_secret = $ClientSecret
    }

    try {
        # Request access token
        $response = Invoke-RestMethod -Uri $tokenEndpoint -Method Post -Body $body -ContentType 'application/x-www-form-urlencoded'

        # Decode JWT token to show claims (for demo purposes only)
        $claims = $null
        $tokenParts = $response.access_token -split '\.'
        if ($tokenParts.Count -ge 2) {
            # Decode JWT payload (Base64Url decode)
            $payload = $tokenParts[1]
            # Add padding if needed
            $padding = (4 - ($payload.Length % 4)) % 4
            $payload = $payload + ('=' * $padding)
            $payload = $payload.Replace('-', '+').Replace('_', '/')

            $payloadJson = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload))
            $claims = $payloadJson | ConvertFrom-Json
        }

        Write-KrJsonResponse @{
            success = $true
            grant_type = 'client_credentials'
            token_type = $response.token_type
            expires_in = $response.expires_in
            scope = $response.scope
            access_token_preview = $response.access_token.Substring(0, [Math]::Min(50, $response.access_token.Length)) + '...'
            claims = $claims
            explanation = @{
                sub = 'Subject - the client ID that was authenticated'
                client_id = 'The OAuth client identifier'
                scope = 'Granted scopes (permissions)'
                iss = 'Issuer - who issued this token'
                aud = 'Audience - who this token is intended for'
                exp = 'Expiration time (Unix timestamp)'
                iat = 'Issued at time (Unix timestamp)'
                nbf = 'Not before time (Unix timestamp)'
                jti = 'JWT ID - unique identifier for this token'
            }
        }
    } catch {
        Write-KrJsonResponse @{
            success = $false
            error = $_.Exception.Message
            details = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message | ConvertFrom-Json } else { $null }
        } -StatusCode 500
    }
} -Arguments @{
    Authority = $Authority
    ClientId = $ClientId
    ClientSecret = $ClientSecret
    Scopes = $Scopes
    UseJwtAuth = $ClientId -like '*.jwt*'
    DuendePrivateKey = $duendePrivateKey
}

# 8) Simulate API call with token
Add-KrMapRoute -Verbs Get -Pattern '/api-call' -ScriptBlock {


    $tokenEndpoint = "$Authority/connect/token"
    $scope = $Scopes -join ' '

    # Prepare client credentials token request
    $body = @{
        grant_type = 'client_credentials'
        client_id = $ClientId
        scope = $scope
    }

    # Choose authentication method
    if ($UseJwtAuth) {
        $jwt = New-JwtClientAssertion -ClientId $ClientId -TokenEndpoint $tokenEndpoint -PrivateKey $DuendePrivateKey
        if (-not $jwt) {
            Write-KrJsonResponse @{ success = $false; error = 'jwt_signing_failed' } -StatusCode 500
            return
        }
        $body.client_assertion_type = 'urn:ietf:params:oauth:client-assertion-type:jwt-bearer'
        $body.client_assertion = $jwt
    } else {
        $body.client_secret = $ClientSecret
    }

    try {
        # Step 1: Get access token
        Write-KrLog -Level Information -Message 'Step 1: Requesting access token...'
        $tokenResponse = Invoke-RestMethod -Uri $tokenEndpoint -Method Post -Body $body -ContentType 'application/x-www-form-urlencoded'

        # Step 2: Use token to call a protected API
        # For demo, we'll call Duende's demo API endpoint
        $apiEndpoint = "$Authority/api/test"
        $headers = @{
            Authorization = "Bearer $($tokenResponse.access_token)"
        }

        Write-KrLog -Level Information -Message 'Step 2: Calling protected API with token...'

        try {
            $apiResponse = Invoke-RestMethod -Uri $apiEndpoint -Method Get -Headers $headers

            Write-KrJsonResponse @{
                success = $true
                step1_token = @{
                    obtained = $true
                    token_type = $tokenResponse.token_type
                    expires_in = $tokenResponse.expires_in
                    scope = $tokenResponse.scope
                }
                step2_api_call = @{
                    success = $true
                    endpoint = $apiEndpoint
                    response = $apiResponse
                }
                note = 'Successfully authenticated with client credentials and called a protected API!'
            }
        } catch {
            # API call failed (expected if endpoint doesn't exist in demo)
            Write-KrJsonResponse @{
                success = $true
                step1_token = @{
                    obtained = $true
                    token_type = $tokenResponse.token_type
                    expires_in = $tokenResponse.expires_in
                    scope = $tokenResponse.scope
                }
                step2_api_call = @{
                    success = $false
                    endpoint = $apiEndpoint
                    error = $_.Exception.Message
                    note = 'Token was obtained successfully, but demo API endpoint may not exist. In production, you would call your actual protected API here.'
                }
                how_to_use_token = @{
                    header_name = 'Authorization'
                    header_value = 'Bearer <your_access_token>'
                    example = "Invoke-RestMethod -Uri 'https://api.example.com/data' -Headers @{ Authorization = 'Bearer $($tokenResponse.access_token.Substring(0, 20))...' }"
                }
            }
        }
    } catch {
        Write-KrJsonResponse @{
            success = $false
            error = $_.Exception.Message
            details = if ($_.ErrorDetails.Message) { $_.ErrorDetails.Message | ConvertFrom-Json } else { $null }
        } -StatusCode 500
    }
} -Arguments @{
    Authority = $Authority
    ClientId = $ClientId
    ClientSecret = $ClientSecret
    Scopes = $Scopes
    UseJwtAuth = $ClientId -like '*.jwt*'
    DuendePrivateKey = $duendePrivateKey
}

# 9) Start server
Write-Host "`n=== M2M Client Credentials Demo ===" -ForegroundColor Cyan
Write-Host "Authority: $Authority" -ForegroundColor Yellow
Write-Host "Client ID: $ClientId ($($clientInfo.Description))" -ForegroundColor Yellow
Write-Host "Auth Method: $($clientInfo.AuthMethod)" -ForegroundColor Yellow
Write-Host "Token Lifetime: $($clientInfo.TokenLifetime)" -ForegroundColor Yellow
Write-Host "Scopes: $($Scopes -join ', ')" -ForegroundColor Yellow

if ($ClientId -like '*.jwt*') {
    Write-Host "`n✅ JWT Bearer authentication enabled with RSA private key signing!" -ForegroundColor Green
    Write-Host 'Using private_key_jwt client assertion for enhanced security.' -ForegroundColor Green
}

if ($ClientId -eq 'm2m.dpop') {
    Write-Host "`n⚠️  WARNING: DPoP is not implemented in this demo" -ForegroundColor Red
    Write-Host 'DPoP requires ephemeral key pairs and per-request JWT proofs (RFC 9449).' -ForegroundColor Yellow
    Write-Host 'The endpoints will return 501 Not Implemented with explanation.' -ForegroundColor Yellow
    Write-Host 'Use -ClientId "m2m" or "m2m.jwt" for working examples.' -ForegroundColor Green
}

Write-Host "`nVisit: http://localhost:$Port" -ForegroundColor Green
Write-Host ''

Start-KrServer -CloseLogsOnExit
