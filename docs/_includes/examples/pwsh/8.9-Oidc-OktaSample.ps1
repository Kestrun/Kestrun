<#
    Sample: OAuth 2.0 (Authorization Code) + Cookies
    Purpose: External login via GitHub → sign into local Cookies scheme → protect routes.
    File:    8.9-Oidc-OktaSample.ps1
    Notes:   Use HTTPS. For OAuth redirects, SameSite=Lax is recommended (Strict can block cross-site return).
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback,

    # Put your app creds in env vars for convenience
    [string]$ClientId = $env:OKTA_CLIENT_ID,
    [string]$ClientSecret = $env:OKTA_CLIENT_SECRET,

    # GitHub OAuth endpoints
    [string]$Authority = $env:OKTA_AUTHORITY
)
if (([string]::IsNullOrWhiteSpace( $ClientId)) -and
    ([string]::IsNullOrWhiteSpace( $ClientSecret)) -and
    (Test-Path -Path .\Utility\Import-EnvFile.ps1)) {
    & .\Utility\Import-EnvFile.ps1
    $ClientId = $env:OKTA_CLIENT_ID
    $ClientSecret = $env:OKTA_CLIENT_SECRET
    $Authority = $env:OKTA_AUTHORITY
}
# Callback and endpoint paths
$CallbackPath = '/signin-oidc'

# Initialize Kestrun root for resolving relative paths
Initialize-KrRoot -Path $PSScriptRoot

# Tip: Set OKTA_CLIENT_ID / OKTA_CLIENT_SECRET in your environment for local runs
#       $env:OKTA_CLIENT_ID = 'xxx'; $env:OKTA_CLIENT_SECRET = 'yyy'
# 1) Logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2) Server
New-KrServer -Name 'Auth Okta Demo'

# 3) Listener (self-signed for local https)
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -SelfSignedCert


$options = [Kestrun.Authentication.OidcOptions]::new()

$options.Authority = $Authority
$options.ClientId = $ClientId
$options.ClientSecret = $ClientSecret
$options.CallbackPath = $CallbackPath
$options.SignedOutCallbackPath = '/signout-callback-oidc'  # Logout callback path
$options.SaveTokens = $true
$options.ResponseType = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseType]::Code;
$options.UsePkce = $true;
$options.GetClaimsFromUserInfoEndpoint = $true
$options.Scope.Clear()
$options.Scope.Add('openid') | Out-Null
$options.Scope.Add('profile') | Out-Null
$options.Scope.Add('email') | Out-Null

# Map the 'name' claim from OIDC token to Identity.Name
# Okta sends the email in the 'name' claim, which gets mapped to ClaimTypes.Name
$options.TokenValidationParameters.NameClaimType = 'name'

# 5) OAuth2 scheme (AUTH CHALLENGE) — signs into the 'Cookies' scheme above
Add-KrOpenIdConnectAuthentication -AuthenticationScheme 'Okta' -Options $options

# 6) Finalize configuration
Enable-KrConfiguration

# 7) Public landing
Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    if (-not $Context.User.Identity.IsAuthenticated) {
        Write-KrHtmlResponse -FilePath './Assets/wwwroot/okta/okta-auth.html'
    } else {
        $name = $Context.User.Identity.Name ?? '(no name)'
        $nickname = $Context.User.FindFirst('nickname')?.Value ?? 'N/A'
        $email = $Context.User.FindFirst('email')?.Value ?? 'No email claim'
        $emailVerified = $Context.User.FindFirst('email_verified')?.Value ?? 'N/A'
        $picture = $Context.User.FindFirst('picture')?.Value ?? ''
        $sub = $Context.User.FindFirst('sub')?.Value ?? 'No sub claim'
        $updatedAt = $Context.User.FindFirst('updated_at')?.Value ?? 'N/A'
        $authType = $Context.User.Identity.AuthenticationType ?? 'Unknown'
        $isAuthenticated = $Context.User.Identity.IsAuthenticated

        $claimsString = ($Context.User.Claims |
                ForEach-Object { "{ Type = '$($_.Type)'; Value = '$($_.Value)' }" }
        ) -join "`n"

        Write-KrHtmlResponse -FilePath './Assets/wwwroot/okta/protected.html' -Variables @{
            name = $name
            nickname = $nickname
            email = $email
            emailVerified = $emailVerified
            picture = $picture
            sub = $sub
            updatedAt = $updatedAt
            authType = $authType
            isAuthenticated = $isAuthenticated
            claims = $claimsString
        }
    }
}

# 8) Protected routes (challenge via 'Okta', session via 'Cookies')
Add-KrMapRoute -Verbs Get -Pattern '/login' -ScriptBlock {
    Invoke-KrChallenge -Scheme 'Okta' -RedirectUri '/'
} -AllowAnonymous

Add-KrMapRoute -Verbs Get -Pattern '/me' -ScriptBlock {
    Write-KrLog -Level Information -Message "'/secure/oauth/me accessed by user: $($Context.User?.Identity?.Name)'"
    $claims = @()
    foreach ($c in $Context.User.Claims) {
        $claims += @{ Type = $c.Type; Value = $c.Value }
    }
    Write-KrJsonResponse @{ authenticated = $Context.User.Identity.IsAuthenticated; claims = $claims }
}

# Logout callback page - shown after OIDC provider completes logout
Add-KrMapRoute -Verbs Get -Pattern '/signout' -ScriptBlock {
    Write-KrHtmlResponse -FilePath './Assets/wwwroot/okta/logout.html'
} -AllowAnonymous

# sign-out clears the session cookie and triggers OIDC logout
Add-KrMapRoute -Verbs Get -Pattern '/logout' -ScriptBlock {
    # Construct the full post-logout redirect URI
    $uriScheme = if ($Context.Request.IsHttps) { 'https' } else { 'http' }
    $hostValue = $Context.Request.Host.Value
    $postLogoutUri = "${uriScheme}://${hostValue}/signout"

    Invoke-KrCookieSignOut -Scheme 'Okta' -AuthKind Oidc -RedirectUri $postLogoutUri
}

# 9) Start
Start-KrServer -CloseLogsOnExit
