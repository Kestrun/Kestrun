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

      3. m2m.dpop (DPoP enabled) ✅ FULLY WORKING
         - Client ID: m2m.dpop
         - Client Secret: secret
         - Grant Type: client_credentials
         - Auth Method: client_secret_post + DPoP proof
         - Access Token Lifetime: 1 hour
         - Allowed Scopes: api
         - Requires DPoP (Demonstrating Proof-of-Possession)
         - DPoP binds tokens to a client key (per-request proofs)
         - Prevents token theft and replay attacks
         - Uses RSA key to sign DPoP proof JWTs

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
    [string]$ClientId = 'm2m.jwt',
    [string]$ClientSecret = 'secret',
    [string[]]$Scopes = @('api')
)


# Duende demo's RSA key pair (PEM format) - loaded from Assets/certs folder
# This is the demo key from https://demo.duendesoftware.com - NOT for production!
$duendePrivateKeyPemPath = Join-Path -Path $PSScriptRoot -ChildPath 'Assets' -AdditionalChildPath 'certs', 'private.pem'
$duendePublicKeyPemPath = Join-Path -Path $PSScriptRoot -ChildPath 'Assets' -ChildPath 'certs' 'public.pem'

# Certificate and public key will be created after Initialize-KrRoot
$duendeCert = $null
$duendePublicKey = $null




