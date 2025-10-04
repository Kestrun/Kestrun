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

# Step 4: Add the PowerShell runtime
# !!!!Important!!!! this step is required to process PowerShell routes and middlewares
Add-KrPowerShellRuntime

# Step 5: Enable default status code pages middleware
Enable-KrStatusCodePage

# Step 6: Enable Kestrun configuration
Enable-KrConfiguration

# Step 7: Map a normal route
Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    Write-KrTextResponse -InputObject 'Hello, World!' -StatusCode 200
}

# Step 8: Start the server
Start-KrServer -CloseLogsOnExit
