<#
.SYNOPSIS
    Adds GitHub OAuth (Authorization Code) authentication to the Kestrun server.
.DESCRIPTION
    Convenience wrapper around the C# extension AddGitHubOAuthAuthentication. Registers three schemes:
      <Name>, <Name>.Cookies, <Name>.Policy
    Includes PKCE, saves tokens, maps login & avatar claims, and can enrich email from /user/emails.
.PARAMETER Server
    The Kestrun server instance. If omitted, uses the current active server.
.PARAMETER AuthenticationScheme
    Base scheme name (default 'GitHub').
.PARAMETER DisplayName
    Display name for the authentication scheme.
.PARAMETER Description
    A description of the GitHub authentication scheme.
.PARAMETER DocId
    Documentation IDs for the authentication scheme.
.PARAMETER ClientId
    GitHub OAuth App Client ID.
.PARAMETER ClientSecret
    GitHub OAuth App Client Secret.
.PARAMETER CallbackPath
    Optional callback path (default '/signin-oauth'). Must match your GitHub OAuth App redirect URL path.
.PARAMETER PassThru
    Return the modified server object.
.EXAMPLE
    Add-KrGitHubAuthentication -ClientId $env:GITHUB_CLIENT_ID -ClientSecret $env:GITHUB_CLIENT_SECRET
.EXAMPLE
    Add-KrGitHubAuthentication -AuthenticationScheme 'GitHubMain' -ClientId 'abc' -ClientSecret 'secret' -Scope 'gist' -DisableEmailEnrichment
.NOTES
    Requires the generic OAuth2 infrastructure plus provider-specific handling in C#.
#>
function Add-KrGitHubAuthentication {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string]$AuthenticationScheme = [Kestrun.Authentication.AuthenticationDefaults]::GitHubAuthenticationSchemeName,

        [Parameter()]
        [string]$DisplayName = [Kestrun.Authentication.AuthenticationDefaults]::GitHubDisplayName,

        [Parameter()]
        [string[]]$DocId = [Kestrun.Authentication.IOpenApiAuthenticationOptions]::DefaultDocumentationIds,

        [Parameter()]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [string]$ClientId,
        [Parameter(Mandatory = $true)]
        [string]$ClientSecret,
        [string]$CallbackPath = '/signin-oauth',
        [switch]$PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        [Kestrun.Hosting.KestrunHostAuthnExtensions]::AddGitHubOAuthAuthentication(
            $Server,
            $AuthenticationScheme,
            $DisplayName,
            $DocId,
            $Description,
            $ClientId,
            $ClientSecret,
            $CallbackPath
        ) | Out-Null

        if ($PassThru) { return $Server }
    }
}
