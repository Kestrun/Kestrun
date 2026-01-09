# Simple Kestrun test app for launcher
# This is a minimal PowerShell script that starts a Kestrun server

Write-Host "Starting Kestrun test app..." -ForegroundColor Green

# Import Kestrun module if not already loaded
$kestrunModule = Join-Path $PSScriptRoot "../../src/PowerShell/Kestrun/Kestrun.psd1"
if (Test-Path $kestrunModule) {
    Import-Module $kestrunModule -Force
}

# Create a simple server
$server = New-KrServer -Name 'LauncherTest' -WorkingDirectory $PSScriptRoot

# Add a simple endpoint
$server | Add-KrEndpoint -Port 5555 -IPAddress "127.0.0.1"

# Add a test route
$server | Add-KrMapRoute -Verbs Get -Pattern '/test' -ScriptBlock {
    Write-KrJsonResponse -InputObject @{
        message = "Hello from launcher test app!"
        timestamp = (Get-Date -Format o)
    } -StatusCode 200
}

# Add a health check route
$server | Add-KrMapRoute -Verbs Get -Pattern '/health' -ScriptBlock {
    Write-KrJsonResponse -InputObject @{
        status = "healthy"
        timestamp = (Get-Date -Format o)
    } -StatusCode 200
}

Write-Host "Server configured. Starting on http://127.0.0.1:5555" -ForegroundColor Cyan
Write-Host "Test with: curl http://127.0.0.1:5555/test" -ForegroundColor Yellow

# Start the server
$server | Start-KrServer -Wait
