<#!
    22.6 Upload with request-level compression (RequestDecompression middleware)

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
        Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.6-request-compressed')
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.6'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Uploads 22.6 - Request Compressed' `
    -Version '1.0.0' `
    -Description 'Request-level compression (RequestDecompression middleware) + multipart parsing using Add-KrFormRoute.'

Add-KrOpenApiContact -Email 'support@example.com'

Add-KrRequestDecompressionMiddleware -AllowedEncoding gzip | Out-Null

$uploadRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.6-request-compressed'
$options = [Kestrun.Forms.KrFormOptions]::new()
$options.DefaultUploadPath = $uploadRoot
$options.ComputeSha256 = $true

# Add Rules
$fileRule = [Kestrun.Forms.KrPartRule]::new()
$fileRule.Name = 'file'
$fileRule.Required = $true
$fileRule.AllowMultiple = $false
$fileRule.AllowedContentTypes.Add('text/plain')
$options.Rules.Add($fileRule)

$noteRule = [Kestrun.Forms.KrPartRule]::new()
$noteRule.Name = 'note'
$noteRule.Required = $true
$options.Rules.Add($noteRule)

Add-KrFormRoute -Pattern '/upload' -Options $options -ScriptBlock {
    $files = $FormPayload.Files['file']
    Write-KrJsonResponse -InputObject @{ count = $files.Count; files = $files } -StatusCode 200
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