# Helper function to calculate JWK thumbprint (for DPoP jkt claim)
function Get-JwkThumbprint {
    param([hashtable]$PublicKey)

    # RFC 7638: JWK Thumbprint - SHA-256 hash of canonical JWK
    # For RSA: {"e":"<e>","kty":"RSA","n":"<n>"}
    $canonicalJwk = "{`"e`":`"$($PublicKey.e)`",`"kty`":`"RSA`",`"n`":`"$($PublicKey.n)`"}"
    $bytes = [Text.Encoding]::UTF8.GetBytes($canonicalJwk)
    $hash = [Security.Cryptography.SHA256]::HashData($bytes)

    # Base64Url encode
    return [Convert]::ToBase64String($hash).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

# Helper function to create DPoP proof JWT using Kestrun's native JWT builder
function New-DPoPProof {
    param(
        [string]$HttpMethod,        # e.g., "POST", "GET"
        [string]$HttpUri,           # Full URI without query/fragment
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [hashtable]$PublicKey,      # Public key components for JWK header
        [string]$AccessToken = $null  # Optional: for API calls (adds 'ath' claim)
    )

    try {
        # Build DPoP proof JWT using Kestrun's native builder
        $builder = New-KrJWTBuilder |
            Add-KrJWTHeader -Name 'typ' -Value 'dpop+jwt' |
            Add-KrJWTHeader -Name 'jwk' -Value @{
                kty = $PublicKey.kty
                n = $PublicKey.n
                e = $PublicKey.e
            } |
            Add-KrJWTClaim -ClaimType 'htm' -Value $HttpMethod |
            Add-KrJWTClaim -ClaimType 'htu' -Value $HttpUri |
            Add-KrJWTClaim -ClaimType 'iat' -Value ([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()) |
            Add-KrJWTClaim -ClaimType 'jti' -Value ([Guid]::NewGuid().ToString())

        # Add access token hash for API calls (RFC 9449 section 4.2)
        if ($AccessToken) {
            $tokenBytes = [Text.Encoding]::UTF8.GetBytes($AccessToken)
            $hashBytes = [Security.Cryptography.SHA256]::HashData($tokenBytes)
            $ath = [Convert]::ToBase64String($hashBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
            $builder = $builder | Add-KrJWTClaim -ClaimType 'ath' -Value $ath
        }

        # Sign with certificate and build
        $result = $builder |
            Protect-KrJWT -X509Certificate $Certificate -Algorithm RS256 |
            Build-KrJWT

        return $result | Get-KrJWTToken

    } catch {
        Write-Error "Failed to create DPoP proof: $_"
        return $null
    }
}

# Helper function to create JWT client assertion using Kestrun's native JWT builder
function New-JwtClientAssertion {
    param(
        [string]$ClientId,
        [string]$TokenEndpoint,
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [string]$KeyId
    )

    try {
        # Build JWT client assertion using Kestrun's native builder
        $result = New-KrJWTBuilder |
            Add-KrJWTHeader -Name 'kid' -Value $KeyId |
            Add-KrJWTIssuer -Issuer $ClientId |
            Add-KrJWTSubject -Subject $ClientId |
            Add-KrJWTAudience -Audience $TokenEndpoint |
            Add-KrJWTClaim -ClaimType 'jti' -Value ([Guid]::NewGuid().ToString()) |
            Limit-KrJWTValidity -Minutes 5 |
            Protect-KrJWT -X509Certificate $Certificate -Algorithm RS256 |
            Build-KrJWT

        return $result | Get-KrJWTToken

    } catch {
        Write-Error "Failed to create JWT client assertion: $_"
        return $null
    }
}

<#
.SYNOPSIS
    Helper function to create JWT client assertion using Kestrun's native JWT builder
    This function generates a JWT client assertion for authentication with the token endpoint.
.DESCRIPTION
    The JWT client assertion is signed with the provided X.509 certificate and includes standard claims.
.PARAMETER ClientId
    The client ID of the application.
.PARAMETER TokenEndpoint
    The token endpoint URL (audience claim).
.PARAMETER Certificate
    The X.509 certificate used to sign the JWT.
.PARAMETER KeyId
    The key ID (kid) to include in the JWT header.
.OUTPUTS
    The signed JWT client assertion as a string.
#>
function New-JwtClientAssertion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ClientId,
        [Parameter(Mandatory = $true)]
        [string]$TokenEndpoint,
        [Parameter(Mandatory = $true)]
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [Parameter(Mandatory = $true)]
        [string]$KeyId
    )

    try {
        # Build JWT client assertion using Kestrun's native builder
        $result = New-KrJWTBuilder |
            Add-KrJWTHeader -Name 'kid' -Value $KeyId |
            Add-KrJWTIssuer -Issuer $ClientId |
            Add-KrJWTSubject -Subject $ClientId |
            Add-KrJWTAudience -Audience $TokenEndpoint |
            Add-KrJWTClaim -ClaimType 'jti' -Value ([Guid]::NewGuid().ToString()) |
            Limit-KrJWTValidity -Lifetime (New-TimeSpan -Minutes 5) |
            Protect-KrJWT -X509Certificate $Certificate -Algorithm RS256 |
            Build-KrJWT

        return $result | Get-KrJWTToken

    } catch {
        Write-Error "Failed to create JWT client assertion: $_"
        return $null
    }
}

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
            Description = 'M2M with DPoP (Demonstrating Proof-of-Possession) - prevents token theft ✅'
            TokenLifetime = '1 hour'
            UseDPoP = $true
            AuthMethod = 'Client Secret + DPoP proof JWT (per-request signed proofs)'
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

# Load RSA private key from PEM file and create certificate
try {
    if (-not (Test-Path $duendePrivateKeyPemPath)) {
        throw "Private key PEM file not found: $duendePrivateKeyPemPath"
    }

    # Read PEM file and import RSA key
    $pemContent = Get-Content $duendePrivateKeyPemPath -Raw
    $rsa = [System.Security.Cryptography.RSA]::Create()
    $rsa.ImportFromPem($pemContent)

    # Extract RSA parameters to build JWK public key components
    $rsaParams = $rsa.ExportParameters($false)  # false = public key only

    # Helper to convert byte array to Base64Url
    function ConvertTo-Base64Url {
        param([byte[]]$Bytes)
        [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    }

    # Build public key JWK for DPoP
    $duendePublicKey = @{
        kty = 'RSA'
        n = ConvertTo-Base64Url $rsaParams.Modulus
        e = ConvertTo-Base64Url $rsaParams.Exponent
        kid = 'ZzAjSnraU3bkWGnnAqLapYGpTyNfLbjbzgAPbbW2GEA'
    }

    # Create a self-signed certificate with the RSA key
    $certRequest = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
        'CN=Duende Demo',
        $rsa,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
    )

    $duendeCert = $certRequest.CreateSelfSigned(
        [DateTimeOffset]::Now.AddDays(-1),
        [DateTimeOffset]::Now.AddYears(1)
    )

    Write-Host '✅ RSA certificate created successfully from PEM' -ForegroundColor Green
    Write-Host "   Certificate Thumbprint: $($duendeCert.Thumbprint)" -ForegroundColor Gray
    Write-Host "   Has Private Key: $($duendeCert.HasPrivateKey)" -ForegroundColor Gray
} catch {
    Write-Warning "Failed to create certificate from PEM: $_"
    Write-Warning 'JWT and DPoP authentication will not work!'
    $duendeCert = $null
}

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

    # Prepare headers
    $headers = @{
        'Content-Type' = 'application/x-www-form-urlencoded'
    }

    # Check if DPoP is required
    if ($UseDPoP) {
        Write-KrLog -Level Information -Message 'Using DPoP (Demonstrating Proof-of-Possession)'

        # Create DPoP proof JWT for token request
        $dpopProof = New-DPoPProof -HttpMethod 'POST' -HttpUri $tokenEndpoint -Certificate $Certificate -PublicKey $PublicKey

        if (-not $dpopProof) {
            Write-KrJsonResponse @{
                success = $false
                error = 'dpop_proof_failed'
                message = 'Failed to create DPoP proof JWT'
            } -StatusCode 500
            return
        }

        # Add DPoP header
        $headers.DPoP = $dpopProof

        # Add JWK thumbprint to request (RFC 9449 requires jkt)
        $jkt = Get-JwkThumbprint -PublicKey $PublicKey
        $body.dpop_jkt = $jkt

        Write-KrLog -Level Debug -Message 'DPoP proof created and JWK thumbprint added'
    }

    # Choose authentication method based on client type
    if ($UseJwtAuth) {
        # JWT Bearer authentication (private_key_jwt)
        Write-KrLog -Level Information -Message 'Using JWT Bearer authentication (private_key_jwt)'

        # Create and sign JWT client assertion
        $jwt = New-JwtClientAssertion -ClientId $ClientId -TokenEndpoint $tokenEndpoint -Certificate $Certificate -KeyId $PublicKey.kid

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
    }    try {
        # Request access token from token endpoint
        $response = Invoke-RestMethod -Uri $tokenEndpoint -Method Post -Body $body -Headers $headers

        Write-KrLog -Level Information -Message 'Successfully received access token, expires in {seconds}s' -Values $response.expires_in

        $responseData = @{
            success = $true
            grant_type = 'client_credentials'
            token_type = $response.token_type
            expires_in = $response.expires_in
            scope = $response.scope
            access_token = $response.access_token
        }

        if ($UseDPoP) {
            $responseData.dpop = @{
                enabled = $true
                token_binding = 'This token is bound to the DPoP key - cannot be used without DPoP proof'
                note = 'DPoP prevents token theft/replay by requiring per-request signed proofs'
            }
        } else {
            $responseData.note = 'This token represents the application itself, not a user. Use it in the Authorization header: Bearer <token>'
        }

        Write-KrJsonResponse $responseData
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
    UseDPoP = $ClientId -eq 'm2m.dpop'
    Certificate = $duendeCert
    PublicKey = $duendePublicKey
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

    # Prepare headers
    $headers = @{
        'Content-Type' = 'application/x-www-form-urlencoded'
    }

    # Add DPoP proof if needed
    if ($UseDPoP) {
        $dpopProof = New-DPoPProof -HttpMethod 'POST' -HttpUri $tokenEndpoint -Certificate $Certificate -PublicKey $PublicKey
        if (-not $dpopProof) {
            Write-KrJsonResponse @{ success = $false; error = 'dpop_proof_failed' } -StatusCode 500
            return
        }
        $headers.DPoP = $dpopProof
        $jkt = Get-JwkThumbprint -PublicKey $PublicKey
        $body.dpop_jkt = $jkt
    }

    # Choose authentication method
    if ($UseJwtAuth) {
        $jwt = New-JwtClientAssertion -ClientId $ClientId -TokenEndpoint $tokenEndpoint -Certificate $Certificate -KeyId $PublicKey.kid
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
        $response = Invoke-RestMethod -Uri $tokenEndpoint -Method Post -Body $body -Headers $headers

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
    UseDPoP = $ClientId -eq 'm2m.dpop'
    Certificate = $duendeCert
    PublicKey = $duendePublicKey
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

    # Prepare headers
    $tokenHeaders = @{
        'Content-Type' = 'application/x-www-form-urlencoded'
    }

    # Add DPoP proof if needed
    if ($UseDPoP) {
        $dpopProof = New-DPoPProof -HttpMethod 'POST' -HttpUri $tokenEndpoint -Certificate $Certificate -PublicKey $PublicKey
        if (-not $dpopProof) {
            Write-KrJsonResponse @{ success = $false; error = 'dpop_proof_failed' } -StatusCode 500
            return
        }
        $tokenHeaders.DPoP = $dpopProof
        $jkt = Get-JwkThumbprint -PublicKey $PublicKey
        $body.dpop_jkt = $jkt
    }

    # Choose authentication method
    if ($UseJwtAuth) {
        $jwt = New-JwtClientAssertion -ClientId $ClientId -TokenEndpoint $tokenEndpoint -Certificate $Certificate -KeyId $PublicKey.kid
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
        $tokenResponse = Invoke-RestMethod -Uri $tokenEndpoint -Method Post -Body $body -Headers $tokenHeaders

        # Step 2: Use token to call a protected API
        # For DPoP, use the specific DPoP test endpoint
        $apiEndpoint = if ($UseDPoP) {
            "$Authority/api/dpop/test"
        } else {
            "$Authority/api/test"
        }

        $apiHeaders = @{
            Authorization = "Bearer $($tokenResponse.access_token)"
        }

        # Add DPoP proof for API call if using DPoP
        if ($UseDPoP) {
            Write-KrLog -Level Information -Message 'Creating DPoP proof for API call'
            $apiDPoPProof = New-DPoPProof -HttpMethod 'GET' -HttpUri $apiEndpoint -Certificate $Certificate -PublicKey $PublicKey -AccessToken $tokenResponse.access_token

            if (-not $apiDPoPProof) {
                Write-KrJsonResponse @{
                    success = $false
                    error = 'dpop_proof_failed'
                    message = 'Failed to create DPoP proof for API call'
                } -StatusCode 500
                return
            }

            $apiHeaders.DPoP = $apiDPoPProof
            Write-KrLog -Level Debug -Message 'DPoP proof added to API request'
        }        Write-KrLog -Level Information -Message 'Step 2: Calling protected API with token...'

        try {
            $apiResponse = Invoke-RestMethod -Uri $apiEndpoint -Method Get -Headers $apiHeaders

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
    UseDPoP = $ClientId -eq 'm2m.dpop'
    Certificate = $duendeCert
    PublicKey = $duendePublicKey
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
    Write-Host "`n✅ DPoP (Demonstrating Proof-of-Possession) enabled!" -ForegroundColor Green
    Write-Host 'Using per-request signed DPoP proofs to bind tokens to key (RFC 9449).' -ForegroundColor Green
    Write-Host 'This prevents token theft and replay attacks - tokens only work with DPoP proofs.' -ForegroundColor Green
}

Write-Host "`nVisit: http://localhost:$Port" -ForegroundColor Green
Write-Host ''

Start-KrServer -CloseLogsOnExit
