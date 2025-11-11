<#
.SYNOPSIS
    Challenges the current request to authenticate with the specified authentication scheme.
.DESCRIPTION
    Wraps ChallengeAsync on the current HTTP context to trigger an authentication challenge.
    This is typically used to redirect users to an external identity provider (e.g., OIDC, OAuth2).
    Designed for use inside Kestrun route script blocks where $Context is available.
.PARAMETER Scheme
    Authentication scheme to challenge (e.g., 'oidc', 'Google', 'AzureAD').
.PARAMETER RedirectUri
    URI to redirect to after successful authentication (default is current request path).
.PARAMETER Properties
    Additional authentication properties to pass to the challenge.
.PARAMETER WhatIf
    Shows what would happen if the command runs. The command is not run.
.PARAMETER Confirm
    Prompts you for confirmation before running the command.
.EXAMPLE
    Invoke-KrChallenge -Scheme 'oidc' -RedirectUri '/dashboard'

    Challenges the user to authenticate with OIDC, redirecting to /dashboard after login.
.EXAMPLE
    Invoke-KrChallenge -Scheme 'Google'

    Challenges the user to authenticate with Google OAuth, redirecting back to the current page.
.EXAMPLE
    $props = @{
        prompt = 'login'
        login_hint = 'user@example.com'
    }
    Invoke-KrChallenge -Scheme 'oidc' -RedirectUri '/hello' -Properties $props

    Challenges with additional properties (forces login prompt and hints the username).
.OUTPUTS
    None. This function initiates an authentication challenge and does not return a value.
.NOTES
    This function must be called from within a route handler where $Context is available.
    After calling this function, the route should return immediately to allow the authentication
    middleware to complete the redirect.
#>
function Invoke-KrChallenge {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
    [OutputType([void])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Scheme,

        [Parameter()]
        [string]$RedirectUri,

        [Parameter()]
        [hashtable]$Properties
    )

    # Only works inside a route script block where $Context is available
    if ($null -eq $Context -or $null -eq $KrServer) {
        Write-KrOutsideRouteWarning
        return
    }

    if ($PSCmdlet.ShouldProcess($Scheme, 'Challenge')) {
        Write-KrLog -Level Information -Message 'Initiating authentication challenge for scheme {scheme}' -Values $Scheme

        # Create AuthenticationProperties
        $authProperties = [Microsoft.AspNetCore.Authentication.AuthenticationProperties]::new()

        # Set redirect URI
        if ($RedirectUri) {
            $authProperties.RedirectUri = $RedirectUri
            Write-KrLog -Level Debug -Message 'Challenge redirect URI set to {uri}' -Values $RedirectUri
        }

        # Add any additional properties from hashtable
        if ($Properties) {
            foreach ($key in $Properties.Keys) {
                $value = $Properties[$key]
                if ($null -ne $value) {
                    $authProperties.Items[$key] = $value.ToString()
                    Write-KrLog -Level Debug -Message 'Added challenge property: {key}={value}' -Values $key, $value
                }
            }
        }

        # Call ChallengeAsync using the ASP.NET Core authentication extensions
        $Context.Challenge($Scheme, $authProperties)
        Write-KrLog -Level Information -Message 'Authentication challenge initiated for scheme {scheme}' -Values $Scheme

        # CRITICAL: Send a 302 status to prevent Kestrun from sending its own 200 OK response
        # The authentication handler has already set up the redirect Location header
        Write-KrStatusResponse -StatusCode 302
    }
}
