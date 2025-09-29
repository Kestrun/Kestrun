<#
    Sample: Content Negotiation
    Purpose: Demonstrate content negotiation and automatic format selection in a Kestrun server.
    File:    9.9-Content-Negotiation.ps1
    Notes:   Shows how Write-KrResponse chooses format based on Accept header.
#>

# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# 2. Server
New-KrServer -Name 'Responses 9.9'

# 3. Listener
Add-KrListener -IPAddress '127.0.0.1' -Port 5000

# 4. Runtime
Add-KrPowerShellRuntime

# Finalize configuration
Enable-KrConfiguration

# Content negotiation route
Add-KrMapRoute -Pattern '/negotiate' -Verbs GET -ScriptBlock {
    $data = @{
        message = 'Hello World'
        timestamp = (Get-Date).ToUniversalTime()
        id = 123
    }
    Write-KrResponse -InputObject $data -StatusCode 200
}

# Explicit format route
Add-KrMapRoute -Pattern '/explicit' -Verbs GET -ScriptBlock {
    $text = 'Explicit plain text response'
    Write-KrResponse -InputObject $text -StatusCode 200 -ContentType 'text/plain'
}

# Error response route
Add-KrMapRoute -Pattern '/error' -Verbs GET -ScriptBlock {
    $errorData = @{
        error = 'Not Found'
        code = 404
        details = 'The requested resource was not found'
    }
    Write-KrResponse -InputObject $errorData -StatusCode 404
}

# Start the server
Start-KrServer
