<#
    .SYNOPSIS
        Adds caching headers to the HTTP response.
    .DESCRIPTION
        This cmdlet allows you to add caching headers to the HTTP response in a route script block.
        It provides various parameters to customize the caching behavior, such as setting max-age,
        no-cache, no-store, and other cache control directives.
    .PARAMETER NoCache
        If specified, the 'no-cache' directive will be added to the Cache-Control header.
    .PARAMETER NoStore
        If specified, the 'no-store' directive will be added to the Cache-Control header.
    .PARAMETER MaxAge
        If specified, sets the 'max-age' directive in seconds for the Cache-Control header.
    .PARAMETER SharedMaxAge
        If specified, sets the 's-maxage' directive in seconds for the Cache-Control header
        (used by shared caches).
    .PARAMETER MaxStale
        If specified, the 'max-stale' directive will be added to the Cache-Control header.
    .PARAMETER MaxStaleLimit
        If specified, sets the limit in seconds for the 'max-stale' directive in the Cache-Control header.
    .PARAMETER MinFresh
        If specified, sets the 'min-fresh' directive in seconds for the Cache-Control header.
    .PARAMETER NoTransform
        If specified, the 'no-transform' directive will be added to the Cache-Control header.
    .PARAMETER OnlyIfCached
        If specified, the 'only-if-cached' directive will be added to the Cache-Control header.
    .PARAMETER Public
        If specified, the 'public' directive will be added to the Cache-Control header.
    .PARAMETER Private
        If specified, the 'private' directive will be added to the Cache-Control header.
    .PARAMETER MustRevalidate
        If specified, the 'must-revalidate' directive will be added to the Cache-Control header.
    .PARAMETER ProxyRevalidate
        If specified, the 'proxy-revalidate' directive will be added to the Cache-Control header.
    .EXAMPLE
         Add-KrCacheResponse  -NoCache -MaxAge 3600 -Public
        This example adds caching headers to the response, setting the 'no-cache' directive,
        a 'max-age' of 3600 seconds, and marking the response as 'public'.
    .EXAMPLE
        Add-KrCacheResponse -NoStore -Private -MustRevalidate
        This example adds caching headers to the response, setting the 'no-store' directive,
        marking the response as 'private', and adding the 'must-revalidate' directive.
    .NOTES
        This cmdlet is used to add caching headers to the response in a route script block,
        allowing you to control how responses are cached by clients and intermediate caches.
        It must be used within a route script block where the $Context variable is available.
#>
function Add-KrCacheResponse {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter()]
        [switch]$NoCache,
        [Parameter()]
        [switch]$NoStore,
        [Parameter()]
        [ValidateRange(1, [int]::MaxValue)]
        [int]$MaxAge,
        [Parameter()]
        [ValidateRange(1, [int]::MaxValue)]
        [int]$SharedMaxAge,
        [Parameter()]
        [switch]$MaxStale,
        [Parameter()]
        [ValidateRange(1, [int]::MaxValue)]
        [int]$MaxStaleLimit,
        [Parameter()]
        [ValidateRange(1, [int]::MaxValue)]
        [int]$MinFresh,
        [Parameter()]
        [switch]$NoTransform,
        [Parameter()]
        [switch]$OnlyIfCached,
        [Parameter()]
        [switch]$Public,
        [Parameter()]
        [switch]$Private,
        [Parameter()]
        [switch]$MustRevalidate,
        [Parameter()]
        [switch]$ProxyRevalidate
    )
    # Only works inside a route script block where $Context is available
    if ($null -ne $Context.Response) {
        # Define default cache control headers if not provided
        $cacheControl = [Microsoft.Net.Http.Headers.CacheControlHeaderValue]::new();
        if ($PSBoundParameters.ContainsKey('NoCache')) { $cacheControl.NoCache = $NoCache.IsPresent }
        if ($PSBoundParameters.ContainsKey('NoStore')) { $cacheControl.NoStore = $NoStore.IsPresent }
        if ($PSBoundParameters.ContainsKey('MaxAge')) { $cacheControl.MaxAge = [TimeSpan]::FromSeconds($MaxAge) }
        if ($PSBoundParameters.ContainsKey('SharedMaxAge')) { $cacheControl.SharedMaxAge = [TimeSpan]::FromSeconds($SharedMaxAge) }
        if ($PSBoundParameters.ContainsKey('MaxStale')) { $cacheControl.MaxStale = $MaxStale.IsPresent }
        if ($PSBoundParameters.ContainsKey('MaxStaleLimit')) { $cacheControl.MaxStaleLimit = [TimeSpan]::FromSeconds($MaxStaleLimit) }
        if ($PSBoundParameters.ContainsKey('MinFresh')) { $cacheControl.MinFresh = [TimeSpan]::FromSeconds($MinFresh) }
        if ($PSBoundParameters.ContainsKey('NoTransform')) { $cacheControl.NoTransform = $NoTransform.IsPresent }
        if ($PSBoundParameters.ContainsKey('OnlyIfCached')) { $cacheControl.OnlyIfCached = $OnlyIfCached.IsPresent }
        if ($PSBoundParameters.ContainsKey('Public')) { $cacheControl.Public = $Public.IsPresent }
        if ($PSBoundParameters.ContainsKey('Private')) { $cacheControl.Private = $Private.IsPresent }
        if ($PSBoundParameters.ContainsKey('MustRevalidate')) { $cacheControl.MustRevalidate = $MustRevalidate.IsPresent }
        if ($PSBoundParameters.ContainsKey('ProxyRevalidate')) { $cacheControl.ProxyRevalidate = $ProxyRevalidate.IsPresent }

        # Apply the cache control headers to the response
        $Context.Response.CacheControl = $cacheControl
    } else {
        Write-KrOutsideRouteWarning
    }
}
