<#
.SYNOPSIS
    Adds cookie authentication to the Kestrun server.
.DESCRIPTION
    Configures the Kestrun server to use cookie authentication for incoming requests.
.PARAMETER Server
    The Kestrun server instance to configure. If not specified, the current server instance is used.
.PARAMETER Name
    The name of the cookie authentication scheme.
.PARAMETER Options
    The cookie authentication options to configure. If not specified, default options are used.
.PARAMETER ClaimPolicy
    The claim policy configuration to apply to the authentication scheme.
.PARAMETER SlidingExpiration
    Indicates whether the cookie expiration should be sliding. Defaults to false.
.PARAMETER LoginPath
    The path to the login page. If not specified, defaults to "/Account/Login".
.PARAMETER LogoutPath
    The path to the logout page. If not specified, defaults to "/Account/Logout".
.PARAMETER AccessDeniedPath
    The path to the access denied page. If not specified, defaults to "/Account/AccessDenied".
.PARAMETER ReturnUrlParameter
    The name of the query parameter used to return the URL after login. Defaults to "ReturnUrl".
.PARAMETER ExpireTimeSpan
    The time span after which the cookie expires. Defaults to 14 days.
.PARAMETER Cookie
    The cookie configuration to use. If not specified, default cookie settings are applied.
    Can be created with New-KrCookieBuilder and passed via pipeline.
.PARAMETER PassThru
    If specified, the cmdlet returns the modified server instance after configuration.
.EXAMPLE
    Add-KrCookiesAuthentication -Server $myServer -Name 'MyCookieAuth' -Options $myCookieOptions -ClaimPolicy $myClaimPolicy
    Adds cookie authentication to the specified Kestrun server with the provided options and claim policy.
.EXAMPLE
    Add-KrCookiesAuthentication -Name 'MyCookieAuth' -SlidingExpiration -LoginPath '/Login' -LogoutPath '/Logout' -AccessDeniedPath '/Denied' -ExpireTimeSpan (New-TimeSpan -Days 14)
    Configures cookie authentication with sliding expiration and custom paths for login, logout, and access denied
.EXAMPLE
    $cookie = New-KrCookieBuilder -Name 'AuthCookie' -HttpOnly -SameSite Lax
    Add-KrCookiesAuthentication -Name 'MyCookieAuth' -Cookie $cookie -SlidingExpiration -ExpireTimeSpan (New-TimeSpan -Days 7)
    Configures cookie authentication using a custom cookie with HttpOnly and SameSite=Lax, along with sliding expiration.
.EXAMPLE
    New-KrCookieBuilder -Name 'AuthCookie' -HttpOnly -SameSite Lax |
        Add-KrCookiesAuthentication -Name 'MyCookieAuth' -SlidingExpiration -ExpireTimeSpan (New-TimeSpan -Days 7)
    Configures cookie authentication using a custom cookie created via pipeline with HttpOnly and SameSite=Lax, along with sliding expiration.
.NOTES
    This cmdlet is part of the Kestrun PowerShell module and is used to configure cookie authentication for Kestrun servers.
.LINK
    https://docs.kestrun.dev/docs/powershell/kestrun/authentication
#>
function Add-KrCookiesAuthentication {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions]$Options,
        [Parameter()]
        [Kestrun.Claims.ClaimPolicyConfig]$ClaimPolicy,
        [Parameter(ParameterSetName = 'Items')]
        [switch] $SlidingExpiration,
        [Parameter(ParameterSetName = 'Items')]
        [string]$LoginPath,
        [Parameter(ParameterSetName = 'Items')]
        [string]$LogoutPath,
        [Parameter(ParameterSetName = 'Items')]
        [string]$AccessDeniedPath,
        [Parameter(ParameterSetName = 'Items')]
        [string]$ReturnUrlParameter,
        [Parameter(ParameterSetName = 'Items')]
        [timespan] $ExpireTimeSpan,
        [Parameter(ParameterSetName = 'Items', ValueFromPipeline = $true)]
        [Microsoft.AspNetCore.Http.CookieBuilder]$Cookie,
        [Parameter()]
        [switch]$PassThru
    )
    process {
        if ($PSCmdlet.ParameterSetName -ne 'Options') {
            $Options = [Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions]::new()
            if ($PSBoundParameters.ContainsKey('SlidingExpiration')) { $Options.SlidingExpiration = $SlidingExpiration.IsPresent }
            if ($PSBoundParameters.ContainsKey('LoginPath')) { $Options.LoginPath = $LoginPath }
            if ($PSBoundParameters.ContainsKey('LogoutPath')) { $Options.LogoutPath = $LogoutPath }
            if ($PSBoundParameters.ContainsKey('AccessDeniedPath')) { $Options.AccessDeniedPath = $AccessDeniedPath }
            if ($PSBoundParameters.ContainsKey('ReturnUrlParameter')) { $Options.ReturnUrlParameter = $ReturnUrlParameter }
            if ($PSBoundParameters.ContainsKey('ExpireTimeSpan')) { $Options.ExpireTimeSpan = $ExpireTimeSpan }
            if ($PSBoundParameters.ContainsKey('Cookie')) { $Options.Cookie = $Cookie }
        }
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        [Kestrun.Hosting.KestrunHostAuthnExtensions]::AddCookieAuthentication(
            $Server, $Name, $Options, $ClaimPolicy) | Out-Null
        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}

