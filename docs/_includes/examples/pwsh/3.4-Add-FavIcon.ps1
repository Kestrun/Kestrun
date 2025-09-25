<#
    Sample Kestrun Server on how to add a favicon.
    This example demonstrates how to configure a favicon in a Kestrun server.
    FileName: 3.4-Add-FavIcon.ps1
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

# Add a file server with browsing enabled
Add-KrFileServerMiddleware -RequestPath '/' -RootPath '.\Assets\wwwroot' -EnableDirectoryBrowsing -ServeUnknownFileTypes

# Add a favicon
Add-KrFaviconMiddleware -IconPath '.\Assets\favicon.png'

# Enable Kestrun configuration
Enable-KrConfiguration


# Start the server asynchronously
Start-KrServer
