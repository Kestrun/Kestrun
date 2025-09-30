<#
    Sample Kestrun Server on how to use static routes.
    These examples demonstrate how to configure static routes in a Kestrun server.
    FileName: 3.1-Static-Routes.ps1
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Initialize Kestrun root directory
# the default value is $PWD
# This is recommended in order to use relative paths without issues
Initialize-KrRoot -Path $PSScriptRoot

# Create a new Kestrun server
New-KrServer -Name "Simple Server"

# Add a listener on the configured port and IP address
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Add a static files service
Add-KrStaticFilesMiddleware -RequestPath '/assets' -RootPath '.\Assets\wwwroot' -ServeUnknownFileTypes

# Enable Kestrun configuration
Enable-KrConfiguration


# Start the server asynchronously
Start-KrServer
