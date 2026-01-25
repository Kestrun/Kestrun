<#
.SYNOPSIS
    Tutorial 22.3 - Form Parsing: multipart/mixed Ordered Parts

.DESCRIPTION
    Demonstrates parsing multipart/mixed requests with ordered parts.
    Unlike multipart/form-data, parts preserve their order and may not have names.

.EXAMPLE
    # Start the server
    pwsh ./22.3-Form-MultipartMixed.ps1
    
    # In another terminal, send a multipart/mixed request:
    $boundary = 'boundary-12345'
    $body = @"
--$boundary
Content-Type: application/json

{"id": 1, "type": "metadata"}
--$boundary
Content-Type: text/plain

This is a plain text part.
--$boundary
Content-Type: application/octet-stream

Binary data here...
--$boundary--
"@
    
    $headers = @{
        'Content-Type' = "multipart/mixed; boundary=$boundary"
    }
    
    Invoke-RestMethod -Uri 'http://localhost:5000/mixed' -Method Post -Body $body -Headers $headers
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

# Create temp directory
$tempDir = Join-Path $scriptPath 'temp'
if (-not (Test-Path $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir | Out-Null
}

# Setup logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'FormLogger' -SetAsDefault

# Create server
$server = New-KrServer -Name 'Multipart Mixed Demo'

# Add endpoint
$server | Add-KrEndpoint -Port 5000 -Protocol Http

# Add multipart/mixed route
$server | Add-KrFormRoute -Pattern '/mixed' `
    -DefaultUploadPath $tempDir `
    -ScriptBlock {
    
    $payload = $FormContext.Payload
    
    if ($payload.PayloadType -ne 'OrderedParts') {
        Write-KrJsonResponse @{ error = 'Expected ordered parts payload' } -StatusCode 400
        return
    }
    
    # Process ordered parts
    $partsInfo = @()
    $index = 0
    foreach ($part in $payload.Parts) {
        $partInfo = @{
            index = $index++
            name = $part.Name
            contentType = $part.ContentType
            size = $part.Length
            tempPath = $part.TempPath
        }
        
        # If content type is text/*, read the content
        if ($part.ContentType -like 'text/*' -and $part.Length -lt 10KB) {
            try {
                $partInfo['content'] = [System.IO.File]::ReadAllText($part.TempPath)
            } catch {
                $partInfo['content'] = '(unable to read)'
            }
        }
        
        $partsInfo += $partInfo
    }
    
    Write-KrJsonResponse @{
        success = $true
        partCount = $payload.Parts.Count
        parts = $partsInfo
    }
}

# Add test page
$server | Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    $html = @'
<!DOCTYPE html>
<html>
<head>
    <title>Multipart/Mixed Test</title>
    <script>
        async function sendMixed() {
            const boundary = 'boundary-' + Date.now();
            const body = [
                `--${boundary}`,
                'Content-Type: application/json',
                '',
                '{"id": 1, "type": "metadata"}',
                `--${boundary}`,
                'Content-Type: text/plain',
                '',
                'This is a plain text part.',
                `--${boundary}`,
                'Content-Type: text/html',
                '',
                '<p>HTML content</p>',
                `--${boundary}--`,
                ''
            ].join('\r\n');
            
            const response = await fetch('/mixed', {
                method: 'POST',
                headers: {
                    'Content-Type': `multipart/mixed; boundary=${boundary}`
                },
                body: body
            });
            
            const result = await response.json();
            document.getElementById('result').textContent = JSON.stringify(result, null, 2);
        }
    </script>
</head>
<body>
    <h1>Multipart/Mixed Test</h1>
    <button onclick="sendMixed()">Send Multipart/Mixed Request</button>
    <pre id="result"></pre>
</body>
</html>
'@
    Write-KrHtmlResponse $html
}

Write-Host 'Starting server on http://localhost:5000'
Write-Host 'Navigate to http://localhost:5000 to test multipart/mixed'
Write-Host "Temp directory: $tempDir"
Write-Host 'Press Ctrl+C to stop'

$server | Enable-KrConfiguration | Start-KrServer
