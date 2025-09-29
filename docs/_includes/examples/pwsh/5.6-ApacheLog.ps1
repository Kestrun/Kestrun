<#
    Sample PowerShell script to create a simple Kestrun server with Apache Common Log Format logging
    This script sets up a Kestrun server that responds with "Hello, World!" and logs requests in Apache common log format.
    FileName: 5.6-ApacheLog.ps1
#>

New-KrLogger |
    Set-KrLoggerMinimumLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'myLogger' -SetAsDefault

# Create a new Kestrun server
New-KrServer -Name "Simple Server"

# Add a listener on port 5000 and IP address 127.0.0.1 (localhost)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# Add the PowerShell runtime
# !!!!Important!!!! this step is required to process PowerShell routes and middlewares
Add-KrPowerShellRuntime
Add-KrCommonAccessLogMiddleware -LoggerName 'myLogger' -UseUtcTimestamp
# Enable Kestrun configuration
Enable-KrConfiguration

# Map the route
Add-KrMapRoute -Verbs Get -Pattern "/hello" -ScriptBlock {
    Write-KrTextResponse -InputObject "Hello, World!" -StatusCode 200
    # Or the shorter version
    # Write-KrTextResponse "Hello, World!"
}

# Start the server asynchronously
Start-KrServer
