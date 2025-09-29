
<#
    Sample: Errors
    Purpose: Demonstrate error responses in a Kestrun server.
    File:    9.7-Errors.ps1
    Notes:   Shows validation and exception error payloads.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# 2. Server
New-KrServer -Name 'Responses 9.7'

# 3. Listener
Add-KrEndpoint -IPAddress $IPAddress -Port $Port

# 4. Runtime
Add-KrPowerShellRuntime

# Finalize configuration
Enable-KrConfiguration

# Validation error route
Add-KrMapRoute -Pattern '/fail' -Verbs GET -ScriptBlock {
    Write-KrErrorResponse -Message 'Missing parameter' -StatusCode 400 -Details 'id required'
}

# Exception error route
Add-KrMapRoute -Pattern '/boom' -Verbs GET -ScriptBlock {
    try {
        throw [System.InvalidOperationException]::new('Exploded')
    } catch {
        Write-KrErrorResponse -Exception $_.Exception -StatusCode 500 -IncludeStack
    }
}

# Start the server
Start-KrServer
