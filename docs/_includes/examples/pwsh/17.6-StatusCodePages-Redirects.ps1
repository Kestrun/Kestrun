#
# Sample: Status Code Pages with Redirects
# This script demonstrates how to set up a Kestrun server with redirect-based error handling.
# The server will redirect error responses to specific error pages.
# FileName: 17.6-StatusCodePages-Redirects.ps1
#
param(
    [int]$Port = 5006,
    [IPAddress]$IPAddress = [IPAddress]::Loopback # Use 'Loopback' for safety in tests/examples
)

# Create a new Kestrun server
New-KrServer -Name 'Status Code Pages with Redirects Server'

# Add a listener on the specified port and IP address
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Add the PowerShell runtime
# !!!!Important!!!! this step is required to process PowerShell routes and middlewares
Add-KrPowerShellRuntime

# Enable status code pages with redirects
# The {0} placeholder will be replaced with the actual status code
# URLs starting with '~' will have PathBase prepended
Enable-KrStatusCodePage -LocationFormat '/error/{0}'

# Enable Kestrun configuration
Enable-KrConfiguration

# Map a normal route
Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    # Read HTML template from file
    $htmlPath = Join-Path $PSScriptRoot 'Assets\wwwroot\statuscodepages\welcome-redirects.html'
    $html = Get-Content -Path $htmlPath -Raw
    Write-KrHtmlResponse -InputObject $html -StatusCode 200
}

# Map routes that trigger different status codes (these will redirect)
Add-KrMapRoute -Verbs Get -Pattern '/notfound' -ScriptBlock {
    # Return empty response with 404 status to trigger redirect
    $Context.Response.StatusCode = 404
}

Add-KrMapRoute -Verbs Get -Pattern '/error' -ScriptBlock {
    # Return empty response with 500 status to trigger redirect
    $Context.Response.StatusCode = 500
}

Add-KrMapRoute -Verbs Get -Pattern '/forbidden' -ScriptBlock {
    # Return empty response with 403 status to trigger redirect
    $Context.Response.StatusCode = 403
}

# Map the error pages that redirects will target
Add-KrMapRoute -Verbs Get -Pattern '/error/{statusCode}' -ScriptBlock {
    $statusCode = Get-KrRequestRouteParam -Name 'statusCode'

    $errorInfo = switch ($statusCode) {
        '404' { @{ title = 'Page Not Found'; description = "The page you're looking for doesn't exist."; icon = '🔍' } }
        '500' { @{ title = 'Internal Server Error'; description = 'Something went wrong on our server.'; icon = '⚠️' } }
        '403' { @{ title = 'Access Forbidden'; description = "You don't have permission to access this resource."; icon = '🚫' } }
        default { @{ title = "Error $statusCode"; description = 'An error occurred while processing your request.'; icon = '❌' } }
    }

    # Read HTML template from file
    $htmlTemplatePath = Join-Path $PSScriptRoot 'Assets\wwwroot\statuscodepages\error-redirects.html'
    $htmlTemplate = Get-Content -Path $htmlTemplatePath -Raw

    # Replace placeholders with actual values
    $html = $htmlTemplate -replace '\{STATUS_CODE\}', $statusCode
    $html = $html -replace '\{ERROR_ICON\}', $errorInfo.icon
    $html = $html -replace '\{ERROR_TITLE\}', $errorInfo.title
    $html = $html -replace '\{ERROR_DESCRIPTION\}', $errorInfo.description

    Write-KrHtmlResponse -InputObject $html -StatusCode 200
}

Write-Host "Server starting on http://$($IPAddress):$Port" -ForegroundColor Green
Write-Host 'Try these URLs:' -ForegroundColor Yellow
Write-Host "  http://$($IPAddress):$Port/hello     - Welcome page with redirect test links" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/notfound  - Redirects to /error/404" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/error     - Redirects to /error/500" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/forbidden - Redirects to /error/403" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/missing   - Redirects to /error/404 (unmapped route)" -ForegroundColor Cyan
Write-Host '' -ForegroundColor White
Write-Host 'Note: Watch the browser URL change during redirects!' -ForegroundColor Magenta

# Start the server asynchronously
Start-KrServer
