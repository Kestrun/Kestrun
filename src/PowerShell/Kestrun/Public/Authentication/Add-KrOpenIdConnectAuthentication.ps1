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
.PARAMETER ResponseMode
    The response mode. Use 'query' or 'fragment' if needed.
.PARAMETER ResponseType
    The response type. Use 'code', 'id_token', or 'token' if needed.
.PARAMETER UsePkce
    Enable PKCE for code flow (default: true).
.PARAMETER PassThru
    Return the modified server object.
.EXAMPLE
    Add-KrOpenIdConnectAuthentication -Authority 'https://example.com' -ClientId $id -ClientSecret $secret
.EXAMPLE
    Add-KrOpenIdConnectAuthentication -Name 'AzureAD' -Authority $authority -ClientId $id -ClientSecret $secret -Scope 'email' -CallbackPath '/signin-oidc'
#>
function Add-KrOpenIdConnectAuthentication {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items' )]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [string]$Name = 'Oidc',
        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions]$Options,
        [Parameter(Mandatory = $true, ParameterSetName = 'Items')]
        [string]$Authority,
        [Parameter(Mandatory = $true, ParameterSetName = 'Items')]
        [string]$ClientId,
        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [string]$ClientSecret,
        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [string[]]$Scope,
        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [ValidateSet('query', 'fragment', 'form_post')]
        [string]$ResponseMode,
        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [ValidateSet('code id_token', 'code id_token token', 'code token', 'code', 'id_token', 'id_token token', 'token', 'none')]
        [string]$ResponseType,
        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [switch]$UsePkce,
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($PSCmdlet.ParameterSetName -ne 'Options') {
            $options = [Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions]::new()
            $options.Authority = $Authority
            $options.ClientId = $ClientId
            $options.ClientSecret = $ClientSecret
            $options.Scope.Clear()
            foreach ($s in $Scope) {
                $options.Scope.Add($s) | Out-Null
            }
            $options.UsePkce = $UsePkce.IsPresent
            if ($ResponseMode) {
                switch ($ResponseMode.ToLower()) {
                    'query' { $options.ResponseMode = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseMode]::Query }
                    'fragment' { $options.ResponseMode = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseMode]::Fragment }
                    'form_post' { $options.ResponseMode = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseMode]::FormPost }
                    default { throw "Invalid ResponseMode: $ResponseMode. Valid values are 'query', 'fragment', 'form_post'." }
                }
            }
            if ($ResponseType) {
                switch ($ResponseType.ToLower()) {
                    'code' { $options.ResponseType = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseType]::Code }
                    'id_token' { $options.ResponseType = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseType]::IdToken }
                    'token' { $options.ResponseType = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseType]::Token }
                    'code id_token' { $options.ResponseType = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseType]::CodeIdToken }
                    'code token' { $options.ResponseType = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseType]::CodeToken }
                    'id_token token' { $options.ResponseType = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseType]::IdTokenToken }
                    'code id_token token' { $options.ResponseType = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseType]::CodeIdTokenToken }
                    'none' { $options.ResponseType = [Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectResponseType]::None }
                    default { throw "Invalid ResponseType: $ResponseType. Valid values are 'code', 'id_token', 'token'." }
                }
            }
        }
        # Call the simpler C# overload that takes individual parameters
        # This lets the framework properly initialize the ConfigurationManager and backchannel
        [Kestrun.Hosting.KestrunHostAuthnExtensions]::AddOpenIdConnectAuthentication(
            $Server,
            $Name,
            $Options,
            $null  # claimPolicy
        ) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}
