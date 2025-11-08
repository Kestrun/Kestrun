<#
    Sample: GitHub Authentication (Authorization Code) + Cookies
    Purpose: Ready-to-use GitHub OAuth2 login using the convenience function.
    Based on: 8.9-Auth2PolicyScheme.ps1, simplified and focused on GitHub.

    Notes:
      - This registers three schemes when using Add-KrGitHubAuthentication -Name 'GitHub':
          1. 'GitHub'          → OAuth challenge (remote login)
          2. 'GitHub.Cookies'  → Cookie sign-in (session persistence)
          3. 'GitHub.Policy'   → Policy/forwarding (authenticate via cookies; challenge via OAuth)
      - PKCE is enabled and tokens are saved to the cookie session.
      - Email claim is optionally enriched from /user/emails when permitted by scope and consent.
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
    [string]$CallbackPath = '/signin-oauth'
)

if (-not $ClientId -or -not $ClientSecret) {
    Write-Host 'ERROR: Set GITHUB_CLIENT_ID and GITHUB_CLIENT_SECRET environment variables before running.' -ForegroundColor Red
    return
}
# This is recommended in order to use relative paths without issues
Initialize-KrRoot -Path $PSScriptRoot

# 1) Logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2) Server
New-KrServer -Name 'GitHub Authentication Demo'

# 3) HTTPS listener (self-signed)
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -SelfSignedCert

# 4) GitHub auth (adds 'GitHub', 'GitHub.Cookies', 'GitHub.Policy')
#    Customize callback if your GitHub App uses a different path (e.g. '/signin-oauth').
#    To disable email enrichment: add -DisableEmailEnrichment
Add-KrGitHubAuthentication -Name 'GitHub' -ClientId $ClientId -ClientSecret $ClientSecret -CallbackPath $CallbackPath

# 5) Finalize configuration
Enable-KrConfiguration

# 6) Landing page
Add-KrHtmlTemplateRoute -Pattern '/' -HtmlTemplatePath './Assets/wwwroot/github-oauth.html'

# 7) Protected routes using the policy scheme
Add-KrRouteGroup -Prefix '/github' -AuthorizationSchema 'GitHub.Policy' {
    Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
        $name = $Context.User.Identity.Name
        if ([string]::IsNullOrWhiteSpace($name)) { $name = '(no name claim)' }
        Write-KrTextResponse "Hello from GitHub auth, $name"
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

# 8) Start
Start-KrServer -CloseLogsOnExit
