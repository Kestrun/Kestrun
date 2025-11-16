<#
    Sample: OpenID Connect (Duende Demo) - Multiple Client Modes
    Purpose: Demonstrate OIDC login with various Duende demo clients and configurations.

    Available Modes:
      1. interactive.confidential - Standard confidential client with client secret
      2. interactive.confidential.jwt - Confidential client with private_key_jwt authentication
      3. interactive.confidential.short - Confidential with short-lived tokens (75s)
      4. interactive.confidential.short.jwt - Short tokens + JWT authentication
      5. interactive.public - Public client (no secret, requires PKCE)
      6. interactive.public.short - Public client with short-lived tokens (75s)
      7. interactive.confidential.nopkce - Confidential without PKCE
      8. interactive.confidential.hybrid - Hybrid flow
      9. interactive.implicit - Implicit flow (legacy)

    Usage Examples:
      # Standard confidential client (1-hour tokens)
      .\8.12-OIDC.ps1
      .\8.12-OIDC.ps1 -Mode 'interactive.confidential'

      # JWT authentication (more secure than client secrets)
      .\8.12-OIDC.ps1 -Mode 'interactive.confidential.jwt'

      # Short-lived tokens (75 seconds) - test token expiration
      .\8.12-OIDC.ps1 -Mode 'interactive.confidential.short'

      # Public client (no secret, PKCE required)
      .\8.12-OIDC.ps1 -Mode 'interactive.public'

    Notes:
      - Schemes registered: 'oidc', 'oidc.Cookies', 'oidc.Policy'
      - Redirect URI: https://localhost:<Port>/signin-oidc
      - JWT modes use Duende's demo RSA keys from Assets/certs/*.pem
      - For refresh tokens, 'offline_access' scope is included by default
      - Do NOT commit real client secrets in production
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
param(
    # Default HTTPS port changed to 5000 (common ASP.NET dev cert port; matches many demo redirect registrations)
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback,
    [string]$Authority = 'https://demo.duendesoftware.com',
    [ValidateSet(
        'interactive.confidential',
        'interactive.confidential.jwt',
        'interactive.confidential.short',
        'interactive.confidential.short.jwt',
        'interactive.public',
        'interactive.public.short',
        'interactive.confidential.nopkce',
        'interactive.confidential.hybrid',
        'interactive.implicit'
    )]
    [string]$Mode = 'interactive.confidential.jwt',
    # Force replacing 127.0.0.1 host with localhost in redirect_uri (helps when demo client registration only lists localhost)
    [switch]$ForceLocalhostRedirect,
    # Attempt a fallback to 'interactive.confidential' client_id for authorize if jwt client continues to fail (experimental)
    [switch]$EnableFallbackClient
)



