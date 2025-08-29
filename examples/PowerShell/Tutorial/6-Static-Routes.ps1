<#
    Sample Kestrun Server on how to use static routes.
    These examples demonstrate how to configure static routes in a Kestrun server.
    FileName: 6-Static-Routes.ps1
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

Add-KrStaticFilesService -RequestPath '/assets' -RootPath '.\Assets\wwwroot'

# Enable Kestrun configuration
Enable-KrConfiguration

# Start the server asynchronously
Start-KrServer
