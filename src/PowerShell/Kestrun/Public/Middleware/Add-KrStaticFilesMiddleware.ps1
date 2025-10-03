<#
.SYNOPSIS
    Registers a static file server to serve files from a specified path.
.DESCRIPTION
    This cmdlet allows you to serve static files from a specified path using the Kestrun server.
    It can be used to serve files like images, stylesheets, and scripts.
.PARAMETER Server
    The Kestrun server instance to which the static file service will be added.
.PARAMETER Options
    The StaticFileOptions to configure the static file service.
.PARAMETER RootPath
    The root path from which to serve static files.
.PARAMETER RequestPath
    The path at which the static file service will be registered.
.PARAMETER HttpsCompression
    The HTTPS compression mode to use for the static files.
.PARAMETER ServeUnknownFileTypes
    If specified, allows serving files with unknown MIME types.
.PARAMETER DefaultContentType
    The default content type to use for files served by the static file service.
.PARAMETER RedirectToAppendTrailingSlash
    If specified, redirects requests to append a trailing slash to the URL.
.PARAMETER ContentTypeMap
    A hashtable mapping file extensions to MIME types.
.PARAMETER NoCache
    If specified, adds a 'no-cache' directive to the Cache-Control header.
.PARAMETER NoStore
    If specified, adds a 'no-store' directive to the Cache-Control header.
.PARAMETER MaxAge
    If specified, sets the 'max-age' directive in seconds for the Cache-Control header.
.PARAMETER SharedMaxAge
    If specified, sets the 's-maxage' directive in seconds for the Cache-Control header
    (used by shared caches).
.PARAMETER MaxStale
    If specified, adds a 'max-stale' directive to the Cache-Control header.
.PARAMETER MaxStaleLimit
    If specified, sets the limit in seconds for the 'max-stale' directive in the Cache-Control header.
.PARAMETER MinFresh
    If specified, sets the 'min-fresh' directive in seconds for the Cache-Control header.
.PARAMETER NoTransform
    If specified, adds a 'no-transform' directive to the Cache-Control header.
.PARAMETER OnlyIfCached
    If specified, adds an 'only-if-cached' directive to the Cache-Control header.
.PARAMETER Public
    If specified, adds a 'public' directive to the Cache-Control header.
.PARAMETER Private
    If specified, adds a 'private' directive to the Cache-Control header.
.PARAMETER MustRevalidate
    If specified, adds a 'must-revalidate' directive to the Cache-Control header.
.PARAMETER ProxyRevalidate
    If specified, adds a 'proxy-revalidate' directive to the Cache-Control header.
.PARAMETER PassThru
    If specified, the cmdlet will return the modified server instance after adding the static file service.
.EXAMPLE
    $server | Add-KrStaticFilesMiddleware -RequestPath '/static' -HttpsCompression -ServeUnknownFileTypes -DefaultContentType 'application/octet-stream' -RedirectToAppendTrailingSlash
    This example adds a static file service to the server for the path '/static', enabling HTTPS compression, allowing serving unknown file types,
    setting the default content type to 'application/octet-stream', and redirecting requests to append a trailing slash.
.EXAMPLE
    $server | Add-KrStaticFilesMiddleware -Options $options
    This example adds a static file service to the server using the specified StaticFileOptions.
.EXAMPLE
    $server | Add-KrStaticFilesMiddleware -RequestPath '/static' -MaxAge 600 -Public -MustRevalidate
    This example adds a static file service to the server for the path '/static', setting caching headers with a max-age of 600 seconds,
    marking the response as public, and adding the must-revalidate directive.
.LINK
    https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.staticfileoptions?view=aspnetcore-8.0
.NOTES
    ContentTypeProvider and ContentTypeProviderOptions are not supported yet.
#>
function Add-KrStaticFilesMiddleware {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Builder.StaticFileOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [string]$RootPath,

        [Parameter(ParameterSetName = 'Items')]
        [string]$RequestPath,

        [Parameter(ParameterSetName = 'Items')]
        [Microsoft.AspNetCore.Http.Features.HttpsCompressionMode]$HttpsCompression,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$ServeUnknownFileTypes,

        [Parameter(ParameterSetName = 'Items')]
        [string]$DefaultContentType,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$RedirectToAppendTrailingSlash,

        [Parameter(ParameterSetName = 'Items')]
        [hashtable]$ContentTypeMap,

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
    }
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            $Options = [Microsoft.AspNetCore.Builder.StaticFileOptions]::new()
            if (-not [string]::IsNullOrEmpty($RootPath)) {
                $resolvedPath = Resolve-KrPath $RootPath -KestrunRoot
                $Options.FileProvider = [Microsoft.Extensions.FileProviders.PhysicalFileProvider]::new($resolvedPath)
            }
            if (-not [string]::IsNullOrEmpty($RequestPath)) {
                $Options.RequestPath = [Microsoft.AspNetCore.Http.PathString]::new($RequestPath.TrimEnd('/'))
            }
            if ($ServeUnknownFileTypes.IsPresent) {
                $Options.ServeUnknownFileTypes = $true
            }
            if ($PSBoundParameters.ContainsKey('HttpsCompression')) {
                $Options.HttpsCompression = $HttpsCompression
            }
            if (-not [string]::IsNullOrEmpty($DefaultContentType)) {
                $Options.DefaultContentType = $DefaultContentType
            }
            if ($RedirectToAppendTrailingSlash.IsPresent) {
                $Options.RedirectToAppendTrailingSlash = $true
            }
            if ($ContentTypeMap) {
                $provider = [Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider]::new()
                foreach ($k in $ContentTypeMap.Keys) {
                    $ext = if ($k -like ".*") { $k } else { ".$k" }
                    $mime = [string]$ContentTypeMap[$k]
                    if ([string]::IsNullOrWhiteSpace($mime)) { continue }
                    $provider.Mappings[$ext] = $mime
                }
                $Options.StaticFileOptions.ContentTypeProvider = $provider
            }
        }
        if ($PSBoundParameters.ContainsKey('NoCache') -or ($PSBoundParameters.ContainsKey('NoStore')) -or ($PSBoundParameters.ContainsKey('MaxAge')) -or
            ($PSBoundParameters.ContainsKey('SharedMaxAge')) -or ($PSBoundParameters.ContainsKey('MaxStale')) -or
            ($PSBoundParameters.ContainsKey('MaxStaleLimit')) -or ($PSBoundParameters.ContainsKey('MinFresh')) -or
            ($PSBoundParameters.ContainsKey('NoTransform')) -or ($PSBoundParameters.ContainsKey('OnlyIfCached')) -or
            ($PSBoundParameters.ContainsKey('Public')) -or ($PSBoundParameters.ContainsKey('Private')) -or
            ($PSBoundParameters.ContainsKey('MustRevalidate')) -or ($PSBoundParameters.ContainsKey('ProxyRevalidate'))
        ) {
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
            # Add the static file service to the server with caching options
            [Kestrun.Hosting.KestrunHostStaticFilesExtensions]::AddStaticFiles($Server, $Options, $cacheControl) | Out-Null
        } else {
            # Add the static file service to the server without caching options
            [Kestrun.Hosting.KestrunHostStaticFilesExtensions]::AddStaticFiles($Server, $Options) | Out-Null
        }

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}

