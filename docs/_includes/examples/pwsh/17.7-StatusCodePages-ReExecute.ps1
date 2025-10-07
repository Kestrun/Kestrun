#
# Sample: Status Code Pages with Re-execution
# This script demonstrates how to set up a Kestrun server with re-execution error handling.
# The server will re-execute the request pipeline using alternate paths for error handling.
# FileName: 17.7-StatusCodePages-ReExecute.ps1
#
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback # Use 'Loopback' for safety in tests/examples
)

Initialize-KrRoot -Path $PSScriptRoot
New-KrLogger | Set-KrLoggerLevel -Level Debug |
    Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# Create a new Kestrun server
New-KrServer -Name 'Status Code Pages with Re-execution Server'

# Add a listener on the specified port and IP address
Add-KrEndpoint -Port $Port -IPAddress $IPAddress


# Enable status code pages with re-execution
# This will re-execute the request pipeline with the specified path and query format
# The {0} placeholder will be replaced with the actual status code
Enable-KrStatusCodePage -PathFormat '/errors/{0}' -QueryFormat 'originalPath={0}'

# Enable Kestrun configuration
Enable-KrConfiguration

Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    # Read HTML template from file
    $htmlPath = '.\Assets\wwwroot\statuscodepages\welcome-reexecute.html'
    Write-KrHtmlResponse -FilePath $htmlPath -StatusCode 200
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

# Map the error handling routes that re-execution will target
Add-KrMapRoute -Verbs Get -Pattern '/errors/{statusCode}' -ScriptBlock {
    $statusCode = Get-KrRequestRouteParam -Name 'statusCode'
    $originalPath = Get-KrRequestQuery -Name 'originalPath'
    Expand-KrObject $Context.Request.RouteValues
    $currentPath = $Context.Request.Path
    $method = $Context.Request.Method

    $errorInfo = switch ($statusCode) {
        '404' {
            @{
                title = 'Page Not Found'
                description = 'The requested page could not be found.'
                icon = '🔍'
                color = '#ff9800'
            }
        }
        '500' {
            @{
                title = 'Internal Server Error'
                description = 'An internal error occurred while processing your request.'
                icon = '⚠️'
                color = '#f44336'
            }
        }
        '403' {
            @{
                title = 'Access Forbidden'
                description = "You don't have permission to access this resource."
                icon = '🚫'
                color = '#e91e63'
            }
        }
        default {
            @{
                title = "Error $statusCode"
                description = 'An error occurred while processing your request.'
                icon = '❌'
                color = '#9c27b0'
            }
        }
    }

    # Read HTML template from file
    $htmlTemplate = 'Assets\wwwroot\statuscodepages\error-reexecute.html'

    $variables = @{
        statusCode = $statusCode
        errorInfo = $errorInfo
        method = $method
        originalPath = $originalPath
        currentPath = $currentPath
        timestamp = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
    }

    Expand-KrObject -InputObject $variables
    Write-KrHtmlResponse -FilePath $htmlTemplate -StatusCode 200 -Variables $variables
}

Write-Host "Server starting on http://$($IPAddress):$Port" -ForegroundColor Green
Write-Host 'Try these URLs:' -ForegroundColor Yellow
Write-Host "  http://$($IPAddress):$Port/hello     - Welcome page with re-execution test links" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/notfound  - Re-executes as /errors/404" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/error     - Re-executes as /errors/500" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/forbidden - Re-executes as /errors/403" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/missing   - Re-executes as /errors/404 (unmapped route)" -ForegroundColor Cyan
Write-Host '' -ForegroundColor White
Write-Host 'Note: The URL stays the same, but the content is from re-executed routes!' -ForegroundColor Magenta

# Start the server asynchronously
Start-KrServer
