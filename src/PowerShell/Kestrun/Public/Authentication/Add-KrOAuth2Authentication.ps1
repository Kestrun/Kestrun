<#
.SYNOPSIS
    Adds OAuth 2.0 (Authorization Code) authentication to the Kestrun server.
.DESCRIPTION
    Configures the Kestrun server to use a generic OAuth 2.0 authorization-code flow.
    You can pass a prebuilt OAuthOptions object, or specify individual items (authority, paths, client, etc.).
.PARAMETER Server
    The Kestrun server instance to configure. If not specified, the current server instance is used.
.PARAMETER Name
    The name of the OAuth authentication scheme (e.g., 'MyOAuth').
.PARAMETER Options
    A preconfigured [Microsoft.AspNetCore.Authentication.OAuth.OAuthOptions] instance.
.PARAMETER ClaimPolicy
    A claim policy configuration applied to the authentication scheme.
.PARAMETER ClientId
    The OAuth client ID (when using itemized parameters).
.PARAMETER ClientSecret
    The OAuth client secret (when using itemized parameters).
.PARAMETER Authority
    The base authority URL (e.g., https://auth.example.com).
.PARAMETER AuthorizationPath
    Relative path to the authorization endpoint. Defaults to '/oauth/authorize'.
.PARAMETER TokenPath
    Relative path to the token endpoint. Defaults to '/oauth/token'.
.PARAMETER CallbackPath
    The callback path that receives the authorization code. Defaults to '/signin-oauth'.
.PARAMETER UserInformationPath
    Optional relative path to a userinfo endpoint. If provided, the handler will call it after token exchange.
.PARAMETER Scope
    One or more OAuth scopes to request.
.PARAMETER CookieScheme
    The cookie scheme that OAuth signs into. Defaults to '<Name>.Cookies' if not specified.
.PARAMETER PassThru
    If specified, returns the modified server instance.
.EXAMPLE
    Add-KrOAuth2Authentication -Name 'MyOAuth' -Authority 'https://auth.example.com' `
        -ClientId 'abc' -ClientSecret 'secret' -Scope 'read','write'
.EXAMPLE
    $opts = [Microsoft.AspNetCore.Authentication.OAuth.OAuthOptions]::new()
    $opts.ClientId = 'abc'; $opts.ClientSecret = 'secret'
    $opts.AuthorizationEndpoint = 'https://auth.example.com/oauth/authorize'
    $opts.TokenEndpoint = 'https://auth.example.com/oauth/token'
    $opts.CallbackPath = '/signin-oauth'
    Add-KrOAuth2Authentication -Name 'MyOAuth' -Options $opts
.NOTES
    This cmdlet requires a corresponding C# extension method:
    Kestrun.Hosting.KestrunHostAuthnExtensions.AddOAuth2Authentication(
        KestrunHost host, string scheme,
        Microsoft.AspNetCore.Authentication.OAuth.OAuthOptions options,
        Kestrun.Claims.ClaimPolicyConfig claimPolicy)
#>
function Add-KrOAuth2Authentication {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Authentication.OAuth.OAuthOptions]$Options,

        [Parameter()]
        [Kestrun.Claims.ClaimPolicyConfig]$ClaimPolicy,

        # ---- Itemized mode ----
        [Parameter(Mandatory = $true, ParameterSetName = 'Items')]
        [string]$ClientId,

        [Parameter(Mandatory = $true, ParameterSetName = 'Items')]
        [string]$ClientSecret,

        [Parameter(Mandatory = $true, ParameterSetName = 'Items')]
        [string]$Authority,

        [Parameter(ParameterSetName = 'Items')]
        [string]$AuthorizationPath = '/oauth/authorize',

        [Parameter(ParameterSetName = 'Items')]
        [string]$TokenPath = '/oauth/token',

        [Parameter(ParameterSetName = 'Items')]
        [string]$CallbackPath = '/signin-oauth',

        [Parameter(ParameterSetName = 'Items')]
        [string]$UserInformationPath,

        [Parameter(ParameterSetName = 'Items')]
        [string[]]$Scope,

        [Parameter(ParameterSetName = 'Items')]
        [string]$CookieScheme,

        [Parameter()]
        [switch]$PassThru
    )
    process {
        if ($PSCmdlet.ParameterSetName -ne 'Options') {
            $Options = [Microsoft.AspNetCore.Authentication.OAuth.OAuthOptions]::new()

            # Endpoints (generic OAuth has no discovery)
            $Options.AuthorizationEndpoint = ($Authority.TrimEnd('/') + '/' + $AuthorizationPath.TrimStart('/'))
            $Options.TokenEndpoint = ($Authority.TrimEnd('/') + '/' + $TokenPath.TrimStart('/'))

            # Credentials
            $Options.ClientId = $ClientId
            $Options.ClientSecret = $ClientSecret

            # Callback
            $Options.CallbackPath = $CallbackPath

            # Persist tokens
            $Options.SaveTokens = $true

            # Sign-in target (cookie)
            if ([string]::IsNullOrWhiteSpace($CookieScheme)) {
                $CookieScheme = "$Name.Cookies"
            }
            $Options.SignInScheme = $CookieScheme

            # Scopes
            if ($null -ne $Scope -and $Scope.Count -gt 0) {
                $Options.Scope.Clear()
                foreach ($s in $Scope) { [void]$Options.Scope.Add($s) }
            }

            # Optional user info endpoint
            if (-not [string]::IsNullOrWhiteSpace($UserInformationPath)) {
                $Options.UserInformationEndpoint = ($Authority.TrimEnd('/') + '/' + $UserInformationPath.TrimStart('/'))
                # Claim mapping can be customized later in C# or via a future -ClaimMap parameter
            }
        }

        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        # Bridge to your C# extension (parallel to AddCookieAuthentication)
        [Kestrun.Hosting.KestrunHostAuthnExtensions]::AddOAuth2Authentication(
            $Server, $Name, $Options, $ClaimPolicy
        ) | Out-Null

        if ($PassThru.IsPresent) { return $Server }
    }
}
