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
    [string]$Mode = 'interactive.confidential'
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

<#
.SYNOPSIS
    Creates a JWT client assertion for OIDC token requests.
.DESCRIPTION
    Used for private_key_jwt client authentication method.
.OUTPUTS
    The signed JWT client assertion as a string.
#>
function New-OidcJwtClientAssertion {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    param(
        [string]$ClientId,
        [string]$TokenEndpoint,
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [string]$KeyId
    )

    try {
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
    $oidcOptions = [Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions]::new()
    $oidcOptions.Authority = $Authority
    $oidcOptions.ClientId = $ClientId
    $oidcOptions.UsePkce = $modeConfig.RequiresPKCE

    # Add scopes
    $oidcOptions.Scope.Clear()
    foreach ($scope in $Scopes) {
        $oidcOptions.Scope.Add($scope) | Out-Null
    }

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

    # Configure event to inject JWT client assertion during token exchange
    # Capture variables in closure for event handler
    $capturedClientId = $ClientId
    $capturedCertificate = $certificate
    $capturedPublicKey = $publicKey

    $oidcOptions.Events = [Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectEvents]::new()
    $oidcOptions.Events.OnAuthorizationCodeReceived = {
        param($context)

        $tokenEndpoint = $context.Options.Configuration.TokenEndpoint
        Write-KrLog -Level Debug -Message 'OnAuthorizationCodeReceived: Creating JWT client assertion for token endpoint: {endpoint}' -Values $tokenEndpoint

        # Create JWT client assertion using captured variables
        $jwt = New-OidcJwtClientAssertion -ClientId $capturedClientId -TokenEndpoint $tokenEndpoint -Certificate $capturedCertificate -KeyId $capturedPublicKey.kid

        if ($jwt) {
            # Inject JWT assertion into token request (remove client_secret if present)
            $context.TokenEndpointRequest.ClientSecret = $null
            $context.TokenEndpointRequest.ClientAssertionType = 'urn:ietf:params:oauth:client-assertion-type:jwt-bearer'
            $context.TokenEndpointRequest.ClientAssertion = $jwt
            Write-KrLog -Level Debug -Message 'JWT client assertion injected into token request'
        } else {
            Write-KrLog -Level Error -Message 'Failed to create JWT client assertion'
        }

        [System.Threading.Tasks.Task]::CompletedTask
    }.GetNewClosure()

    Add-KrOpenIdConnectAuthentication -Name 'oidc' -Options $oidcOptions

} else {
    # Standard configuration with client secret
    $oidcParams = @{
        Name = 'oidc'
        Authority = $Authority
        ClientId = $ClientId
        Scope = $Scopes
        UsePkce = $modeConfig.RequiresPKCE
    }

    # Add ResponseType if specified
    if ($modeConfig.ResponseType) {
        $oidcParams.ResponseType = $modeConfig.ResponseType
        Write-KrLog -Level Debug -Message 'Using ResponseType: {responseType}' -Values $modeConfig.ResponseType
    }

    # Add ResponseMode if specified
    if ($modeConfig.ResponseMode) {
        $oidcParams.ResponseMode = $modeConfig.ResponseMode
        Write-KrLog -Level Debug -Message 'Using ResponseMode: {responseMode}' -Values $modeConfig.ResponseMode
    }

    # Add client secret
    if ($ClientSecret) {
        $oidcParams.ClientSecret = $ClientSecret
    }

    Add-KrOpenIdConnectAuthentication @oidcParams
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
    # Use the new Invoke-KrChallenge function for cleaner OIDC login
    Invoke-KrChallenge -Scheme 'oidc' -RedirectUri '/hello'
} -AllowAnonymous

# 8) Protected route group using the policy scheme
#Add-KrRouteGroup -Prefix '/oidc' -AuthorizationSchema 'oidc'  {
Add-KrMapRoute -Verbs Get -Pattern '/hello' -AuthorizationSchema 'oidc' -ScriptBlock {
    write-host ($Context.User.Identity|ConvertTo-Json -Depth 5)
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
    Invoke-KrCookieSignOut -OidcScheme 'oidc' -RedirectUri $postLogoutUri
}

# 9) Start server
Write-KrLog -Level Information -Message '=== OIDC Duende Demo ==='
Write-KrLog -Level Information -Message 'Mode: {mode} ({description})' -Values $Mode, $modeConfig.Description
Write-KrLog -Level Information -Message 'Authority: {authority}' -Values $Authority
Write-KrLog -Level Information -Message 'Client ID: {clientId}' -Values $ClientId
Write-KrLog -Level Information -Message 'Grant Type: {grantType}' -Values $modeConfig.GrantType
Write-KrLog -Level Information -Message 'Token Lifetime: {lifetime}' -Values $modeConfig.TokenLifetime
Write-KrLog -Level Information -Message 'Requires PKCE: {pkce}' -Values $modeConfig.RequiresPKCE
Write-KrLog -Level Information -Message 'Scopes: {scopes}' -Values ($Scopes -join ', ')

if ($UseJwtAuth) {
    Write-KrLog -Level Information -Message '✅ JWT Bearer authentication enabled with RSA private key signing!'
    Write-KrLog -Level Information -Message 'Using private_key_jwt client assertion for enhanced security.'
}

Write-KrLog -Level Information -Message 'Visit: https://localhost:{port}' -Values $Port

Start-KrServer -CloseLogsOnExit
