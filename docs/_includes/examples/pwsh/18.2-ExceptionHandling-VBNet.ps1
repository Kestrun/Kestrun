#
# Sample: Exception Handling with VB.NET Handler
# Demonstrates handling exceptions via a VB.NET custom exception handler.
# PowerShell routes can throw; middleware (VB.NET) formats a consistent JSON response.
# FileName: 18.2-ExceptionHandling-VBNet.ps1
#
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

Initialize-KrRoot -Path $PSScriptRoot
New-KrLogger | Set-KrLoggerLevel -Level Debug |
    Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Exception Handling - VB.NET Handler'
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Exception handling middleware using VB.NET (recommended for custom handlers)
$vbCode = @'
Context.Response.WriteJsonResponse(New With {.error = True, .message = "Handled by middleware exception handler", .path = Context.Request.Path, .method = Context.Request.Method}, 500)
'@
Enable-KrExceptionHandling -Code $vbCode -Language VBNet

Enable-KrConfiguration

Add-KrMapRoute -Verbs Get -Pattern '/ok' -ScriptBlock {
    Write-KrTextResponse 'Everything is fine.' -StatusCode 200
}

# Throw from PS route; middleware will intercept and format JSON
Add-KrMapRoute -Verbs Get -Pattern '/oops' -ScriptBlock {
    throw [System.InvalidOperationException]::new('Boom! Something went wrong.')
}

# This C# endpoint will use the exception middleware if it throws
Add-KrMapRoute -Verbs Get -Pattern '/csharp-error' -Code 'throw new Exception("C# error");' -Language CSharp

Write-Host "Server starting on http://$($IPAddress):$Port" -ForegroundColor Green
Write-Host 'Try these URLs:' -ForegroundColor Yellow
Write-Host "  http://$($IPAddress):$Port/ok            - Happy path" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/oops          - PowerShell exception (handled by VB.NET middleware)" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/csharp-error  - C# exception (handled by VB.NET middleware)" -ForegroundColor Cyan
Write-Host ''

Start-KrServer
