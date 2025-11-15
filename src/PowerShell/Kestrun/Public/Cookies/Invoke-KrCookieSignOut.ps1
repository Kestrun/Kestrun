<#
.SYNOPSIS
    Signs out the current user by removing their authentication cookie for the given scheme.
.DESCRIPTION
    Wraps SignOutAsync on the current HTTP context to remove a cookie-based session.
    Designed for use inside Kestrun route script blocks where $Context is available.

    For OIDC logout, use -OidcScheme to sign out from both the cookie and OIDC provider.
    This will redirect to the OIDC provider's logout endpoint automatically.
.PARAMETER Scheme
    Authentication scheme to use (default 'Cookies').
.PARAMETER AuthKind
    Authentication kind: 'Cookies' (default), 'OAuth2', or 'Oidc'.
    Use 'OAuth2' to sign out from both Cookies and OAuth2 schemes.
    Use 'Oidc' to sign out from both Cookies and OIDC schemes (triggers redirect to IdP logout).
.PARAMETER Redirect
    If specified, redirects the user to the login path after signing out.
    If the login path is not configured, redirects to '/'.
    NOTE: This is ignored when OidcScheme is used, as the OIDC handler manages the redirect.
.PARAMETER RedirectUri
    URI to redirect to after OIDC logout completes (default '/').
    Only used when OidcScheme is specified.
.PARAMETER Properties
    Additional sign-out authentication properties to pass to the SignOut call.
.PARAMETER WhatIf
    Shows what would happen if the command runs. The command is not run.
.PARAMETER Confirm
    Prompts you for confirmation before running the command. The command is not run unless you respond
    affirmatively.
.EXAMPLE
    Invoke-KrCookieSignOut  # Signs out the current user from the default 'Cookies' scheme.
.EXAMPLE
    Invoke-KrCookieSignOut -Scheme 'MyCookieScheme'  # Signs out the current user from the specified scheme.
.EXAMPLE
    Invoke-KrCookieSignOut -OidcScheme 'oidc' -RedirectUri '/'  # Signs out from both Cookies and OIDC, redirects to root after OIDC logout.
.OUTPUTS
    None
#>
function Invoke-KrCookieSignOut {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low', DefaultParameterSetName = 'SimpleIdentity')]
    [OutputType([void])]
    param(
        [Parameter()]
        [string]$Scheme = 'Cookies',

        [Parameter()]
        [ValidateSet('OAuth2', 'Oidc', 'Cookies')]
        [string]$AuthKind = 'Cookies',

        [switch]$Redirect,

        [Parameter()]
        [string]$RedirectUri = '/',

        [hashtable]$Properties
    )
    # Only works inside a route script block where $Context is available
    if ($null -ne $Context -and $null -ne $KrServer) {
        if ($PSCmdlet.ShouldProcess($Scheme, 'SignOut')) {

            switch ($AuthKind) {
                'OAuth2' {
                    # OAuth2 logout requires special handling
                    Write-KrLog -Level Information -Message 'Signing out from Cookie and OAuth2 ({oauth2Scheme}) schemes' -Values $Scheme
                    $cookieSchemeName = $KrServer.RegisteredAuthentications.ResolveAuthenticationSchemeName($Scheme, $AuthKind)
                    Write-KrLog -Level Debug -Message 'Resolved Cookie scheme name: {scheme}' -Values $cookieSchemeName

                    # Sign out from Cookie
                    $oidcProperties = [Microsoft.AspNetCore.Authentication.AuthenticationProperties]::new()
                    if (-not [string]::IsNullOrEmpty($RedirectUri)  ) {
                        $oidcProperties.RedirectUri = $RedirectUri
                    }
                    $Context.SignOut($cookieSchemeName, $oidcProperties) | Out-Null
                   Write-KrStatusResponse -StatusCode 302
                    Write-KrLog -Level Information -Message 'OAuth2 logout initiated, OAuth2 handler will redirect to IdP logout endpoint'
                    return
                }
                'Oidc' {

                    # OIDC logout requires special handling
                    Write-KrLog -Level Information -Message 'Signing out from Cookie ({cookieScheme}) and OIDC ({oidcScheme}) schemes' -Values $Scheme, $AuthKind
                    $schemeName = $KrServer.RegisteredAuthentications.ResolveAuthenticationSchemeName($Scheme, $AuthKind )
                    Write-KrLog -Level Debug -Message 'Resolved OIDC scheme name: {scheme}' -Values $schemeName

                    $Context.SignOut($schemeName) | Out-Null
                    # Then sign out from OIDC scheme (triggers redirect to IdP logout)
                    $oidcProperties = [Microsoft.AspNetCore.Authentication.AuthenticationProperties]::new()
                    if (-not [string]::IsNullOrEmpty($RedirectUri)  ) {
                        $oidcProperties.RedirectUri = $RedirectUri
                    }
                    $Context.SignOut($Scheme, $oidcProperties) | Out-Null
                 #   Write-KrStatusResponse -StatusCode 302
                    Write-KrLog -Level Information -Message 'OIDC logout initiated, OIDC handler will redirect to IdP logout endpoint'
                    return
                }
                'Cookies' {
                    Write-KrLog -Level Information -Message 'Signing out from Cookie scheme: {scheme}' -Values $Scheme

                    # Standard cookie-only logout
                    if ($Context.User -and $Context.User.Identity.IsAuthenticated) {
                        $Context.SignOut($Scheme, $Properties)
                    }

                    if ($Redirect) {
                        $cookiesAuth = $null
                        if ($KrServer.RegisteredAuthentications.Exists($Scheme, 'Cookie')) {
                            $cookiesAuth = $KrServer.RegisteredAuthentications.Get($Scheme, 'Cookie')
                        } else {
                            Write-KrLog -Level Warning -Message 'Authentication scheme {scheme} not found in registered authentications.' -Values $Scheme
                            Write-KrErrorResponse -Message "Authentication scheme '$Scheme' not found." -StatusCode 400
                            return
                        }
                        Write-KrLog -Level Information -Message 'User {@user} signed out from {scheme} authentication.' -Values $Context.User, $Scheme
                        # Redirect to login path or root

                        if ($null -ne $cookiesAuth -and $cookiesAuth.LoginPath -and $cookiesAuth.LoginPath.ToString().Trim()) {
                            $url = $cookiesAuth.LoginPath
                        } else {
                            $url = '/'
                        }
                        Write-KrLog -Level Information -Message 'Redirecting {user} after logout to {path}' -Values $Context.User, $url
                        Write-KrRedirectResponse -Url $url
                    }
                }
            }
        }
    } else {
        Write-KrOutsideRouteWarning
    }
}
