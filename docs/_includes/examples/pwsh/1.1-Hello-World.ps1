<#
    Sample Kestrun Server Configuration
    This script demonstrates how to set up a simple Kestrun server with a single route.
    The server will respond with "Hello, World!" when accessed.
    FileName: 1.1-Hello-World.ps1
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback # Use 'Loopback' for safety in tests/examples
)

# Create a new Kestrun server
New-KrServer -Name 'Simple Server'

# Add a listener on port 5000 and IP address 127.0.0.1 (localhost)
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Enable Kestrun configuration
Enable-KrConfiguration

# Map the route
Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    Write-KrTextResponse -InputObject 'Hello, World!' -StatusCode 200
    # Or the shorter version
    # Write-KrTextResponse "Hello, World!"
}

# Start the server asynchronously
Start-KrServer
