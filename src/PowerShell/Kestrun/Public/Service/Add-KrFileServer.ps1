<#
    .SYNOPSIS
        Registers a file server to serve static files from a specified path.
    .DESCRIPTION
        This cmdlet allows you to serve static files from a specified path using the Kestrun server.
        It can be used to serve files like images, stylesheets, and scripts.
    .PARAMETER Server
        The Kestrun server instance to which the file server will be added.
    .PARAMETER Options
        The FileServerOptions to configure the file server.
    .PARAMETER RootPath
        The root path from which to serve files.
    .PARAMETER RequestPath
        The path at which the file server will be registered.
    .PARAMETER EnableDirectoryBrowsing
        If specified, enables directory browsing for the file server.
    .PARAMETER RedirectToAppendTrailingSlash
        If specified, requests to the path will be redirected to append a trailing slash.
    .PARAMETER PassThru
        If specified, the cmdlet will return the modified server instance.
    .EXAMPLE
        $server | Add-KrFileServer -RequestPath '/files' -EnableDirectoryBrowsing
        This example adds a file server to the server for the path '/files', enabling directory browsing.
        The file server will use the default options for serving static files.
    .EXAMPLE
        $server | Add-KrFileServer -Options $options
        This example adds a file server to the server using the specified FileServerOptions.
    .LINK
        https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.fileserveroptions?view=aspnetcore-8.0
#>
function Add-KrFileServer {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Builder.FileServerOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [string]$RootPath,

        [Parameter(ParameterSetName = 'Items')]
        [string]$RequestPath,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$EnableDirectoryBrowsing,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$RedirectToAppendTrailingSlash,

        [Parameter()]
        [switch]$PassThru
    )
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            $Options = [Microsoft.AspNetCore.Builder.FileServerOptions]::new()

            if (-not [string]::IsNullOrEmpty($RequestPath)) {
                $Options.RequestPath = [Microsoft.AspNetCore.Http.PathString]::new($RequestPath)
            }
            if (-not [string]::IsNullOrEmpty($RootPath)) {
                $resolvedPath = Resolve-KrPath $RootPath -KestrunRoot
                $Options.FileProvider = [Microsoft.Extensions.FileProviders.PhysicalFileProvider]::new($resolvedPath)      
            }
            if ($EnableDirectoryBrowsing.IsPresent) {
                $Options.EnableDirectoryBrowsing = $true
            }
            if ($RedirectToAppendTrailingSlash.IsPresent) {
                $Options.RedirectToAppendTrailingSlash = $true
            }
        }
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        # Add the file server to the server
        # Use the KestrunHostStaticFilesExtensions to add the file server
        [Kestrun.Hosting.KestrunHostStaticFilesExtensions]::AddFileServer($Server, $Options) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}