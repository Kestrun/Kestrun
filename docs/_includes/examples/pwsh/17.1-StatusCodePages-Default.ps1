#
# Sample: Default Status Code Pages
# This script demonstrates how to set up a Kestrun server with default status code pages.
# The server will show default error pages for 404, 500, and other HTTP error status codes.
# FileName: 17.1-StatusCodePages-Default.ps1
#

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback # Use 'Loopback' for safety in tests/examples
)

# Step 1: Set up logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# Step 2: Create a new Kestrun server
New-KrServer -Name 'Default Status Code Pages Server'

# Step 3: Add a listener on the specified port and IP address
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Step 4: Enable default status code pages middleware
Enable-KrStatusCodePage

# Step 5: Enable Kestrun configuration
Enable-KrConfiguration

# Step 6: Map a normal route
Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    Write-KrTextResponse -InputObject 'Hello, World!' -StatusCode 200
}

# Map routes that trigger different status codes
Add-KrMapRoute -Verbs Get -Pattern '/notfound' -ScriptBlock {
    # Return empty response with 404 status to trigger custom error page
    Write-KrStatusResponse -StatusCode 404
}

Add-KrMapRoute -Verbs Get -Pattern '/error' -ScriptBlock {
    # Return empty response with 500 status to trigger custom error page
    Write-KrStatusResponse -StatusCode 500
}

Add-KrMapRoute -Verbs Get -Pattern '/forbidden' -ScriptBlock {
    # Return empty response with 403 status to trigger custom error page
    Write-KrStatusResponse -StatusCode 403
}

Add-KrMapRoute -Verbs Get -Pattern '/unauthorized' -ScriptBlock {
    # Return empty response with 401 status to trigger custom error page
    Write-KrStatusResponse -StatusCode 401
}

Write-Host "Server starting on http://$($IPAddress):$Port" -ForegroundColor Green
Write-Host 'Try these URLs:' -ForegroundColor Yellow
Write-Host "  http://$($IPAddress):$Port/hello        - Styled welcome page with test links" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/notfound     - Styled 404 error page" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/error        - Styled 500 error page" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/forbidden    - Styled 403 error page" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/unauthorized - Styled 401 error page" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/missing      - Styled 404 for unmapped route" -ForegroundColor Cyan


# Step 7: Start the server
Start-KrServer -CloseLogsOnExit
