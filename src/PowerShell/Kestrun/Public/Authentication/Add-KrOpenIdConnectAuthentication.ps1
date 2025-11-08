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
    Base scheme name (default 'OIDC').
.PARAMETER Authority
    The OpenID Provider authority (e.g., https://login.microsoftonline.com/{tenant}/v2.0).
.PARAMETER ClientId
    OIDC client application (app registration) Client ID.
.PARAMETER ClientSecret
    OIDC client application Client Secret.
.PARAMETER Scope
    Additional scopes to request; default includes 'openid' and 'profile'.
.PARAMETER CallbackPath
    Callback path for the redirect URI (default '/signin-oidc').
.PARAMETER UsePkce
    Enable PKCE for code flow (default: on).
.PARAMETER SaveTokens
    Persist tokens into the auth cookie (default: on).
.PARAMETER GetUserInfo
    Call the UserInfo endpoint to enrich claims when available (default: on).
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
        [string]$Name = 'OIDC',
        [Parameter(Mandatory = $true)]
        [string]$Authority,
        [Parameter(Mandatory = $true)]
        [string]$ClientId,
        [Parameter(Mandatory = $true)]
        [string]$ClientSecret,
        [string[]]$Scope,
        [string]$CallbackPath = '/signin-oidc',
        [switch]$UsePkce,
        [switch]$SaveTokens,
        [switch]$GetUserInfo,
        [switch]$VerboseEvents,
        [switch]$PassThru
    )
    process {
        $Server = Resolve-KestrunServer -Server $Server
        # Defaults for switches: on by default unless explicitly set false; PowerShell switch only signals true when present.
        $usePkce = $true; if ($PSBoundParameters.ContainsKey('UsePkce')) { $usePkce = [bool]$UsePkce }
        $saveTokens = $true; if ($PSBoundParameters.ContainsKey('SaveTokens')) { $saveTokens = [bool]$SaveTokens }
        $getUserInfo = $true; if ($PSBoundParameters.ContainsKey('GetUserInfo')) { $getUserInfo = [bool]$GetUserInfo }

        [Kestrun.Hosting.KestrunHostAuthnExtensions]::AddOpenIdConnectAuthentication(
            $Server,
            $Name,
            $ClientId,
            $ClientSecret,
            $Authority,
            $Scope,
            $CallbackPath,
            $usePkce,
            $saveTokens,
            $getUserInfo,
            [bool]$VerboseEvents.IsPresent,
            $null,
            $null
        ) | Out-Null
        if ($PassThru) { return $Server }
    }
}