# Define all available OIDC client modes
$allModes = @{
    'interactive.confidential' = @{
        ClientId = 'interactive.confidential'
        ClientSecret = 'secret'
        GrantType = 'authorization code and client credentials'
        ResponseType = 'code'
        ResponseMode = $null
        TokenLifetime = '1h'
        RequiresPKCE = $true
        UseJwtAuth = $false
        Scopes = @('openid', 'profile', 'email', 'offline_access', 'api')
        Description = 'Standard confidential client with client secret'
    }
    'interactive.confidential.jwt' = @{
        ClientId = 'interactive.confidential.jwt'
        ClientSecret = $null  # Uses private_key_jwt
        GrantType = 'authorization code and client credentials'
        ResponseType = 'code'
        ResponseMode = $null
        TokenLifetime = '1h'
        RequiresPKCE = $true
        UseJwtAuth = $true
        Scopes = @('openid', 'profile', 'email', 'offline_access', 'api')
        Description = 'Confidential client with private_key_jwt authentication (more secure)'
    }
    'interactive.confidential.short' = @{
        ClientId = 'interactive.confidential.short'
        ClientSecret = 'secret'
        GrantType = 'authorization code and client credentials'
        ResponseType = 'code'
        ResponseMode = $null
        TokenLifetime = '0h 1m 15s (75 seconds)'
        RequiresPKCE = $true
        UseJwtAuth = $false
        Scopes = @('openid', 'profile', 'email', 'offline_access', 'api')
        Description = 'Confidential client with short-lived tokens (useful for testing expiration)'
    }
    'interactive.confidential.short.jwt' = @{
        ClientId = 'interactive.confidential.short.jwt'
        ClientSecret = $null
        GrantType = 'authorization code and client credentials'
        ResponseType = 'code'
        ResponseMode = $null
        TokenLifetime = '0h 1m 15s (75 seconds)'
        RequiresPKCE = $true
        UseJwtAuth = $true
        Scopes = @('openid', 'profile', 'email', 'offline_access', 'api')
        Description = 'Short-lived tokens + JWT authentication'
    }
    'interactive.public' = @{
        ClientId = 'interactive.public'
        ClientSecret = $null
        GrantType = 'authorization code'
        ResponseType = 'code'
        ResponseMode = $null
        TokenLifetime = '1h'
        RequiresPKCE = $true
        UseJwtAuth = $false
        Scopes = @('openid', 'profile', 'email', 'offline_access', 'api')
        Description = 'Public client (no secret, PKCE required)'
    }
    'interactive.public.short' = @{
        ClientId = 'interactive.public.short'
        ClientSecret = $null
        GrantType = 'authorization code'
        ResponseType = 'code'
        ResponseMode = $null
        TokenLifetime = '0h 1m 15s (75 seconds)'
        RequiresPKCE = $true
        UseJwtAuth = $false
        Scopes = @('openid', 'profile', 'email', 'offline_access', 'api')
        Description = 'Public client with short-lived tokens'
    }
    'interactive.confidential.nopkce' = @{
        ClientId = 'interactive.confidential.nopkce'
        ClientSecret = 'secret'
        GrantType = 'authorization code and client credentials'
        ResponseType = 'code'
        ResponseMode = $null
        TokenLifetime = '1h'
        RequiresPKCE = $false
        UseJwtAuth = $false
        Scopes = @('openid', 'profile', 'email', 'offline_access', 'api')
        Description = 'Confidential client without PKCE (legacy compatibility)'
    }
    'interactive.confidential.hybrid' = @{
        ClientId = 'interactive.confidential.hybrid'
        ClientSecret = 'secret'
        GrantType = 'hybrid and client credentials'
        ResponseType = 'code id_token'  # Hybrid flow requires id_token at authorization endpoint
        ResponseMode = 'form_post'      # Hybrid flow typically uses form_post
        TokenLifetime = '1h'
        RequiresPKCE = $true
        UseJwtAuth = $false
        Scopes = @('openid', 'profile', 'email', 'offline_access', 'api')
        Description = 'Hybrid flow (returns id_token at authorization endpoint)'
    }
    'interactive.implicit' = @{
        ClientId = 'interactive.implicit'
        ClientSecret = $null
        GrantType = 'implicit'
        ResponseType = 'id_token token'  # Implicit flow returns tokens directly
        ResponseMode = 'fragment'        # Implicit flow uses fragment for security
        TokenLifetime = '1h'
        RequiresPKCE = $true
        UseJwtAuth = $false
        Scopes = @('openid', 'profile', 'email')  # No offline_access for implicit
        Description = 'Implicit flow (legacy, not recommended for new applications)'
    }
}

# Get the selected mode configuration
$modeConfig = $allModes[$Mode]
$ClientId = $modeConfig.ClientId
$ClientSecret = $modeConfig.ClientSecret
$Scopes = $modeConfig.Scopes
$UseJwtAuth = $modeConfig.UseJwtAuth


# Effective client id may be overridden for fallback logic (only if requested)
$EffectiveClientId = $ClientId

Initialize-KrRoot -Path $PSScriptRoot

# For JWT authentication modes, load the Duende demo RSA keys
$certificate = $null
$publicKey = $null

