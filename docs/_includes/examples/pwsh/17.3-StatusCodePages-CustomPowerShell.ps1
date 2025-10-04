#
# Sample: Status Code Pages with Custom Handler
# This script demonstrates how to set up a Kestrun server with a custom handler function.
# The server will use a delegate function to handle status codes programmatically.
# FileName: 17.3-StatusCodePages-Handler.ps1
#
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback # Use 'Loopback' for safety in tests/examples
)

# Step 1: Set up logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# Create a new Kestrun server
New-KrServer -Name 'Status Code Pages with Handler Server'

# Add a listener on the specified port and IP address
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Add the PowerShell runtime
# !!!!Important!!!! this step is required to process PowerShell routes and middlewares
Add-KrPowerShellRuntime

# Create a custom handler function
$scriptBlock = {

    $statusCode = $KrContext.Response.StatusCode
    $response = $KrContext.Response
    $request = $KrContext.Request

    # Log the error
    Write-Host "Status Code $statusCode triggered for path: $($request.Path)" -ForegroundColor Red

    $response.ContentType = 'application/json'

    $errorResponse = @{
        error = $true
        statusCode = $statusCode
        message = switch ($statusCode) {
            404 { 'The requested resource was not found.' }
            500 { 'An internal server error occurred.' }
            403 { 'Access to this resource is forbidden.' }
            401 { 'Authentication is required.' }
            default { 'An error occurred while processing your request.' }
        }
        timestamp = (Get-Date -Format 'o')
        path = $request.Path.Value
        method = $request.Method
    } | ConvertTo-Json

    Write-KrJsonResponse -InputObject $errorResponse -StatusCode $statusCode
}

# Enable status code pages with custom handler
Enable-KrStatusCodePage -ScriptBlock $scriptBlock

# Enable Kestrun configuration
Enable-KrConfiguration

# Map a normal route
Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    Write-KrJsonResponse -InputObject @{ message = 'Hello, World!'; timestamp = (Get-Date -Format 'o') } -StatusCode 200
}

# Start the server
Start-KrServer -CloseLogsOnExit
