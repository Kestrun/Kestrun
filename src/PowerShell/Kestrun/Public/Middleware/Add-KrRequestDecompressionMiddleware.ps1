<#
    .SYNOPSIS
        Adds request decompression middleware to the server.
    
    .DESCRIPTION
        This cmdlet enables the server to automatically decompress incoming request bodies
        that are compressed with gzip, deflate, or Brotli encoding.
        
        This middleware MUST be added before any routes that need to parse compressed request bodies
        (e.g., form uploads with Content-Encoding: gzip).
        
        For part-level decompression within multipart sections, use the EnablePartDecompression
        option in form parsing instead.
    
    .PARAMETER Server
        The Kestrun server instance to which request decompression will be added.
    
    .PARAMETER PassThru
        If specified, the cmdlet will return the modified server instance.
    
    .EXAMPLE
        $server | Add-KrRequestDecompressionMiddleware
        
        Adds request decompression middleware with default settings (gzip, deflate, brotli).
    
    .EXAMPLE
        $server = New-KrServer -Name 'MyServer' | Add-KrRequestDecompressionMiddleware -PassThru
        
        Creates a server and adds request decompression, returning the server instance.
    
    .NOTES
        This enables ASP.NET Core's RequestDecompression middleware.
        Supported encodings: gzip, deflate, br (Brotli).
        
        For request-level compression (Content-Encoding header), this middleware is required.
        For per-part compression within multipart bodies, enable part decompression in form options.
    
    .LINK
        https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/request-decompression
#>
function Add-KrRequestDecompressionMiddleware {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        
        [Parameter()]
        [switch]$PassThru
    )
    
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    
    process {
        # Add request decompression services
        [Kestrun.Forms.KrRequestDecompressionExtensions]::AddKestrunRequestDecompression(
            $Server.ServiceCollection,
            $null
        ) | Out-Null
        
        # Use request decompression middleware
        [Kestrun.Forms.KrRequestDecompressionExtensions]::UseKestrunRequestDecompression(
            $Server.ApplicationBuilder
        ) | Out-Null
        
        if ($PassThru.IsPresent) {
            return $Server
        }
    }
}
