<#
    Sample: Claims & Policies Authorization
    Purpose: Build claim-based policies and enforce them on routes after Basic auth.
    File:    8.6-Claims-Policies.ps1
    Notes:   Demonstrates issuing claims during authentication.
#>

# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Server
New-KrServer -Name 'Auth Claims'

# 3. Listener
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback) -SelfSignedCert

# 4. Runtime
Add-KrPowerShellRuntime

# 5. Windows authentication
Add-KrWindowsAuthentication

# 7. Finalize configuration
Enable-KrConfiguration

# 8. Map policy-protected routes

Add-KrMapRoute -Verbs Get -Pattern '/' -ScriptBlock { Write-KrTextResponse 'You are authenticated with Windows Authentication' }
 

# 9. Start server
Start-KrServer

