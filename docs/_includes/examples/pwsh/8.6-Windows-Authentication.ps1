<#
    Sample: Windows Authentication
    Purpose: Leverage Windows credentials for seamless authentication.
    File:    8.6-Windows-Authentication.ps1
    Notes:   Demonstrates integrated Windows authentication.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Server
New-KrServer -Name 'Auth Claims'

# 3. Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -SelfSignedCert


# 5. Windows authentication
Add-KrWindowsAuthentication

# 7. Finalize configuration
Enable-KrConfiguration

# 8. Map policy-protected routes

Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock { Write-KrTextResponse 'You are authenticated with Windows Authentication' }


# 9. Start server
Start-KrServer

