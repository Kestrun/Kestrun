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
    An instance of Kestrun.Authentication.OAuth2Options containing the OAuth configuration.
.PARAMETER PassThru
    If specified, returns the modified Kestrun server object.
.EXAMPLE
    Add-KrOAuth2Authentication -Name 'MyOAuth' -Options $oauthOptions
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
        [string]$Name = 'OAuth2',

        [Parameter(Mandatory = $true)]
        [Kestrun.Authentication.OAuth2Options]$Options,

        [Parameter(Mandatory = $false)]
        [switch]$PassThru
    )
    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        # Bridge to your C# extension (parallel to AddCookieAuthentication)
        [Kestrun.Hosting.KestrunHostAuthnExtensions]::AddOAuth2Authentication(
            $Server, $Name, $Options
        ) | Out-Null

        if ($PassThru.IsPresent) { return $Server }
    }
}
