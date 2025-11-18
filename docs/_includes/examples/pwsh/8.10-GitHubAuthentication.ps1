<#
    Sample: GitHub Authentication (Authorization Code) + Cookies
    Purpose: Ready-to-use GitHub OAuth2 login using the convenience function.
    Based on: 8.10-GitHubAuthentication.ps1, simplified and focused on GitHub.

    Notes:
      - This registers three schemes when using Add-KrGitHubAuthentication -Name 'GitHub':
          1. 'GitHub'          → OAuth challenge (remote login) scheme
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

if (([string]::IsNullOrWhiteSpace( $ClientId)) -and
    ([string]::IsNullOrWhiteSpace( $ClientSecret)) -and
    (Test-Path -Path .\Utility\Import-EnvFile.ps1)) {
    & .\Utility\Import-EnvFile.ps1
    $ClientId = $env:GITHUB_CLIENT_ID
    $ClientSecret = $env:GITHUB_CLIENT_SECRET
}

if (-not $ClientId -or -not $ClientSecret) {
    Write-Error 'ERROR: Set GITHUB_CLIENT_ID and GITHUB_CLIENT_SECRET environment variables before running.'
    exit 1
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
Add-KrHtmlTemplateRoute -Pattern '/' -HtmlTemplatePath './Assets/wwwroot/github/github-auth.html'

# 7) Protected routes using the policy scheme

Add-KrMapRoute -Verbs Get -Pattern '/github/login' -AuthorizationSchema 'GitHub' -ScriptBlock {
    # Extract user claims (robust to GitHub-specific claim types)
    $name = $Context.User.Identity.Name ?? '(no name)'
    $email = $Context.User.FindFirst([System.Security.Claims.ClaimTypes]::Email)?.Value `
        ?? $Context.User.FindFirst('email')?.Value `
        ?? 'No email claim'
    $username = $Context.User.FindFirst('urn:github:login')?.Value `
        ?? $Context.User.FindFirst('login')?.Value `
        ?? $Context.User.FindFirst('preferred_username')?.Value `
        ?? $name
    if ([string]::IsNullOrWhiteSpace($username)) { $username = 'N/A' }

    $githubId = $Context.User.FindFirst([System.Security.Claims.ClaimTypes]::NameIdentifier)?.Value `
        ?? $Context.User.FindFirst('id')?.Value `
        ?? $Context.User.FindFirst('sub')?.Value `
        ?? 'N/A'

    $profileUrl = $Context.User.FindFirst('urn:github:html_url')?.Value `
        ?? $Context.User.FindFirst('profile')?.Value `
        ?? $Context.User.FindFirst('url')?.Value `
        ?? "https://github.com/$username"

    # Get authentication details
    $authType = $Context.User.Identity.AuthenticationType ?? 'Unknown'
    $isAuthenticated = $Context.User.Identity.IsAuthenticated

    Write-KrHtmlResponse -FilePath './Assets/wwwroot/github/protected.html' -Variables @{
        name = $name
        email = $email
        username = $username
        githubId = $githubId
        profileUrl = $profileUrl
        authType = $authType
        isAuthenticated = $isAuthenticated
    }
}

Add-KrMapRoute -Verbs Get -Pattern '/github/me' -AuthorizationSchema 'GitHub' -ScriptBlock {
    $claims = foreach ($c in $Context.User.Claims) { @{ Type = $c.Type; Value = $c.Value } }
    Write-KrJsonResponse @{ scheme = 'GitHub'; authenticated = $Context.User.Identity.IsAuthenticated; claims = $claims }
}

Add-KrMapRoute -Verbs Get -Pattern '/github/logout' -ScriptBlock {
    Invoke-KrCookieSignOut -AuthKind 'OAuth2' -Scheme 'GitHub' -RedirectUri '/'
    #  Write-KrTextResponse 'Signed out (local cookie cleared).'
}


# 8) Start
Start-KrServer -CloseLogsOnExit
