<#
    Sample Kestrun Server on how to configure a static file server.
    These examples demonstrate how to configure static routes with directory browsing in a Kestrun server.
    FileName: 3.2-File-Server.ps1
#>

# Initialize Kestrun root directory
# the default value is $PWD
# This is recommended in order to use relative paths without issues
Initialize-KrRoot -Path $PSScriptRoot

# Create a new Kestrun server
New-KrServer -Name "Simple Server"

# Add a listener on port 5000 and IP address 127.0.0.1 (localhost)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# Define the content type map to add to the default set
$map = @{
    ".yaml" = "application/x-yaml"
    ".yml" = "application/x-yaml"
    ".ps1" = "text/plain"
}

# Add a file server with browsing enabled
Add-KrFileServerMiddleware -RequestPath '/' -RootPath '.\Assets\wwwroot' -EnableDirectoryBrowsing -ContentTypeMap $map

# Enable Kestrun configuration
Enable-KrConfiguration

# Start the server asynchronously
Start-KrServer
