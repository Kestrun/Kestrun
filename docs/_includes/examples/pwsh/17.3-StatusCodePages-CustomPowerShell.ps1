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
New-KrLogger | Set-KrLoggerLevel -Level Debug | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# Create a new Kestrun server
New-KrServer -Name 'Status Code Pages with Handler Server'

# Add a listener on the specified port and IP address
Add-KrEndpoint -Port $Port -IPAddress $IPAddress


# Create a custom handler function
$scriptBlock = {
    $statusCode = $Context.Response.StatusCode
    $response = $Context.Response
    $request = $Context.Request

    # Log the error
    Write-KrLog -Level Information -Message 'Status Code {StatusCode} triggered for path {Path}:' -Values $statusCode, $request.Path

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
    }

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

# Start the server
Start-KrServer -CloseLogsOnExit
