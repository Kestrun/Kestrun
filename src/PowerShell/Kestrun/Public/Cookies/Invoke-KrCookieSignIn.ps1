<#
.SYNOPSIS
    Signs in a user issuing an authentication cookie for the given scheme.
.DESCRIPTION
    Wraps SignInAsync on the current HTTP context to create a cookie-based session.
    You can supply an existing ClaimsIdentity or provide claims via -Name, -Claim, or -ClaimTable.
    Optionally sets authentication properties like persistence and expiration.
    Designed for use inside Kestrun route script blocks where $Context is available.
.PARAMETER Scheme
    Authentication scheme to use (default 'Cookies').
.PARAMETER Name
    Convenience parameter to add a ClaimTypes.Name claim.
.PARAMETER Claims
    One or more pre-constructed System.Security.Claims.Claim objects to include.
.PARAMETER Identity
    Existing ClaimsIdentity to use instead of constructing a new one.
.PARAMETER AuthenticationProperties
    Existing AuthenticationProperties to use instead of constructing a new one.
.PARAMETER ExpiresUtc
    Explicit expiration timestamp for the authentication ticket.
.PARAMETER IssuedUtc
    Explicit issued timestamp for the authentication ticket.
.PARAMETER IsPersistent
    Marks the cookie as persistent (survives browser session) if supported.
.PARAMETER AllowRefresh
    Allows the authentication session to be refreshed (sliding expiration scenarios).
.PARAMETER RedirectUri
    Sets the RedirectUri property on AuthenticationProperties.
.PARAMETER Items
    Hashtable of string key-value pairs to add to the Items collection on AuthenticationProperties.
.PARAMETER Parameters
    Hashtable of string key-value pairs to add to the Parameters collection on AuthenticationProperties.
.PARAMETER PassThru
    Returns the created ClaimsPrincipal instead of no output.
.PARAMETER WhatIf
    Shows what would happen if the command runs. The command is not run.
.PARAMETER Confirm
    Prompts you for confirmation before running the command. The command is not run unless you respond affirmatively.
.EXAMPLE
    Invoke-KrCookieSignIn -Name 'admin'
.EXAMPLE
    Invoke-KrCookieSignIn -Scheme 'Cookies' -ClaimTable @{ role = 'admin'; dept = 'it' } -IsPersistent -ExpiresUtc (Get-Date).AddMinutes(30)
.OUTPUTS
    System.Security.Claims.ClaimsPrincipal (when -PassThru specified)
