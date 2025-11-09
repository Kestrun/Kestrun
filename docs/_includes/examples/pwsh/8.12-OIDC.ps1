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
}

# Decide PKCE usage: public client requires PKCE; confidential client (with secret) disables PKCE for Duende demo
$usePkce = [string]::IsNullOrWhiteSpace($ClientSecret)

# 1) Logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2) Server
New-KrServer -Name 'OIDC Duende Demo'

# 3) HTTPS endpoint
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -SelfSignedCert

# 4) OpenID Connect auth (adds 'Oidc', 'Oidc.Cookies', 'Oidc.Policy')
$oidcParams = @{
    Name = 'Oidc'
    Authority = $Authority
    ClientId = $ClientId
    Scope = @()
}
if ($ClientSecret) {
    $oidcParams.ClientSecret = $ClientSecret
}
if ($Scopes -and $Scopes.Count -gt 0) {
    $oidcParams.Scope += $Scopes
}
Add-KrOpenIdConnectAuthentication @oidcParams

# 5) Finalize pipeline
Enable-KrConfiguration

# 6) Landing page
Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    Write-KrHtmlResponse -Template @'
<!doctype html>
<title>OIDC Duende Demo</title>
<h1>OIDC Duende Demo</h1>
<p><a href="/oidc/hello">Login with OIDC (Policy)</a></p>
<p><a href="/oidc/me">Who am I?</a></p>
<p><a href="/oidc/logout">Logout</a></p>
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

# 7) Protected route group using the policy scheme
Add-KrRouteGroup -Prefix '/oidc' -AuthorizationSchema 'Oidc' {
    Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
        $name = $Context.User.Identity.Name
        if ([string]::IsNullOrWhiteSpace($name)) { $name = '(no name claim)' }
        Write-KrTextResponse "Hello from OIDC, $name"
    }

    Add-KrMapRoute -Verbs Get -Pattern '/me' -ScriptBlock {
        $claims = foreach ($c in $Context.User.Claims) { @{ Type = $c.Type; Value = $c.Value } }
        Write-KrJsonResponse @{ scheme = 'Oidc.Policy'; authenticated = $Context.User.Identity.IsAuthenticated; claims = $claims }
    }

    Add-KrMapRoute -Verbs Get -Pattern '/logout' -ScriptBlock {
        Invoke-KrCookieSignOut -Scheme 'Oidc.Cookies'
        Write-KrTextResponse 'Signed out (local cookie cleared).'
    }
}

# 8) Start
Start-KrServer -CloseLogsOnExit
