<#
    .SYNOPSIS
        Adds a form parsing route to the Kestrun server.
    
    .DESCRIPTION
        This cmdlet creates a POST route that automatically parses form data from the request body.
        Supports multiple content types:
        - multipart/form-data (file uploads + fields)
        - application/x-www-form-urlencoded (form fields)
        - multipart/mixed (ordered parts)
        - multipart/related, multipart/byteranges (ordered parts)
        
        The parsed form data is available in the $Context.Items['FormPayload'] within the route handler.
    
    .PARAMETER Server
        The Kestrun server instance to which the route will be added.
    
    .PARAMETER Pattern
        The URL path pattern for the route (e.g., '/upload', '/api/form').
    
    .PARAMETER ScriptBlock
        The script block to execute after form parsing. The parsed form data is available as $FormContext.
    
    .PARAMETER MaxRequestBodyBytes
        Maximum request body size in bytes. Default: 100 MB.
    
    .PARAMETER MaxPartBodyBytes
        Maximum size per part in bytes. Default: 10 MB.
    
    .PARAMETER MaxParts
        Maximum number of parts allowed. Default: 100.
    
    .PARAMETER MaxFieldValueBytes
        Maximum size for text field values in bytes. Default: 1 MB.
    
    .PARAMETER MaxNestingDepth
        Maximum nesting depth for nested multipart sections. Default: 1.
    
    .PARAMETER DefaultUploadPath
        Directory for storing uploaded files. Default: system temp directory.
    
    .PARAMETER ComputeSha256
        If specified, computes SHA-256 hash for each uploaded file.
    
    .PARAMETER EnablePartDecompression
        If specified, enables per-part Content-Encoding decompression (gzip/deflate/br).
        For request-level decompression, use Add-KrRequestDecompressionMiddleware instead.
    
    .PARAMETER MaxDecompressedBytesPerPart
        Maximum decompressed bytes per part (decompression bomb protection). Default: 100 MB.
    
    .PARAMETER PassThru
        If specified, returns the server instance.
    
    .EXAMPLE
        $server | Add-KrFormRoute -Pattern '/upload' -ScriptBlock {
            $payload = $FormContext.Payload
            if ($payload.PayloadType -eq 'NamedParts') {
                $files = $payload.Files
                Write-KrJsonResponse @{
                    fileCount = $files.Count
                    files = $files.Values | ForEach-Object {
                        @{
                            name = $_.OriginalFileName
                            size = $_.Length
                            sha256 = $_.Sha256
                        }
                    }
                }
            }
        }
        
        Adds a file upload route that returns metadata about uploaded files.
    
    .EXAMPLE
        $server | Add-KrFormRoute -Pattern '/form' -ComputeSha256 -DefaultUploadPath './uploads' -ScriptBlock {
            $payload = $FormContext.Payload
            Write-KrJsonResponse @{ fields = $payload.Fields; files = $payload.Files.Keys }
        }
        
        Adds a form route with SHA-256 hashing enabled and custom upload directory.
    
    .NOTES
        The parsed form is available in the script block as $FormContext.
        $FormContext.Payload contains either KrNamedPartsPayload or KrOrderedPartsPayload.
        
        For named parts (multipart/form-data, x-www-form-urlencoded):
        - $FormContext.Payload.Fields: Dictionary<string, string[]>
        - $FormContext.Payload.Files: Dictionary<string, KrFilePart[]>
        
        For ordered parts (multipart/mixed):
        - $FormContext.Payload.Parts: List<KrRawPart>
        
        Uploaded files are stored in temporary files; remember to clean them up or move them.
#>
function Add-KrFormRoute {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        
        [Parameter(Mandatory = $true)]
        [ValidatePattern('^/')]
        [string]$Pattern,
        
        [Parameter(Mandatory = $true)]
        [scriptblock]$ScriptBlock,
        
        [Parameter()]
        [long]$MaxRequestBodyBytes = 100MB,
        
        [Parameter()]
        [long]$MaxPartBodyBytes = 10MB,
        
        [Parameter()]
        [int]$MaxParts = 100,
        
        [Parameter()]
        [long]$MaxFieldValueBytes = 1MB,
        
        [Parameter()]
        [int]$MaxNestingDepth = 1,
        
        [Parameter()]
        [string]$DefaultUploadPath = [System.IO.Path]::GetTempPath(),
        
        [Parameter()]
        [switch]$ComputeSha256,
        
        [Parameter()]
        [switch]$EnablePartDecompression,
        
        [Parameter()]
        [long]$MaxDecompressedBytesPerPart = 100MB,
        
        [Parameter()]
        [switch]$PassThru
    )
    
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    
    process {
        # Create form options
        $options = [Kestrun.Forms.KrFormOptions]::new()
        $options.MaxRequestBodyBytes = $MaxRequestBodyBytes
        $options.MaxPartBodyBytes = $MaxPartBodyBytes
        $options.MaxParts = $MaxParts
        $options.MaxFieldValueBytes = $MaxFieldValueBytes
        $options.MaxNestingDepth = $MaxNestingDepth
        $options.DefaultUploadPath = $DefaultUploadPath
        $options.ComputeSha256 = $ComputeSha256.IsPresent
        $options.EnablePartDecompression = $EnablePartDecompression.IsPresent
        $options.MaxDecompressedBytesPerPart = $MaxDecompressedBytesPerPart
        
        # Create handler that invokes the PowerShell script block
        $handler = {
            param($formContext)
            
            # Make FormContext available in the script block scope
            $FormContext = $formContext
            
            # Execute the user's script block
            $result = & $ScriptBlock
            
            # If the script block didn't return a result, return OK
            if ($null -eq $result) {
                return [Microsoft.AspNetCore.Http.Results]::Ok(@{ success = $true })
            }
            
            return $result
        }.GetNewClosure()
        
        # Register the route using the C# extension method
        $routeBuilder = [Kestrun.Forms.KrFormEndpoints]::MapKestrunFormRoute(
            $Server.WebApplication,
            $Pattern,
            $options,
            $Server.Logger,
            [Func[Kestrun.Forms.KrFormContext, System.Threading.Tasks.Task[Microsoft.AspNetCore.Http.IResult]]]$handler
        )
        
        # Track the route in the server's route registry
        $verb = [Kestrun.Utilities.HttpVerb]::Post
        $routeKey = ($Pattern, $verb)
        if (-not $Server.RegisteredRoutes.ContainsKey($routeKey)) {
            $routeOptions = [Kestrun.Hosting.Options.MapRouteOptions]::new()
            $routeOptions.Pattern = $Pattern
            $routeOptions.HttpVerbs = @($verb)
            $Server.RegisteredRoutes[$routeKey] = $routeOptions
        }
        
        if ($PassThru.IsPresent) {
            return $Server
        }
    }
}
