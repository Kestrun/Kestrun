<#
    Sample Kestrun Server on how to add a favicon.
    This example demonstrates how to configure a favicon in a Kestrun server.
    FileName: 9-Add-FavIcon.ps1
#>

# Import the Kestrun module
Install-PSResource -Name Kestrun

# Initialize Kestrun root directory
# the default value is $PWD
# This is recommended in order to use relative paths without issues
Initialize-KrRoot -Path $PSScriptRoot

# Create a new Kestrun server
New-KrServer -Name "Simple Server"

# Add a listener on port 5000 and IP address 127.0.0.1 (localhost)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# Add a file server with browsing enabled
Add-KrFileServer -RequestPath '/' -RootPath '.\Assets\wwwroot' -EnableDirectoryBrowsing -ServeUnknownFileTypes

# Add a favicon
Add-KrFavicon -IconPath '.\Assets\favicon.png'

# Enable Kestrun configuration
Enable-KrConfiguration

# Start the server asynchronously
Start-KrServer
