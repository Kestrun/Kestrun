<#
    Sample: OpenID Connect (Duende Demo) + Cookies + Policy Scheme
    Purpose: Demonstrate OIDC login via https://demo.duendesoftware.com using the convenience wrapper.

    Schemes registered when using Add-KrOpenIdConnectAuthentication -Name 'Oidc':
        1. 'Oidc'          → Remote OIDC handler (challenge/code flow)
        2. 'Oidc.Cookies'  → Local cookie/session persistence
        3. 'Oidc.Policy'   → Policy/forwarding (authenticate via cookies; challenge via OIDC)

    Notes:
      - PKCE and token persistence are enabled by default.
      - Default scopes: openid profile (add email, api, offline_access as needed).
      - For refresh tokens include 'offline_access' (provider must allow it).
            - Your redirect URI must match https://localhost:<Port><CallbackPath>
                For this sample: https://localhost:5000/signin-oidc (unless you change Port/CallbackPath).
      - Duende demo clients:
          * interactive.public (no secret) – public client requiring PKCE
          * interactive.confidential (requires secret 'secret') – confidential client
        Use whichever fits the registration; sample defaults to confidential for illustration.
      - Do NOT commit real client secrets.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingConvertToSecureStringWithPlainText', '')]
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback,
    [string]$Authority = 'https://demo.duendesoftware.com',
    [string]$ClientId = 'interactive.confidential',
    [string]$ClientSecret = $env:DUENDE_CLIENT_SECRET,  # Set to 'secret' for demo confidential client or via env var
    [string[]]$Scopes  # Additional scopes beyond 'openid' and 'profile' (which are added by default)
)

# Prefer a working default: if using the confidential demo client but no secret is provided,
# automatically switch to the public client (no secret required) to avoid a 400 at the token endpoint.
if ($ClientId -eq 'interactive.confidential' -and [string]::IsNullOrWhiteSpace($ClientSecret)) {
    Write-Host 'WARNING: interactive.confidential requires a client secret.' -ForegroundColor Yellow
    Write-Host "         Set `$env:DUENDE_CLIENT_SECRET='secret' or pass -ClientSecret 'secret'." -ForegroundColor Yellow
    Write-Host "         Using default secret 'secret' for Duende demo." -ForegroundColor Yellow
    $ClientSecret = 'secret'  # Default secret for Duende demo
}

Initialize-KrRoot -Path $PSScriptRoot
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

# 5) OpenID Connect auth (adds 'Oidc', 'Oidc.Cookies', 'Oidc.Policy')
$oidcParams = @{
    Name = 'oidc'
    Authority = $Authority
    ClientId = $ClientId
    Scope = @('openid', 'profile', 'email', 'offline_access', 'api')  # Matching working C# code
}
if ($ClientSecret) {
    $oidcParams.ClientSecret = $ClientSecret
}
if ($Scopes -and $Scopes.Count -gt 0) {
    $oidcParams.Scope += $Scopes
}
Add-KrOpenIdConnectAuthentication @oidcParams

# 6) Finalize pipeline
Enable-KrConfiguration

# 7) Landing page
Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    Write-KrHtmlResponse -Template @'
<!doctype html>
<title>OIDC Duende Demo</title>
<h1>OIDC Duende Demo</h1>
<li><a href="/login">Login</a></li>
<p><a href="/hello">Login with OIDC (Policy)</a></p>
<p><a href="/me">Who am I?</a></p>
<p><a href="/logout">Logout</a></p>
<p>Authority: {{authority}}</p>
<p>ClientId: {{client}}</p>
<p>Callback Path: {{callback}}</p>
<p>Scopes: {{scopes}}</p>
<p>Secret: {{secret}}</p>
'@ -Variables @{ authority = $Authority; client = $client; callback = $CallbackPath; scopes = ($Scopes -join ', '); Secret = $Secret }
} -Arguments @{
    authority = $Authority
    client = $ClientId
    CallbackPath = $CallbackPath
    scopes = $Scopes
    Secret = $ClientSecret
}
Add-KrMapRoute -Verbs Get -Pattern '/login2' -Code @'
var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
{
    RedirectUri = "/hello"
};
await Context.HttpContext.ChallengeAsync("oidc", properties);
'@ -AllowAnonymous -Language CSharp

Add-KrMapRoute -Verbs Get -Pattern '/login' -ScriptBlock {
    # Use the new Invoke-KrChallenge function for cleaner OIDC login
    Invoke-KrChallenge -Scheme 'oidc' -RedirectUri '/hello'
} -AllowAnonymous

# 8) Protected route group using the policy scheme
#Add-KrRouteGroup -Prefix '/oidc' -AuthorizationSchema 'oidc'  {
Add-KrMapRoute -Verbs Get -Pattern '/hello' -AuthorizationSchema 'oidc' -ScriptBlock {
    $name = $Context.User.Identity.Name ?? '(no name)'
    #   if ([string]::IsNullOrWhiteSpace($name)) { $name = '(no name claim)' }
    Write-KrHtmlResponse -Template @'
<!doctype html>
<title>OIDC Duende Demo - Hello</title>
<h1>Hello from OIDC, {{name}}</h1>
<li><a href="/login">Login</a></li>
<li><a href="/hello">Hello (requires auth)</a></li>
<p><a href="/me">Who am I?</a></p>
<p><a href="/logout">Logout</a></p>
'@ -Variables @{ name = $name }
}

Add-KrMapRoute -Verbs Get -Pattern '/me' -AuthorizationSchema 'oidc' -ScriptBlock {
    $claims = foreach ($c in $Context.User.Claims) { @{ Type = $c.Type; Value = $c.Value } }
    Write-KrJsonResponse @{ scheme = 'oidc'; authenticated = $Context.User.Identity.IsAuthenticated; claims = $claims }
}

#Add-KrMapRoute -Verbs Get -Pattern '/logout' -AllowAnonymous -Code @'
Add-KrMapRoute -Verbs Get -Pattern '/logout' -AllowAnonymous -ScriptBlock {
    # Use enhanced Invoke-KrCookieSignOut with OIDC support
    Invoke-KrCookieSignOut -OidcScheme 'oidc' -RedirectUri '/'
}
#'@ -Language CSharp
#}

# 9) Start
Start-KrServer -CloseLogsOnExit
