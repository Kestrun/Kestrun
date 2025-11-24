<#
.SYNOPSIS
    Adds OpenID Connect (Authorization Code) authentication to the Kestrun server.
.DESCRIPTION
    Convenience wrapper around the C# extension AddOpenIdConnectAuthentication. Registers three schemes:
      <Name>, <Name>.Cookies, <Name>.Policy
    Enables PKCE and token persistence by default; supports custom scopes and callback path.
.PARAMETER Server
    The Kestrun server instance. If omitted, uses the current active server.
.PARAMETER AuthenticationScheme
    Base scheme name (default 'Oidc').
.PARAMETER DisplayName
    The display name for the authentication scheme (default is the OpenID Connect default display name).
.PARAMETER Description
    A description of the OpenID Connect authentication scheme.
.PARAMETER Authority
    The OpenID Connect authority URL.
.PARAMETER ClientId
    The OpenID Connect client ID.
.PARAMETER ClientSecret
    The OpenID Connect client secret.
.PARAMETER AuthorizationEndpoint
    The OpenID Connect authorization endpoint URL.
.PARAMETER TokenEndpoint
    The OpenID Connect token endpoint URL.
.PARAMETER ResponseType
    The OpenID Connect response type (default is 'Code').
.PARAMETER CallbackPath
    The callback path for OpenID Connect responses.
.PARAMETER SignedOutCallbackPath
    The callback path for sign-out responses.
.PARAMETER SaveTokens
    If specified, saves the OpenID Connect tokens in the authentication properties.
.PARAMETER UsePkce
    If specified, enables Proof Key for Code Exchange (PKCE) for enhanced security.
.PARAMETER GetClaimsFromUserInfoEndpoint
    If specified, retrieves additional claims from the UserInfo endpoint.
.PARAMETER ClaimPolicy
    An optional Kestrun.Claims.ClaimPolicyConfig to apply claim policies during authentication.
.PARAMETER Options
    An instance of Kestrun.Authentication.OidcOptions containing the OIDC configuration.
.PARAMETER PassThru
    Return the modified server object.
.EXAMPLE
    Add-KrOpenIdConnectAuthentication -Authority 'https://example.com' -ClientId $id -ClientSecret $secret
.EXAMPLE
    Add-KrOpenIdConnectAuthentication -AuthenticationScheme 'AzureAD' -Authority $authority -ClientId $id -ClientSecret $secret -Scope 'email' -CallbackPath '/signin-oidc'
#>
function Add-KrOpenIdConnectAuthentication {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $false)]
        [string]$AuthenticationScheme = [Kestrun.Authentication.AuthenticationDefaults]::OidcSchemeName,

        [Parameter(Mandatory = $false)]
        [string]$DisplayName = [Kestrun.Authentication.AuthenticationDefaults]::OidcDisplayName,

        [Parameter(Mandatory = $false)]
        [string]$Description,

        [Parameter(Mandatory = $false)]
        [string]$Authority,

        [Parameter(Mandatory = $false)]
        [string]$ClientId,

        [Parameter(Mandatory = $false)]
        [string]$ClientSecret,

        [Parameter(Mandatory = $false)]
        [string]$AuthorizationEndpoint,

        [Parameter(Mandatory = $false)]
        [string]$TokenEndpoint,

        [Parameter(Mandatory = $false)]
        [string]$CallbackPath,

        [Parameter(Mandatory = $false)]
        [string]$SignedOutCallbackPath,

        [Parameter(Mandatory = $false)]
        [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseType]$ResponseType,

        [Parameter(Mandatory = $false)]
        [switch]$SaveTokens,

        [Parameter(Mandatory = $false)]
        [switch]$UsePkce,

        [Parameter(Mandatory = $false)]
        [switch]$GetClaimsFromUserInfoEndpoint,

        [Parameter(Mandatory = $false)]
        [Kestrun.Claims.ClaimPolicyConfig]$ClaimPolicy,

        [Parameter(Mandatory = $false)]
        [Kestrun.Authentication.OidcOptions]$Options,

        [Parameter(Mandatory = $false)]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ( $null -eq $Options ) {
            # Build options from individual parameters if not provided
            $Options = [Kestrun.Authentication.OidcOptions]::new()
        }
        if ($Authority) { $Options.Authority = $Authority }
        if ($ClientId) { $Options.ClientId = $ClientId }
        if ($ClientSecret) { $Options.ClientSecret = $ClientSecret }
        if ($AuthorizationEndpoint) { $Options.AuthorizationEndpoint = $AuthorizationEndpoint }
        if ($TokenEndpoint) { $Options.TokenEndpoint = $TokenEndpoint }
        if ($CallbackPath) { $Options.CallbackPath = $CallbackPath }
        if ($SignedOutCallbackPath) { $Options.SignedOutCallbackPath = $SignedOutCallbackPath }
        if ($ClaimPolicy) { $Options.ClaimPolicy = $ClaimPolicy }
        if ($ResponseType) { $Options.ResponseType = $ResponseType }
        if ($Description) { $Options.Description = $Description }
        $Options.SaveTokens = $SaveTokens.IsPresent
        $Options.UsePkce = $UsePkce.IsPresent
        $Options.GetClaimsFromUserInfoEndpoint = $GetClaimsFromUserInfoEndpoint.IsPresent
        # Call C# extension with optional claim policy
        [Kestrun.Hosting.KestrunHostAuthnExtensions]::AddOpenIdConnectAuthentication(
            $Server,
            $AuthenticationScheme,
            $DisplayName,
            $Options
        ) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}
