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
    [int]$Port = 44300,
    [IPAddress]$IPAddress = [IPAddress]::Loopback,
    [string]$Authority = 'https://demo.duendesoftware.com',
    [string]$ClientId = 'interactive.confidential',
    [string]$ClientSecret = $env:DUENDE_CLIENT_SECRET,  # Set to 'secret' for demo confidential client or via env var
    [string]$CallbackPath = '/signin-oidc',
    [string[]]$Scopes = @('openid', 'profile', 'email')
)

if (-not $ClientSecret) {
    # For the confidential demo client a secret of 'secret' is used. Warn if missing.
    Write-Host 'WARNING: ClientSecret missing; set DUENDE_CLIENT_SECRET or pass -ClientSecret.' -ForegroundColor Yellow
}

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
Add-KrOpenIdConnectAuthentication -Name 'Oidc' -Authority $Authority -ClientId $ClientId -ClientSecret $ClientSecret -Scope $Scopes -CallbackPath $CallbackPath

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
<p>Callback Path: {{callback}}</p>
<p>Scopes: {{scopes}}</p>
'@ -Variables @{ authority = $Authority; callback = $CallbackPath; scopes = ($Scopes -join ', ') }
} -Arguments @{
    authority = $Authority
    CallbackPath = $CallbackPath
    scopes = $Scopes
}

# 7) Protected route group using the policy scheme
Add-KrRouteGroup -Prefix '/oidc' -AuthorizationSchema 'Oidc.Policy' {
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
