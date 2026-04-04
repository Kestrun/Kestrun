#
# Sample: Exception Handling with ProblemDetails Fallback
# Demonstrates JSON responses using Problem Details (RFC 7807) without custom handlers.
# FileName: 18.4-ExceptionHandling-ProblemDetails.ps1
#
param(
    [int]$Port = $env:PORT ?? 5000
)

Initialize-KrRoot -Path $PSScriptRoot
New-KrLogger | Set-KrLoggerLevel -Level Debug |
    Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Exception Handling - ProblemDetails'
Add-KrEndpoint -Port $Port

# No custom handler or re-exec path; rely on built-in JSON/ProblemDetails
Enable-KrExceptionHandling -IncludeDetailsInDevelopment -UseProblemDetails

Enable-KrConfiguration

# A normal route
Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    Write-KrJsonResponse @{ msg = 'Hello from /hello' } -StatusCode 200
}

Add-KrMapRoute -Verbs Get -Pattern '/boom' -ScriptBlock {
    throw 'This will be formatted as ProblemDetails (500)'
}

Write-Host "Server starting on http://localhost:$Port" -ForegroundColor Green
Write-Host 'Try these URLs:' -ForegroundColor Yellow
Write-Host "  http://localhost:$Port/hello   - Happy path" -ForegroundColor Cyan
Write-Host "  http://localhost:$Port/boom  - Triggers ProblemDetails JSON" -ForegroundColor Cyan
Write-Host ''

Start-KrServer
