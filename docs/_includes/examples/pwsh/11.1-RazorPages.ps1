<#
    Sample Kestrun Server on how to configure a static file server.
    These examples demonstrate how to configure static routes with directory browsing in a Kestrun server.
    FileName: 11.1-RazorPages.ps1
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Initialize Kestrun root directory
# the default value is $PWD
# This is recommended in order to use relative paths without issues
Initialize-KrRoot -Path $PSScriptRoot

# Create a new logger
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -SetAsDefault -Name 'DefaultLogger'

# Create a new Kestrun server
New-KrServer -Name "RazorPages"

# Add a listener on the configured port and IP address
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Add a Razor Pages handler to the server
Add-KrPowerShellRazorPagesRuntime #-PathPrefix '/Assets'

# Enable Kestrun configuration
Enable-KrConfiguration


# Start the server asynchronously
Start-KrServer
