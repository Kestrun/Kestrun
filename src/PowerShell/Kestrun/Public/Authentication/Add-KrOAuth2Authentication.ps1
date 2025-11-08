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
.PARAMETER UsePkce
    Enables Proof Key for Code Exchange (PKCE) for the authorization code flow.
.PARAMETER SignInScheme
    Explicit cookie scheme to sign into (overrides -CookieScheme if both provided).
.PARAMETER ClaimsIssuer
    The issuer to use when creating claims.
.PARAMETER ClaimMap
    Hashtable mapping ClaimType => JsonKey for mapping userinfo JSON fields to claims.
.PARAMETER ClaimSubMap
    Hashtable mapping ClaimType => 'jsonKey:subKey' for nested userinfo JSON fields.
.PARAMETER BackchannelTimeout
    Timeout for the backchannel HTTP client used by the OAuth handler.
.PARAMETER BackchannelHeaders
    Additional default headers to set on the backchannel HTTP client (hashtable of Name=Value).
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

        [Parameter(ParameterSetName = 'Items')]
        [switch]$UsePkce,

        [Parameter(ParameterSetName = 'Items')]
        [string]$SignInScheme,

        [Parameter(ParameterSetName = 'Items')]
        [string]$ClaimsIssuer,

        [Parameter(ParameterSetName = 'Items')]
        [hashtable]$ClaimMap,

        [Parameter(ParameterSetName = 'Items')]
        [hashtable]$ClaimSubMap,

        [Parameter(ParameterSetName = 'Items')]
        [TimeSpan]$BackchannelTimeout,

        [Parameter(ParameterSetName = 'Items')]
        [hashtable]$BackchannelHeaders,

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
            if (-not [string]::IsNullOrWhiteSpace($SignInScheme)) {
                $Options.SignInScheme = $SignInScheme
            } else {
                if ([string]::IsNullOrWhiteSpace($CookieScheme)) {
                    $CookieScheme = "$Name.Cookies"
                }
                $Options.SignInScheme = $CookieScheme
            }

            # Scopes
            if ($null -ne $Scope -and $Scope.Count -gt 0) {
                $Options.Scope.Clear()
                foreach ($s in $Scope) { [void]$Options.Scope.Add($s) }
            }

            # Optional user info endpoint (allow absolute URL; else combine with Authority)
            if (-not [string]::IsNullOrWhiteSpace($UserInformationPath)) {
                if ([System.Uri]::IsWellFormedUriString($UserInformationPath, [System.UriKind]::Absolute)) {
                    $Options.UserInformationEndpoint = $UserInformationPath
                } else {
                    $Options.UserInformationEndpoint = ($Authority.TrimEnd('/') + '/' + $UserInformationPath.TrimStart('/'))
                }
                # Claim mapping can be customized later via -ClaimMap / -ClaimSubMap
            }

            # PKCE
            if ($UsePkce.IsPresent) { $Options.UsePkce = $true }

            # Claims issuer
            if (-not [string]::IsNullOrWhiteSpace($ClaimsIssuer)) { $Options.ClaimsIssuer = $ClaimsIssuer }

            # Claim mappings (flat) – manually create JsonKeyClaimAction (extension helpers not available)
            if ($ClaimMap) {
                foreach ($k in $ClaimMap.Keys) {
                    $jsonKey = [string]$ClaimMap[$k]
                    $claimType = [string]$k
                    $action = [Microsoft.AspNetCore.Authentication.OAuth.Claims.JsonKeyClaimAction]::new(
                        $claimType,
                        [System.Security.Claims.ClaimValueTypes]::String,
                        $jsonKey)
                    $Options.ClaimActions.Add($action)
                }
            }
            # Claim mappings (nested jsonKey:subKey) – manually create JsonSubKeyClaimAction
            if ($ClaimSubMap) {
                foreach ($k in $ClaimSubMap.Keys) {
                    $spec = [string]$ClaimSubMap[$k]
                    $parts = $spec.Split(':', 2)
                    if ($parts.Count -eq 2 -and $parts[0] -and $parts[1]) {
                        $claimType = [string]$k
                        $jsonKey = $parts[0]
                        $subKey = $parts[1]
                        $action = [Microsoft.AspNetCore.Authentication.OAuth.Claims.JsonSubKeyClaimAction]::new(
                            $claimType,
                            [System.Security.Claims.ClaimValueTypes]::String,
                            $jsonKey,
                            $subKey)
                        $Options.ClaimActions.Add($action)
                    }
                }
            }

            # Backchannel
            if ($PSBoundParameters.ContainsKey('BackchannelTimeout') -or $BackchannelHeaders) {
                $handler = [System.Net.Http.HttpClientHandler]::new()
                $client = [System.Net.Http.HttpClient]::new($handler)
                if ($PSBoundParameters.ContainsKey('BackchannelTimeout')) { $client.Timeout = $BackchannelTimeout }
                if ($BackchannelHeaders) {
                    foreach ($h in $BackchannelHeaders.GetEnumerator()) {
                        [void]$client.DefaultRequestHeaders.TryAddWithoutValidation([string]$h.Key, [string]$h.Value)
                    }
                }
                $Options.Backchannel = $client
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
