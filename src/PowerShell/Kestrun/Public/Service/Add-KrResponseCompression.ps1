<#
    .SYNOPSIS
        Adds response compression to the server.
    .DESCRIPTION
        This cmdlet allows you to configure response compression for the Kestrun server.
        It can be used to compress responses using various algorithms like Gzip, Brotli, etc.
    .PARAMETER Server
        The Kestrun server instance to which the response compression will be added.
    .PARAMETER Options
        The ResponseCompressionOptions to configure the response compression.
    .PARAMETER EnableForHttps
        If specified, enables response compression for HTTPS requests.
    .PARAMETER MimeTypes
        An array of MIME types to compress. If not specified, defaults to common text-based MIME types.
    .PARAMETER ExcludedMimeTypes
        An array of MIME types to exclude from compression.
    .PARAMETER PassThru
        If specified, the cmdlet will return the modified server instance.
    .EXAMPLE
        $server | Add-KrResponseCompression -EnableForHttps -MimeTypes 'text/plain', 'application/json' -ExcludedMimeTypes 'image/*' -Providers $gzipProvider, $brotliProvider
        This example adds response compression to the server, enabling it for HTTPS requests, and specifying the MIME types to compress and exclude, as well as the compression providers to use.
    .EXAMPLE
        $server | Add-KrResponseCompression -Options $options
        This example adds response compression to the server using the specified ResponseCompressionOptions.
    .LINK
        https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.responsecompression.responsecompressionoptions?view=aspnetcore-8.0
    .NOTES
        This cmdlet is used to configure response compression for the Kestrun server, allowing you to specify which MIME types should be compressed and which should be excluded.
        Providers is not supported yet.
#>
function Add-KrResponseCompression {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.ResponseCompression.ResponseCompressionOptions]$Options,
        [Parameter(ParameterSetName = 'Items')]
        [switch]$EnableForHttps,
        [Parameter(ParameterSetName = 'Items')]
        [string[]]$MimeTypes,
        [Parameter(ParameterSetName = 'Items')]
        [string[]]$ExcludedMimeTypes,
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
            $Options = [Microsoft.AspNetCore.ResponseCompression.ResponseCompressionOptions]::new()
            if ($null -ne $MimeTypes -and $MimeTypes.Count -gt 0) {
                $Options.MimeTypes = $MimeTypes
            }
            if ($null -ne $ExcludedMimeTypes -and $ExcludedMimeTypes.Count -gt 0) {
                $Options.ExcludedMimeTypes = $ExcludedMimeTypes
            }
            if ($EnableForHttps.IsPresent) {
                $Options.EnableForHttps = $true
            }
            # Providers are not supported yet
            <# if ($null -ne $Providers -and $Providers.Count -gt 0) {
                foreach ($Provider in $Providers) {
                    $Options.Providers.Add($Provider)
                }
            }#>
        }

        [Kestrun.Hosting.KestrunHttpMiddlewareExtensions]::AddResponseCompression($Server, $Options) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}

