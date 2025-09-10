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
.PARAMETER Claim
    One or more pre-constructed System.Security.Claims.Claim objects to include.
.PARAMETER ClaimTable
    Hashtable of claimType => value. Adds each as a claim.
.PARAMETER Identity
    Existing ClaimsIdentity to use instead of constructing a new one.
.PARAMETER ExpiresUtc
    Explicit expiration timestamp for the authentication ticket.
.PARAMETER IssuedUtc
    Explicit issued timestamp for the authentication ticket.
.PARAMETER IsPersistent
    Marks the cookie as persistent (survives browser session) if supported.
.PARAMETER AllowRefresh
    Allows the authentication session to be refreshed (sliding expiration scenarios).
.PARAMETER Force
    Signs out any existing principal for the scheme before signing in.
.PARAMETER PassThru
    Returns the created ClaimsPrincipal instead of no output.

.EXAMPLE
    Invoke-KrCookieSignIn -Name 'admin'

.EXAMPLE
    Invoke-KrCookieSignIn -Scheme 'Cookies' -ClaimTable @{ role = 'admin'; dept = 'it' } -IsPersistent -ExpiresUtc (Get-Date).AddMinutes(30)

.OUTPUTS
    System.Security.Claims.ClaimsPrincipal (when -PassThru specified)
#>
function Invoke-KrCookieSignIn {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low', DefaultParameterSetName = 'BuildIdentity')]
    param(
        [Parameter()]
        [string]$Scheme = 'Cookies',

        # Identity construction helpers
        [Parameter(ParameterSetName = 'BuildIdentityArray')]
        [Parameter(ParameterSetName = 'BuildIdentityTable')]
        [string]$Name,
        [Parameter(Mandatory = $true, ParameterSetName = 'BuildIdentityArray')]
        [System.Security.Claims.Claim[]]$Claim,
        [Parameter(Mandatory = $true, ParameterSetName = 'BuildIdentityTable')]
        [hashtable]$ClaimTable,

        [Parameter(ParameterSetName = 'Identity', Mandatory = $true)]
        [System.Security.Claims.ClaimsIdentity]$Identity,

        # Session lifetimes
        [Parameter(ParameterSetName = 'BuildIdentityArray')]
        [Parameter(ParameterSetName = 'BuildIdentityTable')]
        [Parameter(ParameterSetName = 'Identity')]
        [object]$ExpiresUtc,        # accepts DateTimeOffset/DateTime/string/duration

        [Parameter(ParameterSetName = 'BuildIdentityArray')]
        [Parameter(ParameterSetName = 'BuildIdentityTable')]
        [Parameter(ParameterSetName = 'Identity')]
        [object]$IssuedUtc,         # same parsing

        # Flags
        [Parameter(ParameterSetName = 'BuildIdentityArray')]
        [Parameter(ParameterSetName = 'BuildIdentityTable')]
        [Parameter(ParameterSetName = 'Identity')]
        [switch]$IsPersistent,
        [Parameter(ParameterSetName = 'BuildIdentityArray')]
        [Parameter(ParameterSetName = 'BuildIdentityTable')]
        [Parameter(ParameterSetName = 'Identity')]
        [switch]$AllowRefresh,

        # Extras
        [Parameter(ParameterSetName = 'BuildIdentityArray')]
        [Parameter(ParameterSetName = 'BuildIdentityTable')]
        [Parameter(ParameterSetName = 'Identity')]
        [string]$RedirectUri,
        [Parameter(ParameterSetName = 'BuildIdentityArray')]
        [Parameter(ParameterSetName = 'BuildIdentityTable')]
        [Parameter(ParameterSetName = 'Identity')]
        [hashtable]$Items,
        [Parameter(ParameterSetName = 'BuildIdentityArray')]
        [Parameter(ParameterSetName = 'BuildIdentityTable')]
        [Parameter(ParameterSetName = 'Identity')]
        [hashtable]$Parameters,

        [Parameter()]
        [switch]$Force,
        [Parameter()]
        [switch]$PassThru
    )

    if (-not (Get-Variable -Name Context -Scope 1 -ErrorAction SilentlyContinue)) {
        throw 'Invoke-KrCookieSignIn must be called inside a route script block where $Context is available.'
    }

    $httpContext = $Context.HttpContext

    if ($Force -and $httpContext.User -and $httpContext.User.Identity.IsAuthenticated) {
        if ($PSCmdlet.ShouldProcess($Scheme, 'SignOut (force)')) {
            [Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions]::SignOutAsync($httpContext, $Scheme).GetAwaiter().GetResult() | Out-Null
        }
    }

    # Build or accept identity
    if (-not $Identity) {
        $Identity = [System.Security.Claims.ClaimsIdentity]::new($Scheme)
        if ($PSBoundParameters.ContainsKey('Name') -and $Name) {
            $Identity.AddClaim([System.Security.Claims.Claim]::new([System.Security.Claims.ClaimTypes]::Name, [string]$Name))
        }
        if ($Claim) { foreach ($c in $Claim) { $Identity.AddClaim($c) } }
        if ($ClaimTable) {
            foreach ($k in $ClaimTable.Keys) {
                $Identity.AddClaim([System.Security.Claims.Claim]::new([string]$k, [string]$ClaimTable[$k]))
            }
        }
    }

    $principal = [System.Security.Claims.ClaimsPrincipal]::new($Identity)

    # Always create; set only what was provided.
    $authProps = [Microsoft.AspNetCore.Authentication.AuthenticationProperties]::new()

    if ($PSBoundParameters.ContainsKey('ExpiresUtc') -and $ExpiresUtc) {
        $authProps.ExpiresUtc = ConvertTo-DateTimeOffset $ExpiresUtc
    }
    if ($PSBoundParameters.ContainsKey('IssuedUtc') -and $IssuedUtc) {
        $authProps.IssuedUtc = ConvertTo-DateTimeOffset $IssuedUtc
    }

    # Nullable<bool> lets -IsPersistent:$false and -AllowRefresh:$false work
    if ($PSBoundParameters.ContainsKey('IsPersistent')) { $authProps.IsPersistent = [bool]$IsPersistent }
    if ($PSBoundParameters.ContainsKey('AllowRefresh')) { $authProps.AllowRefresh = [bool]$AllowRefresh }

    if ($PSBoundParameters.ContainsKey('RedirectUri') -and $RedirectUri) { $authProps.RedirectUri = $RedirectUri }
    if ($PSBoundParameters.ContainsKey('Items') -and $Items) {
        foreach ($k in $Items.Keys) { $authProps.Items[[string]$k] = [string]$Items[$k] }
    }
    if ($PSBoundParameters.ContainsKey('Parameters') -and $Parameters) {
        foreach ($k in $Parameters.Keys) { $authProps.Parameters[[string]$k] = $Parameters[$k] }
    }

    if ($PSCmdlet.ShouldProcess($Scheme, 'SignIn')) {
        [Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions]::SignInAsync(
            $httpContext, $Scheme, $principal, $authProps
        ).GetAwaiter().GetResult() | Out-Null
    }

    if ($PassThru) { return $principal }
}
