<#
    Sample: OAuth 2.0 + Explicit Policy Scheme Forwarding
    Purpose: Demonstrate using the generated <scheme>.Policy scheme name directly for route protection.
    Notes:
      - The Kestrun C# host creates three schemes when you call Add-KrOAuth2Authentication -Name 'GitHub':
          1. 'GitHub'          → The raw OAuth challenge handler
          2. 'GitHub.Cookies'  → The cookie sign-in scheme (session persistence)
          3. 'GitHub.Policy'   → A policy/forwarding scheme: authenticate/sign-in via cookies; challenge via OAuth
      - You can reference 'GitHub.Policy' explicitly if you want to *force* the forwarding behavior.
      - PKCE enabled for better security; tokens saved into the session cookie.
      - Claim mapping demonstrates pulling JSON fields from the userinfo endpoint (GitHub example).
      - Set environment variables first:
          $env:GITHUB_CLIENT_ID = 'your-client-id'
          $env:GITHUB_CLIENT_SECRET = 'your-client-secret'
      - Do NOT hardcode real secrets in sample code.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback,
    [string]$ClientId = $env:GITHUB_CLIENT_ID,
    [string]$ClientSecret = $env:GITHUB_CLIENT_SECRET,
    [string]$Authority = 'https://github.com',
    [string]$AuthorizationPath = '/login/oauth/authorize',
    [string]$TokenPath = '/login/oauth/access_token',
    [string]$CallbackPath = '/signin-oauth',
    # IMPORTANT: GitHub user API is hosted at api.github.com, not github.com
    [string]$UserInformationPath = 'https://api.github.com/user'
)

if (-not $ClientId -or -not $ClientSecret) {
    Write-Host 'ERROR: Set GITHUB_CLIENT_ID and GITHUB_CLIENT_SECRET environment variables before running.' -ForegroundColor Red
    return
}

# 1) Logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2) Server
New-KrServer -Name 'Auth OAuth2 Policy Demo'

# 3) HTTPS listener (self-signed)
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -SelfSignedCert

# 4) GitHub-ready auth (adds 'GitHub', 'GitHub.Cookies', 'GitHub.Policy')
#    Uses PKCE, saves tokens, maps login/avatar, and enriches email when permitted.
Add-KrGitHubAuthentication -Name 'GitHub' -ClientId $ClientId -ClientSecret $ClientSecret

# 5) Finalize configuration
Enable-KrConfiguration

# 6) Public landing page
Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    Write-KrHtmlResponse -Template @'
<!doctype html>
<title>OAuth2 Policy Scheme Demo</title>
<h1>OAuth2 Policy Scheme Demo</h1>
<p><a href="/secure-policy/hello">Login (Policy)</a></p>
<p><a href="/secure-policy/me">Who am I? (Policy)</a></p>
<p><a href="/secure-policy/logout">Logout (Cookies)</a></p>
<p><a href="/raw-oauth/hello">Login (Raw OAuth scheme)</a></p>
'@
}

# 7) Protected routes using the explicit policy scheme name
#    Forwarding: authenticate/sign-in/sign-out via 'GitHub.Cookies', challenge -> 'GitHub'
Add-KrRouteGroup -Prefix '/secure-policy' -AuthorizationSchema 'GitHub.Policy' {
    Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
        Expand-KrObject -InputObject $Context.User
        $name = $Context.User.Identity.Name
        if ([string]::IsNullOrWhiteSpace($name)) { $name = '(no name claim)' }
        Write-KrTextResponse "hello via policy scheme, $name"
    }

    Add-KrMapRoute -Verbs Get -Pattern '/me' -ScriptBlock {
        $claims = foreach ($c in $Context.User.Claims) { @{ Type = $c.Type; Value = $c.Value } }
        Write-KrJsonResponse @{ scheme = 'GitHub.Policy'; authenticated = $Context.User.Identity.IsAuthenticated; claims = $claims }
    }

    Add-KrMapRoute -Verbs Get -Pattern '/logout' -ScriptBlock {
        Invoke-KrCookieSignOut -Scheme 'GitHub.Cookies'
        Write-KrTextResponse 'Signed out (local cookie cleared).'
    }
}

# 8) Alternate group referencing raw OAuth scheme directly (auto forwards too, but less explicit)
Add-KrRouteGroup -Prefix '/raw-oauth' -AuthorizationSchema 'GitHub' {
    Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
        $name = $Context.User?.Identity?.Name
        Write-KrTextResponse "hello via raw oauth forwarding, $name"
    }
}

# 9) Start
Start-KrServer -CloseLogsOnExit
