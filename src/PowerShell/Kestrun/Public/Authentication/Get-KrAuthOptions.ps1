<#
.SYNOPSIS
    Retrieves the authentication options for a specified scheme.
.DESCRIPTION
    This function fetches the options for a given authentication scheme from the dependency injection container.
    It requires the current HTTP context to access the service provider.
.PARAMETER Scheme
    The name of the authentication scheme to retrieve options for.
.PARAMETER OptionType
    The type of the authentication options to retrieve. This should be a type derived from AuthenticationSchemeOptions.
.EXAMPLE
    $options = Get-KrAuthOptions -Scheme 'Cookies' -OptionType ([Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions])
    # Retrieves the cookie authentication options for the 'Cookies' scheme.
.OUTPUTS
    An instance of the specified OptionType containing the authentication options for the given scheme.
.NOTES
    This function is intended to be used within a Kestrun route script block where $Context is available.
#>

function Get-KrAuthOptions {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [string] $Scheme  # if omitted, uses default authenticate scheme
    )

    if (-not $Context) { throw "No request Context available." }

    $sp = $Context.RequestServices

    # 1) Get the scheme (or the default)
    $provType = [Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider]
    $provider = $sp.GetService($provType)
    if (-not $provider) { throw "IAuthenticationSchemeProvider not available. Is Authentication added?" }

    if ([string]::IsNullOrWhiteSpace($Scheme)) {
        $default = $provider.GetDefaultAuthenticateSchemeAsync().GetAwaiter().GetResult()
        if (-not $default) { throw "No default authenticate scheme configured. Specify -Scheme." }
        $Scheme = $default.Name
    }

    $scheme = $provider.GetSchemeAsync($Scheme).GetAwaiter().GetResult()
    if (-not $scheme) { throw "Authentication scheme '$Scheme' is not registered." }

    # 2) From the handler, discover TOptions from AuthenticationHandler`1<TOptions>
    $handlerType = $scheme.HandlerType
    $base = $handlerType
    $optType = $null
    while ($null -ne $base -and -not $optType) {
        if ($base.IsGenericType -and
            $base.GetGenericTypeDefinition().FullName -eq 'Microsoft.AspNetCore.Authentication.AuthenticationHandler`1') {
            $optType = $base.GetGenericArguments()[0]
            break
        }
        $base = $base.BaseType
    }
    if (-not $optType) { throw "Could not infer options type for scheme '$Scheme' (handler=$handlerType)." }

    # 3) Get IOptionsMonitor<TOptions> and fetch options for the scheme
    $monitorGen = [Microsoft.Extensions.Options.IOptionsMonitor``1].MakeGenericType($optType)
    $monitor = $sp.GetService($monitorGen)
    if (-not $monitor) {
        throw "No monitor found for $($optType.FullName). Ensure AddAuthentication().AddXxx('$Scheme', ...) was called and the package is referenced."
    }

    # 4) Return the actual options object
    return $monitor.Get($Scheme)
}
