
<#
    Sample: Low-Level Response
    Purpose: Demonstrate raw stream response in a Kestrun server.
    File:    9.10-Low-Level-Response.ps1
    Notes:   Shows direct stream writing for custom scenarios.
#>

# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# 2. Server
New-KrServer -Name 'Responses 9.10'

# 3. Listener
Add-KrListener -IPAddress '127.0.0.1' -Port 5000

# 4. Runtime
Add-KrPowerShellRuntime

# Finalize configuration
Enable-KrConfiguration

# Raw stream route
Add-KrMapRoute -Pattern '/raw' -Verbs GET -ScriptBlock {
    $Context.Response.ContentType = 'text/plain'
    $Context.Response.StatusCode = 200

    $bytes = [System.Text.Encoding]::UTF8.GetBytes('raw-stream-data')
    $Context.Response.Body.Write($bytes, 0, $bytes.Length)
}

# Start the server
Start-KrServer
