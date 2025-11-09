<#
.SYNOPSIS
    Adds OpenID Connect (Authorization Code) authentication to the Kestrun server.
.DESCRIPTION
    Convenience wrapper around the C# extension AddOpenIdConnectAuthentication. Registers three schemes:
      <Name>, <Name>.Cookies, <Name>.Policy
    Enables PKCE and token persistence by default; supports custom scopes and callback path.
.PARAMETER Server
    The Kestrun server instance. If omitted, uses the current active server.
.PARAMETER Name
    Base scheme name (default 'Oidc').
.PARAMETER Authority
    The OpenID Provider authority (e.g., https://login.microsoftonline.com/{tenant}/v2.0).
.PARAMETER ClientId
    OIDC client application (app registration) Client ID.
.PARAMETER ClientSecret
    OIDC client application Client Secret (leave empty for public clients).
.PARAMETER Scope
    Additional scopes to request; default includes 'openid' and 'profile'.
.PARAMETER CallbackPath
    Callback path for the redirect URI (default '/signin-oidc').
.PARAMETER ResponseMode
    The response mode (default 'form_post'). Use 'query' or 'fragment' if needed.
.PARAMETER UsePkce
    Enable PKCE for code flow (default: true).
.PARAMETER SaveTokens
    Persist tokens into the auth cookie (default: true).
.PARAMETER GetUserInfo
    Call the UserInfo endpoint to enrich claims when available (default: true).
.PARAMETER VerboseEvents
    Enable verbose event logging for token responses and remote failures.
.PARAMETER PassThru
    Return the modified server object.
.EXAMPLE
    Add-KrOpenIdConnectAuthentication -Authority 'https://example.com' -ClientId $id -ClientSecret $secret
.EXAMPLE
    Add-KrOpenIdConnectAuthentication -Name 'AzureAD' -Authority $authority -ClientId $id -ClientSecret $secret -Scope 'email' -CallbackPath '/signin-oidc'
#>
function Add-KrOpenIdConnectAuthentication {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [string]$Name = 'Oidc',
        [Parameter(Mandatory = $true)]
        [string]$Authority,
        [Parameter(Mandatory = $true)]
        [string]$ClientId,
        [Parameter(Mandatory = $false)]
        [string]$ClientSecret,
        [string[]]$Scope,
        [switch]$PassThru
    )
    process {
        $Server = Resolve-KestrunServer -Server $Server

        # Build OpenIdConnectOptions object
        $options = [Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions]::new()
        $options.Authority = $Authority
        $options.ClientId = $ClientId
        if ($ClientSecret) {
            $options.ClientSecret = $ClientSecret
        }
        # Add additional scopes (defaults already include 'openid' and 'profile')
        if ($Scope) {
            foreach ($s in $Scope) {
                if ($options.Scope -notcontains $s) {
                    $options.Scope.Add($s) | Out-Null
                }
            }
        }

        # Enable PKCE, token persistence, and userinfo by default
        [Kestrun.Hosting.KestrunHostAuthnExtensions]::AddOpenIdConnectAuthentication(
            $Server,
            $Name,
            $options,
            $null
        ) | Out-Null

        if ($PassThru) { return $Server }
    }
}
