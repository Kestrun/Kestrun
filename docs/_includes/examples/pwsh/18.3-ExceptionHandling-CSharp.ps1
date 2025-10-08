#
# Sample: Exception Handling with C# Inline Code
# Demonstrates using Enable-KrExceptionHandling with C# code via Roslyn.
# FileName: 18.3-ExceptionHandling-CSharp.ps1
#
param(
    [int]$Port = 5053,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

Initialize-KrRoot -Path $PSScriptRoot
New-KrLogger | Set-KrLoggerLevel -Level Debug |
    Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Exception Handling - CSharp'
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

$cs = @"
// The Context variable is available and typed to Kestrun.Models.KestrunContext
// You can also access HttpContext via Context.HttpContext
Context.Response.WriteJsonResponse(new {
    error = true,
    message = "Handled by C# exception handler",
    path = Context.Request.Path,
    method = Context.Request.Method
}, statusCode: 500);
"@

Enable-KrExceptionHandling -Code $cs -Language ([Kestrun.Scripting.ScriptLanguage]::CSharp)

Enable-KrConfiguration

Add-KrMapRoute -Verbs Get -Pattern '/fail' -ScriptBlock {
    throw 'C# handler demo'
}

Write-Host "Server starting on http://$($IPAddress):$Port" -ForegroundColor Green
Write-Host 'Try these URLs:' -ForegroundColor Yellow
Write-Host "  http://$($IPAddress):$Port/fail  - Triggers exception handled by C#" -ForegroundColor Cyan
Write-Host ''

Start-KrServer