#>
function Invoke-KrCookieSignIn {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low', DefaultParameterSetName = 'Claims')]
    [OutputType([System.Security.Claims.ClaimsPrincipal])]
    param(
        [Parameter()]
        [string]$Scheme = 'Cookies',

        # Identity construction helpers
        [Parameter(ParameterSetName = 'SimpleIdentity')]
        [string]$Name,

        [Parameter(ParameterSetName = 'BuildIdentity')]
        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_BuildIdentity')]
        [Parameter(ParameterSetName = 'AuthenticationProperties_Claim')]
        [Parameter(ParameterSetName = 'Claims')]
        [System.Security.Claims.Claim[]]$Claims,


        [Parameter(ParameterSetName = 'Identity', Mandatory = $true)]
        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_Identity')]
        [Parameter(ParameterSetName = 'AuthenticationProperties_Identity')]
        [System.Security.Claims.ClaimsIdentity]$Identity,

        [Parameter(Mandatory = $true, ParameterSetName = 'AuthenticationProperties_Claim')]
        [Parameter(ParameterSetName = 'AuthenticationProperties_Identity')]
        [Microsoft.AspNetCore.Authentication.AuthenticationProperties]$AuthenticationProperties,
        # Session lifetimes
        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_BuildIdentity')]
        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_Identity')]
        [Parameter(ParameterSetName = 'SimpleIdentity_BuildIdentity')]
        [object]$ExpiresUtc,        # accepts DateTimeOffset/DateTime/string/duration

        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_BuildIdentity')]
        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_Identity')]
        [Parameter(ParameterSetName = 'SimpleIdentity_BuildIdentity')]
        [object]$IssuedUtc,         # same parsing

        # Flags
        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_BuildIdentity')]
        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_Identity')]
        [Parameter(ParameterSetName = 'SimpleIdentity_BuildIdentity')]
        [switch]$IsPersistent,
        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_BuildIdentity')]
        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_Identity')]
        [Parameter(ParameterSetName = 'SimpleIdentity_BuildIdentity')]
        [switch]$AllowRefresh,

        # Extras
        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_BuildIdentity')]
        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_Identity')]
        [Parameter(ParameterSetName = 'SimpleIdentity_BuildIdentity')]
        [string]$RedirectUri,
        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_BuildIdentity')]
        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_Identity')]
        [Parameter(ParameterSetName = 'SimpleIdentity_BuildIdentity')]
        [hashtable]$Items,
        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_BuildIdentity')]
        [Parameter(ParameterSetName = 'AuthenticationPropertiesItems_Identity')]
        [Parameter(ParameterSetName = 'SimpleIdentity_BuildIdentity')]
        [hashtable]$Parameters,

        [Parameter()]
        [switch]$PassThru
    )
    # Only works inside a route script block where $Context is available
    if ($null -ne $Context) {

        # Build or accept identity
        if (-not $Identity) {
            $Identity = [System.Security.Claims.ClaimsIdentity]::new($Scheme)
        }

        #   Add Name claim if provided
        if ($PSBoundParameters.ContainsKey('Name') -and $Name) {
            $Identity.AddClaim([System.Security.Claims.Claim]::new([System.Security.Claims.ClaimTypes]::Name, [string]$Name))
        }

        # Add any provided claims
        if ($Claims) {
            foreach ($claim in $Claims) {
                $Identity.AddClaim($claim)
            }
        }

        # Create principal
        $principal = [System.Security.Claims.ClaimsPrincipal]::new($Identity)

        if ($PSBoundParameters -eq 'AuthenticationPropertiesItems_BuildIdentity') {
            $AuthenticationProperties = [Microsoft.AspNetCore.Authentication.AuthenticationProperties]::new()

            if ($PSBoundParameters.ContainsKey('ExpiresUtc') -and $ExpiresUtc) {
                $authProps.ExpiresUtc = ConvertTo-DateTimeOffset $ExpiresUtc
            }
            if ($PSBoundParameters.ContainsKey('IssuedUtc') -and $IssuedUtc) {
                $authProps.IssuedUtc = ConvertTo-DateTimeOffset $IssuedUtc
            }
            if ($PSBoundParameters.ContainsKey('IsPersistent')) {
                $authProps.IsPersistent = [bool]$IsPersistent
            }
            if ($PSBoundParameters.ContainsKey('AllowRefresh')) {
                $authProps.AllowRefresh = [bool]$AllowRefresh
            }
            if ($PSBoundParameters.ContainsKey('RedirectUri') -and $RedirectUri) {
                $authProps.RedirectUri = $RedirectUri
            }
            if ($PSBoundParameters.ContainsKey('Items') -and $Items) {
                foreach ($k in $Items.Keys) { $authProps.Items[[string]$k] = [string]$Items[$k] }
            }
            if ($PSBoundParameters.ContainsKey('Parameters') -and $Parameters) {
                foreach ($k in $Parameters.Keys) { $authProps.Parameters[[string]$k] = $Parameters[$k] }
            }
        }

        # Sign in
        if ($PSCmdlet.ShouldProcess($Scheme, 'SignIn')) {
            [Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions]::SignInAsync(
                $Context.HttpContext, $Scheme, $principal, $AuthenticationProperties
            ).GetAwaiter().GetResult() | Out-Null
        }

        # Return principal if requested
        if ($PassThru) {
            return $principal
        }
    } else {
        Write-KrOutsideRouteWarning
    }
}
