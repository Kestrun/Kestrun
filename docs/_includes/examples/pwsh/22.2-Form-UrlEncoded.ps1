<#
.SYNOPSIS
    Tutorial 22.2 - Form Parsing: application/x-www-form-urlencoded

.DESCRIPTION
    Demonstrates parsing application/x-www-form-urlencoded form submissions.

.EXAMPLE
    # Start the server
    pwsh ./22.2-Form-UrlEncoded.ps1
    
    # In another terminal, submit form data:
    Invoke-RestMethod -Uri 'http://localhost:5000/form' -Method Post -Body @{
        username = 'john'
        email = 'john@example.com'
        message = 'Hello from URL-encoded form'
    }
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

# Setup logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'FormLogger' -SetAsDefault

# Create server
$server = New-KrServer -Name 'URL-Encoded Form Demo'

# Add endpoint
$server | Add-KrEndpoint -Port 5000 -Protocol Http

# Add form route
$server | Add-KrFormRoute -Pattern '/form' -ScriptBlock {
    $payload = $FormContext.Payload
    
    if ($payload.PayloadType -ne 'NamedParts') {
        Write-KrJsonResponse @{ error = 'Expected named parts payload' } -StatusCode 400
        return
    }
    
    # Extract all fields
    $formData = @{}
    foreach ($kvp in $payload.Fields.GetEnumerator()) {
        # URL-encoded forms can have multiple values per key
        if ($kvp.Value.Length -eq 1) {
            $formData[$kvp.Key] = $kvp.Value[0]
        } else {
            $formData[$kvp.Key] = $kvp.Value
        }
    }
    
    Write-KrJsonResponse @{
        success = $true
        receivedFields = $formData
        fieldCount = $payload.Fields.Count
    }
}

# Add test page
$server | Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock {
    $html = @'
<!DOCTYPE html>
<html>
<head><title>URL-Encoded Form Test</title></head>
<body>
    <h1>URL-Encoded Form Test</h1>
    <form method="post" action="/form">
        <p>
            <label>Username: <input type="text" name="username" value="john" /></label>
        </p>
        <p>
            <label>Email: <input type="email" name="email" value="john@example.com" /></label>
        </p>
        <p>
            <label>Message: <textarea name="message">Hello from form</textarea></label>
        </p>
        <p>
            <label>Tags (multiple):
                <select name="tags" multiple size="3">
                    <option value="tag1">Tag 1</option>
                    <option value="tag2">Tag 2</option>
                    <option value="tag3">Tag 3</option>
                </select>
            </label>
        </p>
        <p>
            <button type="submit">Submit</button>
        </p>
    </form>
</body>
</html>
'@
    Write-KrHtmlResponse $html
}

Write-Host 'Starting server on http://localhost:5000'
Write-Host 'Navigate to http://localhost:5000 to test URL-encoded forms'
Write-Host 'Press Ctrl+C to stop'

$server | Enable-KrConfiguration | Start-KrServer
