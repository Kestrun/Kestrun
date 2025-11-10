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
    Write-Host "         Switching to 'interactive.public' (no secret, PKCE) for this run." -ForegroundColor Yellow
    $ClientId = 'interactive.public'
    $ClientId = 'interactive.confidential'
    $ClientSecret = 'secret'
}

Initialize-KrRoot -Path $PSScriptRoot
# 1) Logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null


if (Test-Path 'devcert.pfx' ) {
    $cert = Import-KrCertificate -FilePath 'devcert.pfx' -Password (ConvertTo-SecureString -String 'p@ss' -AsPlainText -Force)
} else {
    $cert = New-KrSelfSignedCertificate -DnsNames 'localhost' -Exportable
    Export-KrCertificate -Certificate $cert `
        -FilePath 'devcert' -Format pfx -IncludePrivateKey -Password (ConvertTo-SecureString -String 'p@ss' -AsPlainText -Force)
}

if (-not (Test-KrCertificate -Certificate $cert )) {
    Write-Error 'Certificate validation failed. Ensure the certificate is valid and not self-signed.'
    exit 1
}


# 2) Server
New-KrServer -Name 'OIDC Duende Demo'

# 3) HTTPS endpoint
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -X509Certificate $cert

# 4) OpenID Connect auth (adds 'Oidc', 'Oidc.Cookies', 'Oidc.Policy')
$oidcParams = @{
    Name = 'oidc'
    Authority = $Authority
    ClientId = $ClientId
    Scope = @('openid', 'profile', 'email', 'api', 'offline_access')  # default scopes
}
if ($ClientSecret) {
    $oidcParams.ClientSecret = $ClientSecret
}
if ($Scopes -and $Scopes.Count -gt 0) {
    $oidcParams.Scope += $Scopes
}
#Add-KrHttpsRedirection

Add-KrOpenIdConnectAuthentication @oidcParams


# 5) Finalize pipeline
Enable-KrConfiguration

# 6) Landing page
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
Context.Challenge("oidc", new Dictionary<string, string?> {
    { "RedirectUri", "/hello" }
});
'@ -AllowAnonymous -Language CSharp

Add-KrMapRoute -Verbs Get -Pattern '/login' -ScriptBlock {
    <#   $dic = [System.Collections.Generic.Dictionary[string, string]]::new()
    $dic.Add('RedirectUri', '/hello')
    $props = [Microsoft.AspNetCore.Authentication.AuthenticationProperties]::new( $dic);
    $Context.HttpContext.ChallengeAsync('Oidc', $props) | Out-Null#>
    $task = $Context.Challenge('oidc', @{ 'RedirectUri' = '/hello' })
    Write-KrRedirectResponse -Url '/hello'
} -AllowAnonymous

# 7) Protected route group using the policy scheme
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

Add-KrMapRoute -Verbs Get -Pattern '/logout' -AuthorizationSchema 'oidc' -ScriptBlock {
    $context.SignOut('Cookies')
    $context.SignOut('oidc', @{ 'RedirectUri' = '/' })

    #Invoke-KrCookieSignOut -Scheme 'oidc'
    Write-KrTextResponse 'Signed out (local cookie cleared).'
}
#}

# 8) Start
Start-KrServer -CloseLogsOnExit
