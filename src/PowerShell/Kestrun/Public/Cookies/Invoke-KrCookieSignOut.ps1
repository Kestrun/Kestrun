<#
.SYNOPSIS
    Signs out the current user by removing their authentication cookie for the given scheme.
.DESCRIPTION
    Wraps SignOutAsync on the current HTTP context to remove a cookie-based session.
    Designed for use inside Kestrun route script blocks where $Context is available.
.PARAMETER Scheme
    Authentication scheme to use (default 'Cookies').
.PARAMETER Redirect
    If specified, redirects the user to the login path after signing out.
    If the login path is not configured, redirects to '/'.
.PARAMETER WhatIf
    Shows what would happen if the command runs. The command is not run.
.PARAMETER Confirm
    Prompts you for confirmation before running the command. The command is not run unless you respond
    affirmatively.
.EXAMPLE
    Invoke-KrCookieSignOut  # Signs out the current user from the default 'Cookies' scheme.
.EXAMPLE
    Invoke-KrCookieSignOut -Scheme 'MyCookieScheme'  # Signs out the current user from the specified scheme.
.OUTPUTS
    None
#>
function Invoke-KrCookieSignOut {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low', DefaultParameterSetName = 'SimpleIdentity')]
    [OutputType([System.Security.Claims.ClaimsPrincipal])]
    param(
        [Parameter()]
        [string]$Scheme = 'Cookies',
        [switch]$Redirect
    )
    # Only works inside a route script block where $Context is available
    if ($null -ne $Context) {
        if ($PSCmdlet.ShouldProcess($Scheme, 'SignOut')) {
            # Sign out the user
            if ($Context.User -and $Context.User.Identity.IsAuthenticated) {
                [Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions]::SignOutAsync($Context.HttpContext, $Scheme).GetAwaiter().GetResult() | Out-Null
            }

            if ($Redirect) {
                $cookiesAuth = $null
                if ($KestrunHost.RegisteredAuthentications.Exists($Scheme, "Cookie")) {
                    $cookiesAuth = $KestrunHost.RegisteredAuthentications.Get($Scheme, "Cookie")
                } else {
                    Write-KrLog -Level Warning -Message 'Authentication scheme {scheme} not found in registered authentications.' -Properties $Scheme
                    Write-KrErrorResponse -Message "Authentication scheme '$Scheme' not found." -StatusCode 400
                    return
                }
                Write-KrLog -Level Information -Message 'User {@user} signed out from {scheme} authentication.' -Properties $Context.User, $Scheme
                # Redirect to login path or root

                if ($null -ne $cookiesAuth -and $cookiesAuth.LoginPath -and $cookiesAuth.LoginPath.ToString().Trim()) {
                    $url = $cookiesAuth.LoginPath
                } else {
                    $url = '/'
                }
                Write-KrLog -Level Information -Message 'Redirecting {user} after logout to {path}' -Properties $Context.User, $url
                Write-KrRedirectResponse -Url $url
            }
        }
    } else {
        Write-KrOutsideRouteWarning
    }
}
