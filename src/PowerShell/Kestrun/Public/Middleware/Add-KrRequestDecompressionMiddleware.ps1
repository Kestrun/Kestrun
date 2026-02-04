<#
.SYNOPSIS
    Adds request decompression middleware to the server.
.DESCRIPTION
    Enables ASP.NET Core RequestDecompression middleware so Content-Encoding
    (gzip/deflate/br) request bodies are transparently decompressed before
    Kestrun parses form payloads.
.PARAMETER AllowedEncoding
    The allowed request encodings (gzip, deflate, br). If omitted, defaults are used.
.EXAMPLE
    $server | Add-KrRequestDecompressionMiddleware -AllowedEncoding gzip, br
.EXAMPLE
    Add-KrRequestDecompressionMiddleware -AllowedEncoding gzip
#>
function Add-KrRequestDecompressionMiddleware {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [string[]]$AllowedEncoding,
        [switch]$PassThru
    )

    $Server = Resolve-KestrunServer

    if ($PSBoundParameters.ContainsKey('AllowedEncoding')) {
        [Kestrun.Hosting.Compression.KrRequestDecompressionExtensions]::AddRequestDecompression($Server, $AllowedEncoding) | Out-Null
    } else {
        [Kestrun.Hosting.Compression.KrRequestDecompressionExtensions]::AddRequestDecompression($Server) | Out-Null
    }
}
