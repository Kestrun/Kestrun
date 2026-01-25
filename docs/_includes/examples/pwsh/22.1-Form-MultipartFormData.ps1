<#
.SYNOPSIS
    Tutorial 22.1 - Form Parsing: multipart/form-data File Upload

.DESCRIPTION
    Demonstrates parsing multipart/form-data requests with file uploads and text fields.
    Supports multiple files per field and computes SHA-256 hashes.

.EXAMPLE
    # Start the server
    pwsh ./22.1-Form-MultipartFormData.ps1
    
    # In another terminal, upload files:
    $files = @(
        @{Name='files'; FileName='test1.txt'; ContentType='text/plain'; Content=[System.Text.Encoding]::UTF8.GetBytes('Hello World 1')}
        @{Name='files'; FileName='test2.txt'; ContentType='text/plain'; Content=[System.Text.Encoding]::UTF8.GetBytes('Hello World 2')}
    )
    
    $multipart = [System.Net.Http.MultipartFormDataContent]::new()
    $multipart.Add([System.Net.Http.StringContent]::new('testuser'), 'username')
    $multipart.Add([System.Net.Http.StringContent]::new('A test upload'), 'description')
    foreach ($file in $files) {
        $content = [System.Net.Http.ByteArrayContent]::new($file.Content)
        $content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse($file.ContentType)
        $multipart.Add($content, $file.Name, $file.FileName)
    }
    
    $client = [System.Net.Http.HttpClient]::new()
    $response = $client.PostAsync('http://localhost:5000/upload', $multipart).Result
    $response.Content.ReadAsStringAsync().Result
#>

try {
    $scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
    $kestrunModulePath = Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $scriptPath))) 'src' 'PowerShell' 'Kestrun' 'Kestrun.psm1'
    
    if (Test-Path $kestrunModulePath) {
        Import-Module $kestrunModulePath -Force -ErrorAction Stop
    } else {
        Import-Module Kestrun -MaximumVersion 2.99 -ErrorAction Stop
    }
} catch {
    Write-Error "Failed to import Kestrun module: $_"
    exit 1
}

# Create upload directory
$uploadDir = Join-Path $scriptPath 'uploads'
if (-not (Test-Path $uploadDir)) {
    New-Item -ItemType Directory -Path $uploadDir | Out-Null
}

# Setup logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'FormLogger' -SetAsDefault

# Create server
$server = New-KrServer -Name 'Form Upload Demo'

# Add endpoint
$server | Add-KrEndpoint -Port 5000 -Protocol Http

# Add form upload route
$server | Add-KrFormRoute -Pattern '/upload' `
    -DefaultUploadPath $uploadDir `
    -ComputeSha256 `
    -MaxPartBodyBytes 50MB `
    -ScriptBlock {
    
    $payload = $FormContext.Payload
    
    if ($payload.PayloadType -ne 'NamedParts') {
        Write-KrJsonResponse @{ error = 'Expected named parts payload' } -StatusCode 400
        return
    }
    
    # Extract fields
    $fields = @{}
    foreach ($kvp in $payload.Fields.GetEnumerator()) {
        $fields[$kvp.Key] = $kvp.Value
    }
    
    # Extract files
    $fileMetadata = @()
    foreach ($kvp in $payload.Files.GetEnumerator()) {
        $fieldName = $kvp.Key
        foreach ($file in $kvp.Value) {
            $fileMetadata += @{
                fieldName = $fieldName
                originalFileName = $file.OriginalFileName
                contentType = $file.ContentType
                size = $file.Length
                sha256 = $file.Sha256
                tempPath = $file.TempPath
            }
        }
    }
    
    Write-KrJsonResponse @{
        success = $true
        fields = $fields
        files = $fileMetadata
        totalFiles = $fileMetadata.Count
    }
}

# Add simple test endpoint
$server | Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    $html = @'
<!DOCTYPE html>
<html>
<head><title>File Upload Test</title></head>
<body>
    <h1>File Upload Test</h1>
    <form method="post" action="/upload" enctype="multipart/form-data">
        <p>
            <label>Username: <input type="text" name="username" value="testuser" /></label>
        </p>
        <p>
            <label>Description: <input type="text" name="description" value="Test upload" /></label>
        </p>
        <p>
            <label>Files: <input type="file" name="files" multiple /></label>
        </p>
        <p>
            <button type="submit">Upload</button>
        </p>
    </form>
</body>
</html>
'@
    Write-KrHtmlResponse $html
}

# Enable and start
Write-Host 'Starting server on http://localhost:5000'
Write-Host 'Navigate to http://localhost:5000 to test file uploads'
Write-Host "Upload directory: $uploadDir"
Write-Host 'Press Ctrl+C to stop'

$server | Enable-KrConfiguration | Start-KrServer
