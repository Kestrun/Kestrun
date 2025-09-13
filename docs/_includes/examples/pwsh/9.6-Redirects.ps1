
<#
    Sample: Redirects
    Purpose: Demonstrate HTTP redirects in a Kestrun server.
    File:    9.6-Redirects.ps1
    Notes:   Shows 3xx Location header response.
#>

# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# 2. Server
New-KrServer -Name 'Responses 9.6'

# 3. Listener
Add-KrListener -IPAddress '127.0.0.1' -Port 5000

# 4. Runtime
Add-KrPowerShellRuntime

# Finalize configuration and start server
Enable-KrConfiguration

# Redirect route
Add-KrMapRoute -Pattern '/old' -Verbs GET -ScriptBlock {
    Write-KrRedirectResponse -Url 'https://example.com/new' -Message 'Resource moved to /new'
}

# Start the server
Start-KrServer
