<#
    22.15 File hash upload (OpenAPI)

    Client example (PowerShell):
        $filePath = Join-Path $env:TEMP 'upload.bin'
        [System.IO.File]::WriteAllBytes($filePath, (1..255))
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $stream = [System.IO.File]::OpenRead($filePath)
        $fileContent = [System.Net.Http.StreamContent]::new($stream)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/octet-stream')
        $content.Add($fileContent, 'file', (Split-Path $filePath -Leaf))
        $resp = $client.PostAsync("http://127.0.0.1:$Port/upload-hash", $content).Result
        $resp.Content.ReadAsStringAsync().Result
        $stream.Dispose()
        $content.Dispose()
        $client.Dispose()

    Cleanup:
        Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.15-file-hash-openapi')
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.15'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Uploads 22.15 - File hash (OpenAPI)' `
    -Version '1.0.0' `
    -Description 'Upload a binary file and return MD5/SHA1/SHA256/SHA384/SHA512 hashes with OpenAPI annotations.'

Add-KrOpenApiContact -Email 'support@example.com'

# =========================================================
#               FORM OPTION DEFINITION
# =========================================================
# Set default upload path for form parts
$scriptName = [System.IO.Path]::GetFileNameWithoutExtension($PSCommandPath)
$uploadRoot = Join-Path ([System.IO.Path]::GetTempPath()) "kestrun-uploads-$scriptName"
Set-KrServerOptions -DefaultUploadPath $uploadRoot

[OpenApiSchemaComponent(Description = 'File hash upload form', AdditionalPropertiesAllowed = $false)]
[KrBindForm(MaxRequestBodyBytes = (600MB), MaxPartBodyBytes = (600MB), MaxDecompressedBytesPerPart = (600MB))]  # 600 MB
class FileHashUpload {
    [KrPart(Required = $true, ContentTypes = 'application/octet-stream', AllowMultiple = $false, MaxBytes = (600MB))]
    [OpenApiProperty(Description = 'Binary file to hash', Format = 'binary')]
    [Kestrun.Forms.KrFilePart] $file
}

[OpenApiSchemaComponent(Description = 'File hash response')]
class FileHashResponse {
    [OpenApiProperty(Description = 'Original filename')]
    [string] $fileName

    [OpenApiProperty(Description = 'File content type')]
    [string] $contentType

    [OpenApiProperty(Description = 'File size in bytes')]
    [long] $size

    [OpenApiProperty(Description = 'SHA1 hash (hex)')]
    [string] $sha1

    [OpenApiProperty(Description = 'SHA256 hash (hex)')]
    [string] $sha256

    [OpenApiProperty(Description = 'SHA384 hash (hex)')]
    [string] $sha384

    [OpenApiProperty(Description = 'SHA512 hash (hex)')]
    [string] $sha512

    [OpenApiProperty(Description = 'MD5 hash (hex)')]
    [string] $md5
}

Enable-KrConfiguration

<#
.SYNOPSIS
    Upload binary file and return hashes.
.DESCRIPTION
    Accepts multipart/form-data with a single binary file and returns MD5/SHA1 hashes.
.PARAMETER FormPayload
    The parsed multipart/form-data payload.
#>
function uploadHashOpenApi {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingBrokenHashAlgorithms', '')]
    [OpenApiPath(HttpVerb = 'post', Pattern = '/upload-hash')]
    [OpenApiResponse(StatusCode = '200', Description = 'File hash metadata', Schema = [FileHashResponse], ContentType = 'application/json')]

    param(
        [OpenApiRequestBody(ContentType = ('multipart/form-data'), Required = $true)]
        [FileHashUpload] $FormPayload
    )

    if ($null -eq $FormPayload.file) {
        Write-KrJsonResponse -InputObject @{ error = 'file part missing' } -StatusCode 400
        return
    }

    $file = $FormPayload.file
    $sha1 = (Get-FileHash -Algorithm SHA1 -Path $file.TempPath).Hash
    $sha256 = (Get-FileHash -Algorithm SHA256 -Path $file.TempPath).Hash
    $sha384 = (Get-FileHash -Algorithm SHA384 -Path $file.TempPath).Hash
    $sha512 = (Get-FileHash -Algorithm SHA512 -Path $file.TempPath).Hash
    $md5 = (Get-FileHash -Algorithm MD5 -Path $file.TempPath).Hash

    $response = [FileHashResponse]@{
        fileName = $file.OriginalFileName
        contentType = $file.ContentType
        size = $file.Length
        sha1 = $sha1
        sha256 = $sha256
        sha384 = $sha384
        sha512 = $sha512
        md5 = $md5
    }

    Write-KrJsonResponse -InputObject $response -StatusCode 200
}

# =========================================================
#                OPENAPI DOC ROUTE / UI
# =========================================================

Add-KrOpenApiRoute -SpecVersion OpenApi3_2

Add-KrApiDocumentationRoute -DocumentType Swagger -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Redoc -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Elements -OpenApiEndpoint '/openapi/v3.2/openapi.json'

# Start the server asynchronously
Start-KrServer
