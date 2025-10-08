#
# Sample: Exception Handling with ProblemDetails Fallback
# Demonstrates JSON responses using Problem Details (RFC 7807) without custom handlers.
# FileName: 18.4-ExceptionHandling-ProblemDetails.ps1
#
param(
    [int]$Port = 5054,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

Initialize-KrRoot -Path $PSScriptRoot
New-KrLogger | Set-KrLoggerLevel -Level Debug |
    Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Exception Handling - ProblemDetails'
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# No custom handler or re-exec path; rely on built-in JSON/ProblemDetails
Enable-KrExceptionHandling -IncludeDetailsInDevelopment -UseProblemDetails

Enable-KrConfiguration

Add-KrMapRoute -Verbs Get -Pattern '/boom' -ScriptBlock {
    throw 'This will be formatted as ProblemDetails (500)'
}

Write-Host "Server starting on http://$($IPAddress):$Port" -ForegroundColor Green
Write-Host 'Try these URLs:' -ForegroundColor Yellow
Write-Host "  http://$($IPAddress):$Port/boom  - Triggers ProblemDetails JSON" -ForegroundColor Cyan
Write-Host ''

Start-KrServer
