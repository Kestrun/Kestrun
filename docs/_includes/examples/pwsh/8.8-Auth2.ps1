<#
    Sample: OAuth 2.0 (Authorization Code) + Cookies
    Purpose: External login via GitHub → sign into local Cookies scheme → protect routes.
    File:    8.4-OAuth2.ps1
    Notes:   Use HTTPS. For OAuth redirects, SameSite=Lax is recommended (Strict can block cross-site return).
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback,

    # Put your app creds in env vars for convenience
    [string]$ClientId = $env:GITHUB_CLIENT_ID,
    [string]$ClientSecret = $env:GITHUB_CLIENT_SECRET,

    # GitHub OAuth endpoints
    [string]$Authority = 'https://github.com',
    [string]$AuthorizationPath = '/login/oauth/authorize',
    [string]$TokenPath = '/login/oauth/access_token',
    [string]$CallbackPath = '/signin-oauth'
)
$ClientId = 'Ov23liPwNax68FU5v09g'
$ClientSecret = '36d1ecab4a9e646d56173512ceca6420dfcf0238'
# 1) Logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2) Server
New-KrServer -Name 'Auth OAuth2 + Cookies'

# 3) Listener (self-signed for local https)
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -SelfSignedCert

# 4) Cookie scheme (SESSION) — name MUST be 'Cookies' to match protection below
#    Important: SameSite=Lax is friendlier for OAuth return redirects.
#New-KrCookieBuilder -Name 'KestrunAuth' -HttpOnly -SecurePolicy Always -SameSite Lax |
#   Add-KrCookiesAuthentication -Name 'Cookies' `
##     -ExpireTimeSpan (New-TimeSpan -Minutes 30)


# 5) OAuth2 scheme (AUTH CHALLENGE) — signs into the 'Cookies' scheme above
Add-KrOAuth2Authentication -Name 'GitHub' `
    -Authority $Authority `
    -AuthorizationPath $AuthorizationPath `
    -TokenPath $TokenPath `
    -CallbackPath $CallbackPath `
    -ClientId $ClientId `
    -ClientSecret $ClientSecret `
    -Scope 'read:user', 'user:email'
#-CookieScheme 'Cookie8s'

# 6) Finalize configuration
Enable-KrConfiguration

# 7) Public landing
Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    Write-KrHtmlResponse -Template @'
<!doctype html>
<title>OAuth2 + Cookies</title>
<h1>OAuth2 + Cookies</h1>
<p><a href="/secure/oauth/hello">Login with GitHub</a></p>
<p><a href="/secure/oauth/me">Who am I?</a></p>
<p><a href="/secure/oauth/logout">Logout</a></p>
'@
}

# 8) Protected routes (challenge via 'GitHub', session via 'Cookies')
Add-KrRouteGroup -Prefix '/secure/oauth' -AuthorizationSchema 'GitHub' {
    Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
        $name = $Context.User?.Identity?.Name
        if ([string]::IsNullOrWhiteSpace($name)) { $name = '(no name claim)' }
        Write-KrTextResponse "hello, $name — you’re suavely authenticated via GitHub → Cookies."
    }

    Add-KrMapRoute -Verbs Get -Pattern '/me' -ScriptBlock {
        Write-KrLog -Level Information -Message "'/secure/oauth/me accessed by user: $($Context.User?.Identity?.Name)'"
        $claims = @()
        foreach ($c in $Context.User.Claims) {
            $claims += @{ Type = $c.Type; Value = $c.Value }
        }
        Write-KrJsonResponse @{ authenticated = $Context.User.Identity.IsAuthenticated; claims = $claims }
    }

    # sign-out clears the session cookie (no remote sign-out for plain OAuth2)
    Add-KrMapRoute -Verbs Get -Pattern '/logout' -ScriptBlock {
        Invoke-KrCookieSignOut -Scheme 'Cookies' -Redirect -RedirectUri '/'
    }
}

# 9) Start
Start-KrServer -CloseLogsOnExit
