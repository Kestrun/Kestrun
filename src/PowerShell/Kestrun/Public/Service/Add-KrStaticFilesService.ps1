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
    If specified, enables HTTPS compression for the static files.
.PARAMETER ServeUnknownFileTypes
    If specified, allows serving files with unknown MIME types.
.PARAMETER DefaultContentType
    The default content type to use for files served by the static file service.
.PARAMETER RedirectToAppendTrailingSlash
    If specified, redirects requests to append a trailing slash to the URL.
.PARAMETER ContentTypeMap
    A hashtable mapping file extensions to MIME types.
.PARAMETER PassThru
    If specified, the cmdlet will return the modified server instance after adding the static file service.
.EXAMPLE
    $server | Add-KrStaticFilesService -RequestPath '/static' -HttpsCompression -ServeUnknownFileTypes -DefaultContentType 'application/octet-stream' -RedirectToAppendTrailingSlash
    This example adds a static file service to the server for the path '/static', enabling HTTPS compression, allowing serving unknown file types,
    setting the default content type to 'application/octet-stream', and redirecting requests to append a trailing slash.
.EXAMPLE
    $server | Add-KrStaticFilesService -Options $options
    This example adds a static file service to the server using the specified StaticFileOptions.
.LINK
    https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.staticfileoptions?view=aspnetcore-8.0
.NOTES
    ContentTypeProvider and ContentTypeProviderOptions are not supported yet.
#>
function Add-KrStaticFilesService {
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
        [switch]$HttpsCompression,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$ServeUnknownFileTypes,

        [Parameter(ParameterSetName = 'Items')]
        [string]$DefaultContentType,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$RedirectToAppendTrailingSlash,

        [Parameter(ParameterSetName = 'Items')]
        [hashtable]$ContentTypeMap,

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
            if ($HttpsCompression.IsPresent) {
                $Options.HttpsCompression = $true
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

        [Kestrun.Hosting.KestrunHostStaticFilesExtensions]::AddStaticFiles($Server, $Options) | Out-Null
        # Add the static file service to the server

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}