if ($UseJwtAuth) {
    $privateKeyPemPath = Join-Path -Path $PSScriptRoot -ChildPath 'Assets' -AdditionalChildPath 'certs', 'private.pem'
    $publicKeyPemPath = Join-Path -Path $PSScriptRoot -ChildPath 'Assets' -AdditionalChildPath 'certs', 'public.pem'

    try {
        if (-not (Test-Path $privateKeyPemPath)) {
            throw "Private key PEM file not found: $privateKeyPemPath"
        }

        # Read and import private key
        $privateKeyContent = Get-Content $privateKeyPemPath -Raw
        $rsa = [System.Security.Cryptography.RSA]::Create()
        $rsa.ImportFromPem($privateKeyContent)

        # Read and import public key
        if (-not (Test-Path $publicKeyPemPath)) {
            throw "Public key PEM file not found: $publicKeyPemPath"
        }
        $publicKeyContent = Get-Content $publicKeyPemPath -Raw
        $rsaPublic = [System.Security.Cryptography.RSA]::Create()
        $rsaPublic.ImportFromPem($publicKeyContent)

        # Extract RSA parameters
        $rsaParams = $rsaPublic.ExportParameters($false)

        # Create self-signed certificate
        $certRequest = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
            'CN=Duende Demo OIDC',
            $rsa,
            [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
        )

        $certificate = $certRequest.CreateSelfSigned(
            [DateTimeOffset]::Now.AddDays(-1),
            [DateTimeOffset]::Now.AddYears(1)
        )

        # Build public key JWK
        $publicKey = @{
            kty = 'RSA'
            n = ConvertTo-KrBase64Url $rsaParams.Modulus
            e = ConvertTo-KrBase64Url $rsaParams.Exponent
            kid = (Get-KrJwkThumbprint -Certificate $certificate)
        }

        Write-Host '✅ JWT authentication enabled - loaded RSA keys from PEM files' -ForegroundColor Green
    } catch {
        Write-Error "Failed to load RSA keys for JWT authentication: $_"
        $certificate = $null
        return 1
    }
}



# 1) Logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null


# 2) Server
New-KrServer -Name 'OIDC Duende Demo'

# 3) HTTPS endpoint (primary)
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -SelfSignedCert

# 4) HTTP endpoint for redirect (optional - redirects HTTP to HTTPS)
# Add-KrEndpoint -Port 5001 -IPAddress $IPAddress
# Add-KrHttpsRedirection -HttpPort 5001 -HttpsPort $Port

