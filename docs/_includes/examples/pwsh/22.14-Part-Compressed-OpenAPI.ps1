<#!
    22.14-Part-Compressed-OpenAPI with part-level compression and OpenAPI (optional feature)

    Client example (PowerShell):
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $raw = [System.Text.Encoding]::UTF8.GetBytes('compressed-part')
        $ms = [System.IO.MemoryStream]::new()
        $gzip = [System.IO.Compression.GZipStream]::new($ms, [System.IO.Compression.CompressionMode]::Compress, $true)
        $gzip.Write($raw, 0, $raw.Length)
        $gzip.Dispose()
        $compressed = $ms.ToArray()
        $part = [System.Net.Http.ByteArrayContent]::new($compressed)
        $part.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
        $part.Headers.ContentEncoding.Add('gzip')
        $content.Add($part,'file','payload.txt')
        $resp = $client.PostAsync("http://127.0.0.1:$Port/part-compressed", $content).Result
        $resp.Content.ReadAsStringAsync().Result

    Cleanup:
        Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.14-part-compressed-openapi')
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.14-Part-Compressed-OpenAPI'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Uploads 22.14-Part-Compressed-OpenAPI' `
    -Version '1.0.0' `
    -Description 'Per-part compression + multipart parsing using Add-KrFormRoute.'

Add-KrOpenApiContact -Email 'support@example.com'

$uploadRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.14-part-compressed-openapi'

# Add Rules
New-KrFormPartRule -Name 'file' -Required -AllowOnlyOne -AllowedContentTypes 'text/plain' |
    Add-KrFormOption -Name 'PartCompressed' -DefaultUploadPath $uploadRoot -ComputeSha256 -EnablePartDecompression -MaxDecompressedBytesPerPart (1024 * 1024)


<#
.SYNOPSIS
    OpenAPI route function for /part-compressed
.DESCRIPTION
    OpenAPI route function for /part-compressed with KrBindForm and OpenAPI attributes
.PARAMETER FormPayload
    The parsed multipart/form-data payload.
#>
function partcompressed {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/part-compressed')]
    [KrBindForm(Template = 'PartCompressed')]
    [OpenApiResponse(  StatusCode = '200', Description = 'Parsed fields and files', ContentType = 'application/json')]

    param(
        [OpenApiRequestBody(contentType = ('multipart/form-data'), Required = $true)]
        [KrFormData] $FormPayload
    )
    $file = $FormPayload.Files['file'][0]
    Write-KrJsonResponse -InputObject @{ fileName = $file.OriginalFileName; length = $file.Length; sha256 = $file.Sha256 } -StatusCode 200
}

Enable-KrConfiguration

# =========================================================
#                OPENAPI DOC ROUTE / UI
# =========================================================

Add-KrOpenApiRoute -SpecVersion OpenApi3_2

Add-KrApiDocumentationRoute -DocumentType Swagger -OpenApiEndpoint '/openapi/v3.2/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Redoc -OpenApiEndpoint '/openapi/v3.2/openapi.json'

# Start the server asynchronously
Start-KrServer
