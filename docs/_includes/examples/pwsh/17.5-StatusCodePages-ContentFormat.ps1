#
# Sample: Status Code Pages with Content Format
# This script demonstrates how to set up a Kestrun server with custom content type and body format.
# The server will show formatted error pages with placeholders for status codes.
# FileName: 17.5-StatusCodePages-ContentFormat.ps1
#
param(
    [int]$Port = 5005,
    [IPAddress]$IPAddress = [IPAddress]::Loopback # Use 'Loopback' for safety in tests/examples
)

# Create a new Kestrun server
New-KrServer -Name 'Status Code Pages with Content Format Server'

# Add a listener on the specified port and IP address
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Add the PowerShell runtime
# !!!!Important!!!! this step is required to process PowerShell routes and middlewares
Add-KrPowerShellRuntime

# Define custom HTML template with placeholder for status code
$htmlTemplatePath = Join-Path $PSScriptRoot 'Assets\wwwroot\statuscodepages\error-contentformat.html'
$htmlTemplate = Get-Content -Path $htmlTemplatePath -Raw

# Replace timestamp placeholder with current timestamp
$htmlTemplate = $htmlTemplate -replace '\{TIMESTAMP\}', (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

# Enable status code pages with custom content type and body format
# The {0} placeholder will be replaced with the actual status code
Enable-KrStatusCodePage -ContentType 'text/html; charset=utf-8' -BodyFormat $htmlTemplate

# Enable Kestrun configuration
Enable-KrConfiguration

# Map a normal route with some styling
Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    # Read HTML template from file
    $htmlPath = Join-Path $PSScriptRoot 'Assets\wwwroot\statuscodepages\welcome-contentformat.html'
    $html = Get-Content -Path $htmlPath -Raw
    Write-KrHtmlResponse -InputObject $html -StatusCode 200
}

# Map routes that trigger different status codes
Add-KrMapRoute -Verbs Get -Pattern '/notfound' -ScriptBlock {
    # Return empty response with 404 status to trigger custom error page
    $Context.Response.StatusCode = 404
}

Add-KrMapRoute -Verbs Get -Pattern '/error' -ScriptBlock {
    # Return empty response with 500 status to trigger custom error page
    $Context.Response.StatusCode = 500
}

Add-KrMapRoute -Verbs Get -Pattern '/forbidden' -ScriptBlock {
    # Return empty response with 403 status to trigger custom error page
    $Context.Response.StatusCode = 403
}

Add-KrMapRoute -Verbs Get -Pattern '/unauthorized' -ScriptBlock {
    # Return empty response with 401 status to trigger custom error page
    $Context.Response.StatusCode = 401
}

Write-Host "Server starting on http://$($IPAddress):$Port" -ForegroundColor Green
Write-Host 'Try these URLs:' -ForegroundColor Yellow
Write-Host "  http://$($IPAddress):$Port/hello        - Styled welcome page with test links" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/notfound     - Styled 404 error page" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/error        - Styled 500 error page" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/forbidden    - Styled 403 error page" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/unauthorized - Styled 401 error page" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/missing      - Styled 404 for unmapped route" -ForegroundColor Cyan

# Start the server asynchronously
Start-KrServer