# 5) OpenID Connect auth (adds 'oidc', 'oidc.Cookies', 'oidc.Policy')
if ($UseJwtAuth -and $certificate) {
    # JWT authentication requires custom event handling to inject client_assertion
    Write-KrLog -Level Information -Message 'Configuring OIDC with private_key_jwt authentication'

    # Create options object to configure events
    $oidcOptions = [Kestrun.Authentication.OidcOptions]::new()
    $oidcOptions.Authority = $Authority
    $oidcOptions.ClientId = $EffectiveClientId
    $oidcOptions.UsePkce = $modeConfig.RequiresPKCE
    # Enable token persistence and userinfo claims retrieval
    $oidcOptions.SaveTokens = $true
    $oidcOptions.GetClaimsFromUserInfoEndpoint = $true
    $oidcOptions.UseSecurityTokenValidator = $true
    $oidcOptions.PushedAuthorizationBehavior = [Microsoft.AspNetCore.Authentication.OpenIdConnect.PushedAuthorizationBehavior]::Disable
    # Add scopes
    $oidcOptions.Scope.Clear()
    foreach ($scope in $Scopes) {
        $oidcOptions.Scope.Add($scope) | Out-Null
    }
    #   $oidcOptions.PushedAuthorizationBehavior = [Microsoft.AspNetCore.Authentication.OpenIdConnect.PushedAuthorizationBehavior]::Disable;



    # Add ResponseType if specified
    if ($modeConfig.ResponseType) {
        switch ($modeConfig.ResponseType) {
            'code' { $oidcOptions.ResponseType = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseType]::Code }
            'code id_token' { $oidcOptions.ResponseType = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseType]::CodeIdToken }
        }
        Write-KrLog -Level Debug -Message 'Using ResponseType: {responseType}' -Values $modeConfig.ResponseType
    }

    # Add ResponseMode if specified
    if ($modeConfig.ResponseMode) {
        switch ($modeConfig.ResponseMode) {
            'form_post' { $oidcOptions.ResponseMode = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseMode]::FormPost }
        }
        Write-KrLog -Level Debug -Message 'Using ResponseMode: {responseMode}' -Values $modeConfig.ResponseMode
    }
    $ClientAssertionJwkJson = @{
        'd' = 'GmiaucNIzdvsEzGjZjd43SDToy1pz-Ph-shsOUXXh-dsYNGftITGerp8bO1iryXh_zUEo8oDK3r1y4klTonQ6bLsWw4ogjLPmL3yiqsoSjJa1G2Ymh_RY_' +
        'sFZLLXAcrmpbzdWIAkgkHSZTaliL6g57vA7gxvd8L4s82wgGer_JmURI0ECbaCg98JVS0Srtf9GeTRHoX4foLWKc1Vq6NHthzqRMLZe-aRBNU9IMvXNd7kCcIbHCM3GTD_' +
        '8cFj135nBPP2HOgC_ZXI1txsEf-djqJj8W5vaM7ViKU28IDv1gZGH3CatoysYx6jv1XJVvb2PH8RbFKbJmeyUm3Wvo-rgQ'
        'dp' = 'YNjVBTCIwZD65WCht5ve06vnBLP_Po1NtL_4lkholmPzJ5jbLYBU8f5foNp8DVJBdFQW7wcLmx85-NC5Pl1ZeyA-Ecbw4fDraa5Z4wUKlF0LT6VV79rfOF19y8kwf6MigyrDqMLcH_CRnRGg5NfDsijlZXffINGuxg6wWzhiqqE'
        'dq' = 'LfMDQbvTFNngkZjKkN2CBh5_MBG6Yrmfy4kWA8IC2HQqID5FtreiY2MTAwoDcoINfh3S5CItpuq94tlB2t-VUv8wunhbngHiB5xUprwGAAnwJ3DL39D2m43i_3YP-UO1TgZQUAOh7Jrd4foatpatTvBtY3F1DrCrUKE5Kkn770M'
        'e' = 'AQAB'
        'kid' = 'ZzAjSnraU3bkWGnnAqLapYGpTyNfLbjbzgAPbbW2GEA'
        'kty' = 'RSA'
        'n' = 'wWwQFtSzeRjjerpEM5Rmqz_DsNaZ9S1Bw6UbZkDLowuuTCjBWUax0vBMMxdy6XjEEK4Oq9lKMvx9JzjmeJf1knoqSNrox3Ka0rnxXpNAz6sATvme8p9mTXyp0cX4lF4U2J54xa2' +
        '_S9NF5QWvpXvBeC4GAJx7QaSw4zrUkrc6XyaAiFnLhQEwKJCwUw4NOqIuYvYp_IXhw-5Ti_icDlZS-282PcccnBeOcX7vc21pozibIdmZJKqXNsL1Ibx5Nkx1F1jLnekJAmdaACDjYRLL_6n3W4wUp19UvzB1lGtXcJKLLkqB6YDiZNu16OSiSprfmrRXvYmvD8m6Fnl5aetgKw'
        'p' = '7enorp9Pm9XSHaCvQyENcvdU99WCPbnp8vc0KnY_0g9UdX4ZDH07JwKu6DQEwfmUA1qspC-e_KFWTl3x0-I2eJRnHjLOoLrTjrVSBRhBMGEH5PvtZTTThnIY2LReH-6EhceGvcsJ_MhNDUEZLykiH1OnKhmRuvSdhi8oiETqtPE'
        'q' = '0CBLGi_kRPLqI8yfVkpBbA9zkCAshgrWWn9hsq6a7Zl2LcLaLBRUxH0q1jWnXgeJh9o5v8sYGXwhbrmuypw7kJ0uA3OgEzSsNvX5Ay3R9sNel-3Mqm8Me5OfWWvmTEBOci8RwHstdR-7b9ZT13jk-dsZI7OlV_uBja1ny9Nz9ts'
        'qi' = 'pG6J4dcUDrDndMxa-ee1yG4KjZqqyCQcmPAfqklI2LmnpRIjcK78scclvpboI3JQyg6RCEKVMwAhVtQM6cBcIO3JrHgqeYDblp5wXHjto70HVW6Z8kBruNx1AH9E8LzNvSRL-JVTFzBkJuNgzKQfD0G77tQRgJ-Ri7qu3_9o1M4'
    }
    $oidcOptions.JwkJson = ($clientAssertionJwkJson | ConvertTo-Json -Compress)
    Add-KrOpenIdConnectAuthentication -Name 'oidc' -Options $oidcOptions

} else {
    # Standard configuration with client secret
    # Apply fallback at configuration time for non-JWT modes if requested (rare)
    if ($EnableFallbackClient.IsPresent -and $ClientId -eq 'interactive.confidential.jwt') {
        $EffectiveClientId = 'interactive.confidential'
        Write-KrLog -Level Warning -Message 'Fallback client id applied at configuration: interactive.confidential'
    }
    $oidcOptions = [Kestrun.Authentication.OidcOptions]::new()
    $oidcOptions.Authority = $Authority
    $oidcOptions.ClientId = $EffectiveClientId
    $oidcOptions.UsePkce = $modeConfig.RequiresPKCE
    # Enable token persistence and userinfo claims retrieval
    $oidcOptions.SaveTokens = $true
    $oidcOptions.GetClaimsFromUserInfoEndpoint = $true
    $oidcOptions.UseSecurityTokenValidator = $true
    $oidcOptions.PushedAuthorizationBehavior = [Microsoft.AspNetCore.Authentication.OpenIdConnect.PushedAuthorizationBehavior]::Disable
    # Add scopes
    $oidcOptions.Scope.Clear()
    foreach ($scope in $Scopes) {
        $oidcOptions.Scope.Add($scope) | Out-Null
    }
    $oidcOptions.ResponseType = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseType]::Code;

    # Add client secret
    if ($ClientSecret) {
        $oidcOptions.ClientSecret = $ClientSecret
    }

    Add-KrOpenIdConnectAuthentication -Options $oidcOptions -Name 'oidc'
}

