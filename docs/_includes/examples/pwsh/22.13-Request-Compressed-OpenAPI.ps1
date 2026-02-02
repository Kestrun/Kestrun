<#!
    22.13 Upload with request-level compression with OpenAPI (RequestDecompression middleware)

    Client example (PowerShell):
        $boundary = 'req-boundary'
        $body = @(
            "--$boundary",
            "Content-Disposition: form-data; name=note",
            "",
            "compressed",
            "--$boundary",
            "Content-Disposition: form-data; name=file; filename=hello.txt",
            "Content-Type: text/plain",
            "",
            "hello",
            "--$boundary--",
            ""
        ) -join "`r`n"
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
        $ms = [System.IO.MemoryStream]::new()
        $gzip = [System.IO.Compression.GZipStream]::new($ms, [System.IO.Compression.CompressionMode]::Compress, $true)
        $gzip.Write($bytes, 0, $bytes.Length)
        $gzip.Dispose()
        $compressed = $ms.ToArray()
        Invoke-WebRequest -Method Post -Uri "http://127.0.0.1:$Port/upload" -ContentType "multipart/form-data; boundary=$boundary" -Headers @{ 'Content-Encoding'='gzip' } -Body $compressed

    Cleanup:
        Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.13-request-compressed')
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.13'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Uploads 22.13 - Request Compressed' `
    -Version '1.0.0' `
    -Description 'Request-level compression (RequestDecompression middleware) + multipart parsing using Add-KrFormRoute.'

Add-KrOpenApiContact -Email 'support@example.com'

Add-KrRequestDecompressionMiddleware -AllowedEncoding gzip | Out-Null

# Set default upload path for form parts
$uploadRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.13-request-compressed'
Set-KrServerOptions -DefaultUploadPath $uploadRoot

[OpenApiSchemaComponent(Description = 'Request-compressed upload form')]
[KrBindForm(ComputeSha256 = $true)]
class RequestCompressedUpload {
    [KrPart(Required = $true)]
    [OpenApiProperty(Description = 'Required note field')]
    [string] $note

    [KrPart(Required = $true, ContentTypes = 'text/plain', AllowMultiple = $false)]
    [OpenApiProperty(Description = 'The file to upload', Format = 'binary')]
    [Kestrun.Forms.KrFilePart] $file
}

<#
.SYNOPSIS
    Upload endpoint for request-compressed multipart/form-data
.DESCRIPTION
    Upload endpoint for request-compressed multipart/form-data
    using OpenAPI documentation and Add-KrFormRoute.
.PARAMETER FormPayload
    The parsed multipart/form-data payload.
#>
function upload {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/upload')]
    [OpenApiResponse(  StatusCode = '200', Description = 'Parsed fields and files', ContentType = 'application/json')]
    param(
        [OpenApiRequestBody(contentType = ('multipart/form-data'), Required = $true)]
        [RequestCompressedUpload] $FormPayload
    )
    $file = $FormPayload.file
    $files = if ($null -ne $file) { @($file) } else { @() }
    Write-KrJsonResponse -InputObject @{ count = $files.Count; files = $files } -StatusCode 200
}

Enable-KrConfiguration

# =========================================================
#                OPENAPI DOC ROUTE / UI
# =========================================================

Add-KrOpenApiRoute

Add-KrApiDocumentationRoute -DocumentType Swagger -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Redoc -OpenApiEndpoint '/openapi/v3.1/openapi.json'

# Start the server asynchronously
Start-KrServer -CloseLogsOnExit
