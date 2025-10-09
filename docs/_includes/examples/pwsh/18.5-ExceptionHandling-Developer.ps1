#
# Sample: Exception Handling with Re-execution
# Demonstrates how to configure the developer exception handling page.
# Don't use this in production!
# FileName: 18.5-ExceptionHandling-Developer.ps1
#
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

Initialize-KrRoot -Path $PSScriptRoot
New-KrLogger | Set-KrLoggerLevel -Level Debug |
    Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# Create server and listener
New-KrServer -Name 'Exception Handling - Developer'
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Configure exception handling to use the Developer Exception Page
Enable-KrExceptionHandling -DeveloperExceptionPage -SourceCodeLineCount 10

# Finalize configuration
Enable-KrConfiguration

# A normal route
Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    Write-KrJsonResponse @{ msg = 'Hello from /hello' } -StatusCode 200
}

# A route that throws to simulate an unhandled exception
Add-KrMapRoute -Verbs Get -Pattern '/throw' -ScriptBlock {
    throw [System.InvalidOperationException]::new('Boom! Something went wrong.')
}


Write-Host "Server starting on http://$($IPAddress):$Port" -ForegroundColor Green
Write-Host 'Try these URLs:' -ForegroundColor Yellow
Write-Host "  http://$($IPAddress):$Port/hello   - Happy path" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/throw   - Triggers exception, shows developer exception page" -ForegroundColor Cyan

Start-KrServer