# 6) Finalize pipeline
Enable-KrConfiguration

# 7) Landing page
Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    Write-KrHtmlResponse -FilePath './Assets/wwwroot/openId/oidcdemo.html' -Variables @{
        mode = $ClientId
        authority = $Authority
        clientId = $ClientId
        description = $modeConfig.Description
        grantType = $modeConfig.GrantType
        tokenLifetime = $modeConfig.TokenLifetime
        requiresPkce = if ($modeConfig.RequiresPKCE) { 'Yes' } else { 'No' }
        authMethod = if ($UseJwtAuth) { 'private_key_jwt (RSA signature)' } elseif ($ClientSecret) { "client_secret_post (secret: $($ClientSecret.Substring(0,3))***)" } else { 'none (public client)' }
        scopes = ($Scopes -join ', ')
        port = $Port
    }
}

Add-KrMapRoute -Verbs Get -Pattern '/login' -ScriptBlock {
    # Manual pre-challenge diagnostics: construct expected authorize URL
    $hostHeader = $Context.Request.Host.Value
    $scheme = if ($Context.Request.IsHttps) { 'https' } else { 'http' }
    $redirectHost = if ($ForceLocalhostRedirect.IsPresent -and $hostHeader -like '127.0.0.1*') { 'localhost' } else { $hostHeader }
    if ($hostHeader -like '127.0.0.1*' -and -not $ForceLocalhostRedirect.IsPresent) {
        Write-KrLog -Level Warning -Message '127.0.0.1 host detected; consider -ForceLocalhostRedirect for demo client compatibility.'
    }
    $redirectUri = "${scheme}://${redirectHost}/signin-oidc"
    $responseType = if ($modeConfig.ResponseType) { $modeConfig.ResponseType } else { 'code' }
    $scopeValue = ($Scopes -join ' ')
    $authorizeEndpoint = "$Authority/connect/authorize"
    $authUrlPreview = "$authorizeEndpoint?client_id=$ClientId&response_type=$responseType&scope=$([Uri]::EscapeDataString($scopeValue))&redirect_uri=$([Uri]::EscapeDataString($redirectUri))"
    Write-KrLog -Level Warning -Message 'Authorize diagnostics: client_id={clientId}, response_type={rt}, redirect_uri={ru}, scope={scope}' -Values $ClientId, $responseType, $redirectUri, $scopeValue
    Write-KrLog -Level Warning -Message 'Authorize base URL: {url}' -Values $authorizeEndpoint
    Write-KrLog -Level Warning -Message 'Authorize assembled (partial preview): {preview}' -Values $authUrlPreview
    if ($scheme -eq 'https' -and $hostHeader -like '127.0.0.1*') {
        Write-KrLog -Level Warning -Message 'Host 127.0.0.1 detected; some demo clients are registered for localhost. Try https://localhost:{port}/ if invalid_client continues.' -Values $Port
    }
    # Proceed with challenge
    Invoke-KrChallenge -Scheme 'oidc' -RedirectUri '/hello'
} -AllowAnonymous

