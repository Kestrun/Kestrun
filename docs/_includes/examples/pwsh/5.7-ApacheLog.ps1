<#
    Sample PowerShell script to create a simple Kestrun server with Apache Common Log Format logging
    This script sets up a Kestrun server that responds with "Hello, World!" and logs requests in Apache common log format.
    FileName: 5.7-ApacheLog.ps1
#>
param(
    [int]$Port = $env:PORT ?? 5000
)

New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Add-KrSinkFile -Path '.\logs\apache_access.log' -RollingInterval Day |
    Register-KrLogger -Name 'myApacheLogger'

# Create a new Kestrun server
New-KrServer -Name "Simple Server"

# HTTP listener (optional)
Add-KrEndpoint -Port $Port

# Add the PowerShell runtime


# Add Apache Common Log Format middleware
Add-KrCommonAccessLogMiddleware -LoggerName 'myApacheLogger' -UseUtcTimestamp

# Enable Kestrun configuration
Enable-KrConfiguration

# Map the route
Add-KrMapRoute -Verbs Get -Pattern "/hello" -ScriptBlock {
    Write-KrTextResponse -InputObject "Hello, World!" -StatusCode 200
    # Or the shorter version
    # Write-KrTextResponse "Hello, World!"
}

# Start the server asynchronously
Start-KrServer -CloseLogsOnExit
