<#!
    22.8 Basic multipart/form-data upload OpenAPI example

    Client example (PowerShell):
        $client = [System.Net.Http.HttpClient]::new()
        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $content.Add([System.Net.Http.StringContent]::new('Hello from client'), 'note')
        $bytes = [System.Text.Encoding]::UTF8.GetBytes('sample file')
        $fileContent = [System.Net.Http.ByteArrayContent]::new($bytes)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
        $content.Add($fileContent, 'file', 'hello.txt')
        $resp = $client.PostAsync("http://127.0.0.1:$Port/upload", $content).Result
        $resp.Content.ReadAsStringAsync().Result
        $content.Dispose()
        $client.Dispose()

    Cleanup:
        Remove-Item -Recurse -Force (Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.1-basic-multipart')
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Forms 22.1'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress | Out-Null

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Uploads 22.1 - Basic Multipart' `
    -Version '1.0.0' `
    -Description 'Basic multipart/form-data upload example using Add-KrFormRoute.'

Add-KrOpenApiContact -Email 'support@example.com'

# =========================================================
#               FORM OPTION DEFINITION
# =========================================================
# Set default upload path for form parts
$uploadRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.1-basic-multipart'
Set-KrServerOptions -DefaultUploadPath $uploadRoot

[OpenApiSchemaComponent(Description = 'File upload form', AdditionalPropertiesAllowed = $false)]
[KrBindForm(ComputeSha256 = $true)]
class FileUpload {
    [OpenApiProperty(Description = 'Optional note field')]
    [string] $note

    [KrPart(Required = $true, ContentTypes = 'text/plain', AllowMultiple = $false)]
    [OpenApiProperty(Description = 'The file to upload', Format = 'binary')]
    [Kestrun.Forms.KrFilePart] $file
}

Enable-KrConfiguration

<#
.SYNOPSIS
    Upload endpoint for multipart/form-data
.DESCRIPTION
    Handles multipart/form-data uploads with file and field processing.
.PARAMETER FormPayload
    The parsed multipart/form-data payload.
#>
function upload {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/upload')]
    [OpenApiResponse(  StatusCode = '200', Description = 'Parsed fields and files', ContentType = 'application/json')]

    param(
        [OpenApiRequestBody(contentType = ('multipart/form-data'), Required = $true)]
        [FileUpload] $FormPayload
    )
    $files = @()
    if ($null -ne $FormPayload.file) {
        $files = @(
            [pscustomobject]@{
                name = $FormPayload.file.Name
                fileName = $FormPayload.file.OriginalFileName
                contentType = $FormPayload.file.ContentType
                length = $FormPayload.file.Length
                sha256 = $FormPayload.file.Sha256
            }
        )
    }

    $fields = @{}
    if (-not [string]::IsNullOrWhiteSpace($FormPayload.note)) {
        $fields['note'] = @($FormPayload.note)
    }
    Write-KrJsonResponse -InputObject @{ fields = $fields; files = $files } -StatusCode 200
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
