#
# Sample: Exception Handling within PowerShell Scripts
# Demonstrates handling exceptions WITHIN PowerShell scripts (recommended approach).
# Note: PowerShell runtime has built-in exception handling, so custom exception
# middleware applies to other types of endpoints, not PowerShell scripts.
# FileName: 18.2-ExceptionHandling-PowerShell.ps1
#
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

Initialize-KrRoot -Path $PSScriptRoot
New-KrLogger | Set-KrLoggerLevel -Level Debug |
    Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Exception Handling - PowerShell Scripts'
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Exception handling middleware (for non-PowerShell endpoints)
$handler = {
    Write-KrJsonResponse -InputObject @{ error = $true; message = 'Handled by middleware exception handler.' } -StatusCode 500
}
Enable-KrExceptionHandling -ScriptBlock $handler

Enable-KrConfiguration

Add-KrMapRoute -Verbs Get -Pattern '/ok' -ScriptBlock {
    Write-KrTextResponse 'Everything is fine.' -StatusCode 200
}

# PowerShell scripts should handle their own exceptions
Add-KrMapRoute -Verbs Get -Pattern '/oops' -ScriptBlock {
     throw [System.InvalidOperationException]::new('Boom! Something went wrong.')
   # throw 'Oops from /oops route'
}

# This C# endpoint will use the exception middleware if it throws
Add-KrMapRoute -Verbs Get -Pattern '/csharp-error' -Code 'throw new Exception("C# error");' -Language CSharp

Write-Host "Server starting on http://$($IPAddress):$Port" -ForegroundColor Green
Write-Host 'Try these URLs:' -ForegroundColor Yellow
Write-Host "  http://$($IPAddress):$Port/ok            - Happy path" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/oops          - PowerShell exception (handled in script)" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/csharp-error  - C# exception (handled by middleware)" -ForegroundColor Cyan
Write-Host ''

Start-KrServer
