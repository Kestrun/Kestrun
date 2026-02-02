<#!
    22.9 multipart/form-data with multiple files under the same field name using OpenAPI

    Client example (PowerShell):
        $Port = 5000
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $content.Add([System.Net.Http.StringContent]::new('Batch upload'),'note')
        $file1 = [System.Net.Http.ByteArrayContent]::new([System.Text.Encoding]::UTF8.GetBytes('file-1'))
        $file1.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
        $content.Add($file1,'files','one.txt')
        $file2 = [System.Net.Http.ByteArrayContent]::new([System.Text.Encoding]::UTF8.GetBytes('file-2'))
        $file2.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
        $content.Add($file2,'files','two.txt')
        $resp = $client.PostAsync("http://127.0.0.1:$Port/upload", $content).Result
        $resp.Content.ReadAsStringAsync().Result

    Cleanup:
        Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.2-multiple-files')
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.2'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Uploads 22.2 - Multiple Files' `
    -Version '1.0.0' `
    -Description 'Multipart/form-data upload with multiple files using Add-KrFormRoute.'

Add-KrOpenApiContact -Email 'support@example.com'

# Set default upload path for form parts
$uploadRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.2-multiple-files'
Set-KrServerOptions -DefaultUploadPath $uploadRoot

[OpenApiSchemaComponent(Description = 'Multiple files upload form')]
[KrBindForm(ComputeSha256 = $true)]
class FilesUpload {
    [KrPart(Required = $true, ContentTypes = 'text/plain', AllowMultiple = $true)]
    [OpenApiProperty(Description = 'Files to upload', Format = 'binary')]
    [Kestrun.Forms.KrFilePart[]] $Files

    [KrPart(Required = $false)]
    [OpenApiProperty(Description = 'Optional note field')]
    [string] $note
}

<#
.SYNOPSIS
    Upload route for multiple files using OpenAPI annotations.
.DESCRIPTION
    This function defines an upload route that accepts multiple files under the same field name using OpenAPI annotations.
    It responds with a JSON object containing details about the uploaded files.
.PARAMETER FormPayload
    The form data payload containing the uploaded files.
#>
function upload {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/upload')]
    [OpenApiResponse(  StatusCode = '200', Description = 'Parsed fields and files', ContentType = 'application/json')]

    param(
        [OpenApiRequestBody(contentType = ('multipart/form-data'), Required = $true)]
        [FilesUpload] $FormPayload
    )
    $files = @($FormPayload.Files)
    $result = [pscustomobject]@{
        count = $files.Count
        files = $files | ForEach-Object {
            [pscustomobject]@{
                fileName = $_.OriginalFileName
                length = $_.Length
                sha256 = $_.Sha256
            }
        }
    }
    Write-KrJsonResponse -InputObject $result -StatusCode 200
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
