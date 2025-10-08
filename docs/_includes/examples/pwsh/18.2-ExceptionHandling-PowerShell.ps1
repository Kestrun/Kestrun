#
# Sample: Exception Handling with PowerShell ScriptBlock
# Demonstrates using Enable-KrExceptionHandling with a custom PowerShell handler.
# FileName: 18.2-ExceptionHandling-PowerShell.ps1
#
param(
    [int]$Port = 5052,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

Initialize-KrRoot -Path $PSScriptRoot
New-KrLogger | Set-KrLoggerLevel -Level Debug |
    Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'Exception Handling - PowerShell'
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Custom exception handler using a PowerShell scriptblock
$handler = {
    # $Context is auto-injected; you can access the exception via feature, or simply respond
    $env = [System.Environment]::GetEnvironmentVariable('ASPNETCORE_ENVIRONMENT')
    $detail = $null
    if ($env -eq 'Development') { $detail = 'Detailed information available in Development.' }

    Write-KrJsonResponse -StatusCode 500 -Body @{ error = $true; message = 'Handled by PowerShell exception handler.'; env = $env; detail = $detail }
}

Enable-KrExceptionHandling -ScriptBlock $handler

Enable-KrConfiguration

Add-KrMapRoute -Verbs Get -Pattern '/ok' -ScriptBlock {
    Write-KrTextResponse 'Everything is fine.' -StatusCode 200
}

Add-KrMapRoute -Verbs Get -Pattern '/oops' -ScriptBlock {
    throw 'Oops from /oops route'
}

Write-Host "Server starting on http://$($IPAddress):$Port" -ForegroundColor Green
Write-Host 'Try these URLs:' -ForegroundColor Yellow
Write-Host "  http://$($IPAddress):$Port/ok    - Happy path" -ForegroundColor Cyan
Write-Host "  http://$($IPAddress):$Port/oops  - Triggers exception handled by PS scriptblock" -ForegroundColor Cyan
Write-Host ''

Start-KrServer