# 8) Protected route group using the policy scheme
#Add-KrRouteGroup -Prefix '/oidc' -AuthorizationSchema 'oidc'  {
Add-KrMapRoute -Verbs Get -Pattern '/hello' -AuthorizationSchema 'oidc' -ScriptBlock {
    Write-Host ($Context.User.Identity | ConvertTo-Json -Depth 5)
    $name = $Context.User.Identity.Name ?? '(no name)'
    $email = $Context.User.FindFirst('email')?.Value ?? 'No email claim'
    $sub = $Context.User.FindFirst('sub')?.Value ?? 'No sub claim'

    # Get authentication details
    $authType = $Context.User.Identity.AuthenticationType ?? 'Unknown'
    $isAuthenticated = $Context.User.Identity.IsAuthenticated

    Write-KrHtmlResponse -FilePath './Assets/wwwroot/openId/protected.html' -Variables @{
        name = $name
        email = $email
        sub = $sub
        authType = $authType
        isAuthenticated = $isAuthenticated
    }
}

Add-KrMapRoute -Verbs Get -Pattern '/me' -AuthorizationSchema 'oidc' -ScriptBlock {
    $claims = foreach ($c in $Context.User.Claims) { @{ Type = $c.Type; Value = $c.Value } }
    Write-KrJsonResponse @{ scheme = 'oidc'; authenticated = $Context.User.Identity.IsAuthenticated; claims = $claims }
}

Add-KrMapRoute -Verbs Get -Pattern '/logout' -AllowAnonymous -ScriptBlock {
    # Use enhanced Invoke-KrCookieSignOut with OIDC support
    # Construct full redirect URI for post-logout redirect
    $uriScheme = if ($Context.Request.IsHttps) { 'https' } else { 'http' }
    $hostValue = $Context.Request.Host.Value
    $postLogoutUri = "${uriScheme}://${hostValue}/"

    Write-KrLog -Level Debug -Message 'Logging out with post_logout_redirect_uri: {uri}' -Values $postLogoutUri
    Invoke-KrCookieSignOut -AuthKind 'Oidc' -Scheme 'oidc' -RedirectUri $postLogoutUri
}

# 9) Start server
Write-KrLog -Level Information -Message '=== OIDC Duende Demo ==='
Write-KrLog -Level Information -Message 'Mode: {mode} ({description})' -Values $Mode, $modeConfig.Description
Write-KrLog -Level Information -Message 'Authority: {authority}' -Values $Authority
Write-KrLog -Level Information -Message 'Client ID (requested): {clientId}' -Values $ClientId
if ($EffectiveClientId -ne $ClientId) {
    Write-KrLog -Level Information -Message 'Client ID (effective): {clientId}' -Values $EffectiveClientId
}
Write-KrLog -Level Information -Message 'Grant Type: {grantType}' -Values $modeConfig.GrantType
Write-KrLog -Level Information -Message 'Token Lifetime: {lifetime}' -Values $modeConfig.TokenLifetime
Write-KrLog -Level Information -Message 'Requires PKCE: {pkce}' -Values $modeConfig.RequiresPKCE
Write-KrLog -Level Information -Message 'Scopes: {scopes}' -Values ($Scopes -join ', ')

if ($UseJwtAuth) {
    Write-KrLog -Level Information -Message '✅ JWT Bearer authentication enabled with RSA private key signing!'
    Write-KrLog -Level Information -Message 'Using private_key_jwt client assertion for enhanced security.'
}

Write-KrLog -Level Information -Message 'Visit: https://localhost:{port} (redirect host fix={fix}, fallback={fallback})' -Values $Port, ($ForceLocalhostRedirect.IsPresent), ($EnableFallbackClient.IsPresent)

Start-KrServer -CloseLogsOnExit
