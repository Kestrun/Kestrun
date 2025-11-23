<#
.SYNOPSIS
    Adds OAuth 2.0 (Authorization Code) authentication to the Kestrun server.
.DESCRIPTION
    Configures the Kestrun server to use a generic OAuth 2.0 authorization-code flow.
    You can pass a prebuilt OAuthOptions object, or specify individual items (authority, paths, client, etc.).
.PARAMETER Server
    The Kestrun server instance to configure. If not specified, the current server instance is used.
.PARAMETER AuthenticationScheme
    The name of the OAuth authentication scheme (e.g., 'MyOAuth').
.PARAMETER DisplayName
    The display name for the authentication scheme (e.g., 'GitHub Login').
.PARAMETER ClientId
    The OAuth client ID.
.PARAMETER ClientSecret
    The OAuth client secret.
.PARAMETER AuthorizationEndpoint
    The OAuth authorization endpoint URL.
.PARAMETER TokenEndpoint
    The OAuth token endpoint URL.
.PARAMETER CallbackPath
    The callback path for OAuth responses.
.PARAMETER SaveTokens
    If specified, saves the OAuth tokens in the authentication properties.
.PARAMETER ClaimPolicy
    An optional Kestrun.Claims.ClaimPolicyConfig to apply claim policies during authentication.
.PARAMETER Options
    An instance of Kestrun.Authentication.OAuth2Options containing the OAuth configuration.
.PARAMETER PassThru
    If specified, returns the modified Kestrun server object.
.EXAMPLE
    Add-KrOAuth2Authentication -AuthenticationScheme 'MyOAuth' -Options $oauthOptions
    Adds an OAuth2 authentication scheme named 'MyOAuth' using the provided options.
.NOTES
    This is a convenience wrapper around the C# extension AddOAuth2Authentication.
#>
function Add-KrOAuth2Authentication {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $false)]
        [string]$AuthenticationScheme = [Kestrun.Authentication.AuthenticationDefaults]::OAuth2SchemeName,

        [Parameter(Mandatory = $false)]
        [string]$DisplayName = [Kestrun.Authentication.AuthenticationDefaults]::OAuth2DisplayName,
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
        [switch]$SaveTokens,
         [Parameter(Mandatory = $false)]
        [Kestrun.Claims.ClaimPolicyConfig]$ClaimPolicy,
        [Parameter(Mandatory = $false)]
        [Kestrun.Authentication.OAuth2Options]$Options,

        [Parameter(Mandatory = $false)]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($Options -eq $null) {
            # Build options from individual parameters if not provided
            $Options = [Kestrun.Authentication.OAuth2Options]::new()
        }

        if ($ClientId) { $Options.ClientId = $ClientId }
        if ($ClientSecret) { $Options.ClientSecret = $ClientSecret }
        if ($AuthorizationEndpoint) { $Options.AuthorizationEndpoint = $AuthorizationEndpoint }
        if ($TokenEndpoint) { $Options.TokenEndpoint = $TokenEndpoint }
        if ($CallbackPath) { $Options.CallbackPath = $CallbackPath }
        if ($ClaimPolicy) { $Options.ClaimPolicy = $ClaimPolicy }
        $Options.SaveTokens = $SaveTokens.IsPresent

        # Bridge to your C# extension (parallel to AddCookieAuthentication)
        [Kestrun.Hosting.KestrunHostAuthnExtensions]::AddOAuth2Authentication(
            $Server, $AuthenticationScheme, $DisplayName, $Options) | Out-Null
        if ($PassThru.IsPresent) {
            return $Server
        }
    }
}
