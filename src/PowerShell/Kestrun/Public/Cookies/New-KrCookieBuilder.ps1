<#
.SYNOPSIS
    Creates and configures a new [Microsoft.AspNetCore.Http.CookieBuilder] instance.
.DESCRIPTION
    Provides a PowerShell-friendly wrapper for constructing CookieBuilder objects with strongly-typed
    properties commonly used when configuring cookie authentication. All parameters are optional; any
    not supplied retain the underlying default from CookieBuilder / ASP.NET Core.
    This helper simplifies scripts by avoiding direct new() object + property assignment sequences.
.PARAMETER Name
    The name of the cookie. If not specified, a framework default may be used by the consumer.
.PARAMETER Domain
    The domain to associate the cookie with. If not specified, the cookie is associated with the host of the request.
.PARAMETER Path
    The path to associate the cookie with. If not specified, the cookie is associated with the root path ('/').
.PARAMETER HttpOnly
    Indicates whether a cookie is inaccessible by client-side script but specific components may use a different value.
.PARAMETER IsEssential
    Indicates if this cookie is essential for the application to function correctly. If true then
    consent policy checks may be bypassed.SameSiteMode Default is $false.
.PARAMETER MaxAge
    The maximum age of the cookie. Accepts a TimeSpan or a value convertible to Time
    Span (e.g. string like '00:30:00' or integer seconds). If not specified, the cookie is a session cookie.
.PARAMETER Expiration
    Alias for Expires; provided for convenience. Accepts the same types and conversion as Expires.
.PARAMETER SecurePolicy
    The secure policy for the cookie. Accepts a value from the CookieSecurePolicy enum.
    If not specified, the default is CookieSecurePolicy.SameAsRequest.
    - SameAsRequest: Cookie is secure if the request is HTTPS; supports both HTTP and HTTPS for development.
    - Always: Cookie is always marked secure; use when all pages are served over HTTPS.
    - None: Cookie is never marked secure; not recommended due to potential security risks on HTTP connections.
.PARAMETER SameSite
    The SameSite mode for the cookie. Accepts a value from the [Microsoft.AspNetCore.Http.SameSiteMode] enum:
    - Unspecified (-1): No SameSite field will be set; the client should follow its default cookie policy.
    - None (0): Indicates the client should disable same-site restrictions.
    - Lax: Indicates the client should send the cookie with "same-site" requests, and with "cross-site" top-level navigations.
    - Strict: Indicates the client should only send the cookie with "same-site" requests.
.PARAMETER Extensions
    Additional cookie attributes to append to the Set-Cookie header. Accepts an array of strings
.PARAMETER WhatIf
    Shows what would happen if the command runs. The command is not run.
.PARAMETER Confirm
    Prompts you for confirmation before running the command. The command is not run unless you respond affirmatively.
.EXAMPLE
    # Basic cookie
    $cookie = New-KrCookieBuilder -Name 'AuthCookie' -HttpOnly -SameSite Lax
.EXAMPLE
    # Full configuration
    $cookie = New-KrCookieBuilder -Name 'AuthCookie' -Domain 'example.local' -Path '/' -SecurePolicy Always \
        -IsEssential -MaxAge (New-TimeSpan -Hours 1) -Expires (Get-Date).AddHours(1) -SameSite Strict -HttpOnly
.OUTPUTS
    Microsoft.AspNetCore.Http.CookieBuilder

.NOTES
    Setting both -MaxAge and -Expires is allowed; ASP.NET Core will honour both where applicable.
    If -Name is omitted a framework default may be used by the consumer.
#>
function New-KrCookieBuilder {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
    [OutputType([Microsoft.AspNetCore.Http.CookieBuilder])]
    param(
        [Parameter()] [string]$Name,
        [Parameter()] [string]$Domain,
        [Parameter()] [string]$Path,
        [Parameter()] [switch]$HttpOnly,
        [Parameter()] [switch]$IsEssential,
        [Parameter()] [object]$MaxAge,
        [Parameter()] [object]$Expiration,
        [Parameter()] [Microsoft.AspNetCore.Http.CookieSecurePolicy]$SecurePolicy,
        [Parameter()] [Microsoft.AspNetCore.Http.SameSiteMode]$SameSite,
        [Parameter()] [string[]]$Extensions
    )

    $builder = [Microsoft.AspNetCore.Http.CookieBuilder]::new()

    if ($PSBoundParameters.ContainsKey('Name')) {
        $builder.Name = $Name
    }
    if ($PSBoundParameters.ContainsKey('Domain')) {
        $builder.Domain = $Domain
    }
    if ($PSBoundParameters.ContainsKey('Path')) {
        $builder.Path = $Path
    }
    if ($HttpOnly.IsPresent) {
        $builder.HttpOnly = $true
    }
    if ($IsEssential.IsPresent) {
        $builder.IsEssential = $true
    }
    if ($PSBoundParameters.ContainsKey('MaxAge')) {
        $builder.MaxAge = ConvertTo-TimeSpan -InputObject $MaxAge
    }
    if ($PSBoundParameters.ContainsKey('Expiration')) {
        $builder.Expiration = ConvertTo-TimeSpan -InputObject $Expiration
    }
    if ($PSBoundParameters.ContainsKey('SecurePolicy')) {
        $builder.SecurePolicy = $SecurePolicy
    }
    if ($PSBoundParameters.ContainsKey('SameSite')) {
        $builder.SameSite = $SameSite
    }
    if ($PSBoundParameters.ContainsKey('Extensions')) {
        $builder.Extensions.Clear()
        $builder.Extensions.AddRange($Extensions)
    }

    if ($PSCmdlet.ShouldProcess('CookieBuilder', 'Create')) {
        return $builder
    } else {
        return $null
    }
}
