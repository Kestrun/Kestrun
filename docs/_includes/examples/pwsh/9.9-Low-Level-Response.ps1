
<#
    Sample: Low-Level Response
    Purpose: Demonstrate raw stream response in a Kestrun server.
    File:    9.9-Low-Level-Response.ps1
    Notes:   Shows direct stream writing for custom scenarios.
#>

# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# 2. Server
New-KrServer -Name 'Responses 9.9'

# 3. Listener
Add-KrListener -IPAddress '127.0.0.1' -Port 5000

# 4. Runtime
Add-KrPowerShellRuntime

# Finalize configuration and start server
Enable-KrConfiguration

# Raw stream route
Add-KrMapRoute -Pattern '/raw' -Verbs GET -ScriptBlock {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes('raw-stream')
    $ms = New-Object System.IO.MemoryStream(, $bytes)
    Write-KrResponse -InputObject $ms -StatusCode 200
}

# Start the server
Start-KrServer
