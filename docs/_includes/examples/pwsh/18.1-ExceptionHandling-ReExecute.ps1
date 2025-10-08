#
# Sample: Exception Handling with Re-execution
# Demonstrates how to configure exception handling to re-execute the pipeline to a fixed error path.
# FileName: 18.1-ExceptionHandling-ReExecute.ps1
#
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

Initialize-KrRoot -Path $PSScriptRoot
New-KrLogger | Set-KrLoggerLevel -Level Debug |
    Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# Create server and listener
New-KrServer -Name 'Exception Handling - ReExecute'
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Configure exception handling to re-execute the pipeline at /error
Enable-KrExceptionHandling -ExceptionHandlingPath '/error'

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

# The error route that receives re-executed requests
Add-KrMapRoute -Verbs Get -Pattern '/error' -ScriptBlock {
    # Note: the request URL remains the original URL; this route handles the response content
    $path = $Context.Request.Path
    $method = $Context.Request.Method
    Write-KrJsonResponse @{ error=$true; message='Request re-executed to /error'; path=$path; method=$method } -StatusCode 500
}

Write-Host "Server starting on http://$($IPAddress):$Port" -ForegroundColor Green
Write-Host 'Try these URLs:' -ForegroundColor Yellow
Write-Host "  http://$($IPAddress):$Port/hello   - Happy path" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/throw   - Triggers exception, re-executes to /error" -ForegroundColor Cyan

Start-KrServer
