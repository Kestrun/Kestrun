<#
    .SYNOPSIS
        Adds response caching to the Kestrun server.
    .DESCRIPTION
        This cmdlet allows you to enable and configure response caching for the Kestrun server.
        It can be used to improve performance by caching responses for frequently requested resources.
    .PARAMETER Server
        The Kestrun server instance to which response caching will be added.
    .PARAMETER SizeLimit
        The maximum size, in bytes, of the response cache. If not specified, the default
        size limit of the underlying implementation will be used.
    .PARAMETER MaximumBodySize
        The maximum size, in bytes, of the response body that can be cached. If not specified,
        the default value of 64KB will be used.
    .PARAMETER UseCaseSensitivePaths
        If specified, the caching will be case-sensitive with respect to request paths.

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
    .PARAMETER PassThru
        If specified, returns the modified server instance after adding response caching.
    .EXAMPLE
        $server | Add-KrCacheMiddleware -SizeLimit 10485760 -MaximumBody 65536 -UseCaseSensitivePaths
        This example adds response caching to the server with a size limit of 10MB, a maximum body size of 64KB,
        and enables case-sensitive paths.
    .EXAMPLE
        $server | Add-KrCacheMiddleware
        This example adds response caching to the server with default settings.
    .NOTES
        This cmdlet is used to enable and configure response caching for the Kestrun server,
#>
function Add-KrCacheMiddleware {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [long]$SizeLimit,
        [Parameter()]
        [long]$MaximumBodySize,
        [Parameter()]
        [switch]$UseCaseSensitivePaths,

        [Parameter()]
        [switch]$NoCache,
        [Parameter()]
        [switch]$NoStore,
        [Parameter()]
        [int]$MaxAge,
        [Parameter()]
        [int]$SharedMaxAge,
        [Parameter()]
        [switch]$MaxStale,
        [Parameter()]
        [int]$MaxStaleLimit,
        [Parameter()]
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
        [switch]$ProxyRevalidate,
        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        if ($null -eq $Server) {
            throw 'Server is not initialized. Please ensure the server is configured before setting options.'
        }
    }
    process {
        $options = [Microsoft.AspNetCore.ResponseCaching.ResponseCachingOptions]::new()
        if ($PSBoundParameters.ContainsKey('SizeLimit')) {
            $options.SizeLimit = $SizeLimit
        }
        if ($PSBoundParameters.ContainsKey('MaximumBodySize')) {
            $options.MaximumBodySize = $MaximumBodySize
        }
        if ($PSBoundParameters.ContainsKey('UseCaseSensitivePaths')) {
            $options.UseCaseSensitivePaths = $UseCaseSensitivePaths.IsPresent
        }

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

        # Add response caching middleware to the server
        [Kestrun.Hosting.KestrunHttpMiddlewareExtensions]::AddResponseCaching($Server, $options, $cacheControl) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}

