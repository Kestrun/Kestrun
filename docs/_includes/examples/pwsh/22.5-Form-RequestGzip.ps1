<#
.SYNOPSIS
    Tutorial 22.5 - Form Parsing: Request-Level Gzip Compression

.DESCRIPTION
    Demonstrates handling gzip-compressed request bodies using the RequestDecompression middleware.
    The entire request body (multipart/form-data) is compressed at the HTTP level with Content-Encoding: gzip.

.EXAMPLE
    # Start the server
    pwsh ./22.5-Form-RequestGzip.ps1
    
    # The server handles compressed uploads automatically when the middleware is enabled
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
$server = New-KrServer -Name 'Request Gzip Demo'

# Add endpoint
$server | Add-KrEndpoint -Port 5000 -Protocol Http

# IMPORTANT: Add request decompression middleware BEFORE form routes
$server | Add-KrRequestDecompressionMiddleware

# Add form upload route
$server | Add-KrFormRoute -Pattern '/upload' `
    -DefaultUploadPath $uploadDir `
    -ComputeSha256 `
    -ScriptBlock {
    
    $payload = $FormContext.Payload
    
    if ($payload.PayloadType -ne 'NamedParts') {
        Write-KrJsonResponse @{ error = 'Expected named parts payload' } -StatusCode 400
        return
    }
    
    # Extract files
    $fileMetadata = @()
    foreach ($kvp in $payload.Files.GetEnumerator()) {
        foreach ($file in $kvp.Value) {
            $fileMetadata += @{
                fieldName = $kvp.Key
                originalFileName = $file.OriginalFileName
                size = $file.Length
                sha256 = $file.Sha256
            }
        }
    }
    
    Write-KrJsonResponse @{
        success = $true
        message = 'Request decompressed and parsed successfully'
        files = $fileMetadata
        totalFiles = $fileMetadata.Count
        fields = $payload.Fields.Keys
    }
}

# Add test page
$server | Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    $html = @'
<!DOCTYPE html>
<html>
<head>
    <title>Request Gzip Test</title>
    <script src="https://cdn.jsdelivr.net/npm/pako@2.1.0/dist/pako.min.js"></script>
    <script>
        async function sendCompressed() {
            const formData = new FormData();
            formData.append('username', document.getElementById('username').value);
            formData.append('file', document.getElementById('file').files[0]);
            
            // Build multipart body manually
            const boundary = 'boundary-' + Date.now();
            let body = '';
            
            body += `--${boundary}\r\n`;
            body += `Content-Disposition: form-data; name="username"\r\n\r\n`;
            body += document.getElementById('username').value + '\r\n';
            
            if (document.getElementById('file').files[0]) {
                const file = document.getElementById('file').files[0];
                const fileContent = await file.arrayBuffer();
                body += `--${boundary}\r\n`;
                body += `Content-Disposition: form-data; name="file"; filename="${file.name}"\r\n`;
                body += `Content-Type: ${file.type}\r\n\r\n`;
                body += new TextDecoder().decode(fileContent) + '\r\n';
            }
            
            body += `--${boundary}--\r\n`;
            
            // Compress the body with gzip
            const compressed = pako.gzip(body);
            
            const response = await fetch('/upload', {
                method: 'POST',
                headers: {
                    'Content-Type': `multipart/form-data; boundary=${boundary}`,
                    'Content-Encoding': 'gzip'
                },
                body: compressed
            });
            
            const result = await response.json();
            document.getElementById('result').textContent = JSON.stringify(result, null, 2);
        }
    </script>
</head>
<body>
    <h1>Request-Level Gzip Compression Test</h1>
    <p>This example compresses the entire multipart/form-data body with gzip before sending.</p>
    <form onsubmit="event.preventDefault(); sendCompressed();">
        <p>
            <label>Username: <input type="text" id="username" value="testuser" /></label>
        </p>
        <p>
            <label>File: <input type="file" id="file" /></label>
        </p>
        <p>
            <button type="submit">Upload (with Gzip)</button>
        </p>
    </form>
    <pre id="result"></pre>
    <p><small>Note: The request body is compressed with Content-Encoding: gzip at the HTTP level.</small></p>
</body>
</html>
'@
    Write-KrHtmlResponse $html
}

Write-Host 'Starting server on http://localhost:5000'
Write-Host 'Navigate to http://localhost:5000 to test gzip-compressed uploads'
Write-Host 'RequestDecompression middleware is enabled'
Write-Host "Upload directory: $uploadDir"
Write-Host 'Press Ctrl+C to stop'

$server | Enable-KrConfiguration | Start-KrServer
