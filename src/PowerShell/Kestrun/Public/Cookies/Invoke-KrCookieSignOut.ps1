<#
.SYNOPSIS
    Signs out the current user by removing their authentication cookie for the given scheme.
.DESCRIPTION
    Wraps SignOutAsync on the current HTTP context to remove a cookie-based session.
    Designed for use inside Kestrun route script blocks where $Context is available.
.PARAMETER Scheme
    Authentication scheme to use (default 'Cookies').
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
        [string]$Scheme = 'Cookies'
    )
    # Only works inside a route script block where $Context is available
    if ($null -ne $Context) {
        if ($PSCmdlet.ShouldProcess($Scheme, 'SignOut')) {
            # Sign out the user
            if ($Context.User -and $Context.User.Identity.IsAuthenticated) {
                [Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions]::SignOutAsync($Context.HttpContext, $Scheme).GetAwaiter().GetResult() | Out-Null
            }
        }
    } else {
        Write-KrOutsideRouteWarning
    }
}
