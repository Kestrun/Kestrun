<#
.SYNOPSIS
    Tutorial 22.4 - Form Parsing: Nested Multipart

.DESCRIPTION
    Demonstrates parsing nested multipart/* sections (one level deep).
    A multipart/mixed request can contain another multipart/* section as one of its parts.

.EXAMPLE
    # Start the server
    pwsh ./22.4-Form-NestedMultipart.ps1
    
    # The server will handle a request where one part contains a nested multipart section
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
$server = New-KrServer -Name 'Nested Multipart Demo'

# Add endpoint
$server | Add-KrEndpoint -Port 5000 -Protocol Http

# Add nested multipart route
$server | Add-KrFormRoute -Pattern '/nested' `
    -DefaultUploadPath $tempDir `
    -MaxNestingDepth 1 `
    -ScriptBlock {
    
    $payload = $FormContext.Payload
    
    if ($payload.PayloadType -ne 'OrderedParts') {
        Write-KrJsonResponse @{ error = 'Expected ordered parts payload' } -StatusCode 400
        return
    }
    
    # Process parts, looking for nested payloads
    $partsInfo = @()
    $nestedCount = 0
    
    foreach ($part in $payload.Parts) {
        $partInfo = @{
            contentType = $part.ContentType
            size = $part.Length
            hasNested = $null -ne $part.NestedPayload
        }
        
        if ($part.NestedPayload) {
            $nestedCount++
            $nested = $part.NestedPayload
            
            if ($nested.PayloadType -eq 'OrderedParts') {
                $partInfo['nestedType'] = 'OrderedParts'
                $partInfo['nestedPartCount'] = $nested.Parts.Count
                $partInfo['nestedParts'] = @()
                
                foreach ($nestedPart in $nested.Parts) {
                    $partInfo['nestedParts'] += @{
                        contentType = $nestedPart.ContentType
                        size = $nestedPart.Length
                    }
                }
            }
        } elseif ($part.Length -lt 1KB) {
            # Read small text parts
            if ($part.ContentType -like 'text/*' -or $part.ContentType -like 'application/json') {
                try {
                    $partInfo['content'] = [System.IO.File]::ReadAllText($part.TempPath)
                } catch {
                    $partInfo['content'] = '(unable to read)'
                }
            }
        }
        
        $partsInfo += $partInfo
    }
    
    Write-KrJsonResponse @{
        success = $true
        topLevelPartCount = $payload.Parts.Count
        nestedSectionCount = $nestedCount
        parts = $partsInfo
    }
}

# Add test page with JavaScript to send nested multipart
$server | Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    $html = @'
<!DOCTYPE html>
<html>
<head>
    <title>Nested Multipart Test</title>
    <script>
        async function sendNested() {
            const outerBoundary = 'outer-' + Date.now();
            const innerBoundary = 'inner-' + Date.now();
            
            // Build nested multipart section
            const nestedBody = [
                `--${innerBoundary}`,
                'Content-Type: text/plain',
                '',
                'First nested part',
                `--${innerBoundary}`,
                'Content-Type: application/json',
                '',
                '{"nested": true, "index": 2}',
                `--${innerBoundary}--`,
                ''
            ].join('\r\n');
            
            // Build outer multipart with nested section
            const body = [
                `--${outerBoundary}`,
                'Content-Type: application/json',
                '',
                '{"type": "metadata", "hasNested": true}',
                `--${outerBoundary}`,
                `Content-Type: multipart/mixed; boundary=${innerBoundary}`,
                '',
                nestedBody,
                `--${outerBoundary}`,
                'Content-Type: text/plain',
                '',
                'Final outer part',
                `--${outerBoundary}--`,
                ''
            ].join('\r\n');
            
            const response = await fetch('/nested', {
                method: 'POST',
                headers: {
                    'Content-Type': `multipart/mixed; boundary=${outerBoundary}`
                },
                body: body
            });
            
            const result = await response.json();
            document.getElementById('result').textContent = JSON.stringify(result, null, 2);
        }
    </script>
</head>
<body>
    <h1>Nested Multipart Test</h1>
    <p>This example sends a multipart/mixed request where one part contains a nested multipart/mixed section.</p>
    <button onclick="sendNested()">Send Nested Multipart Request</button>
    <pre id="result"></pre>
</body>
</html>
'@
    Write-KrHtmlResponse $html
}

Write-Host 'Starting server on http://localhost:5000'
Write-Host 'Navigate to http://localhost:5000 to test nested multipart'
Write-Host "Temp directory: $tempDir"
Write-Host 'Press Ctrl+C to stop'

$server | Enable-KrConfiguration | Start-KrServer
