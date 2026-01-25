<#!
    22.1 Basic multipart/form-data upload

    Client example (PowerShell):
        $client = [System.Net.Http.HttpClient]::new()
        try {
            $content = [System.Net.Http.MultipartFormDataContent]::new()
            try {
                $content.Add([System.Net.Http.StringContent]::new('Hello from client'), 'note')
                $bytes = [System.Text.Encoding]::UTF8.GetBytes('sample file')
                $fileContent = [System.Net.Http.ByteArrayContent]::new($bytes)
                $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('text/plain')
                $content.Add($fileContent, 'file', 'hello.txt')
                $resp = $client.PostAsync("http://127.0.0.1:$Port/upload", $content).Result
                $resp.Content.ReadAsStringAsync().Result
            } finally { $content.Dispose() }
        } finally { $client.Dispose() }

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

$uploadRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'kestrun-uploads-22.1-basic-multipart'
$options = [Kestrun.Forms.KrFormOptions]::new()
$options.DefaultUploadPath = $uploadRoot
$options.ComputeSha256 = $true

# Add Rules
$rule = [Kestrun.Forms.KrPartRule]::new()
$rule.Name = 'file'
$rule.Required = $true
$rule.AllowMultiple = $false
$rule.AllowedContentTypes.Add('text/plain')
$options.Rules.Add($rule)


Add-KrFormRoute -Pattern '/upload' -Options $options -ScriptBlock {
    $files = foreach ($entry in $FormPayload.Files.GetEnumerator()) {
        foreach ($file in $entry.Value) {
            [pscustomobject]@{
                name = $file.Name
                fileName = $file.OriginalFileName
                contentType = $file.ContentType
                length = $file.Length
                sha256 = $file.Sha256
            }
        }
    }
    $fields = @{}
    foreach ($key in $FormPayload.Fields.Keys) {
        $fields[$key] = $FormPayload.Fields[$key]
    }
    Write-KrJsonResponse -InputObject @{ fields = $fields; files = $files } -StatusCode 200
}

Enable-KrConfiguration

# =========================================================
#                OPENAPI DOC ROUTE / UI
# =========================================================

Add-KrOpenApiRoute -SpecVersion OpenApi3_2

Add-KrApiDocumentationRoute -DocumentType Swagger -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Redoc -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Elements -OpenApiEndpoint '/openapi/v3.2/openapi.json'

# Start the server asynchronously
Start-KrServer
