
<#
    Sample: BSON & CBOR
    Purpose: Demonstrate BSON and CBOR binary responses in a Kestrun server.
    File:    9.5-Bson-Cbor.ps1
    Notes:   Shows compact binary object encoding routes.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# 2. Server
New-KrServer -Name 'Responses 9.5'

# 3. Listener
Add-KrEndpoint -IPAddress $IPAddress -Port $Port

# 4. Runtime
Add-KrPowerShellRuntime

# Finalize configuration and start server
Enable-KrConfiguration

# BSON route
Add-KrMapRoute -Pattern '/bson' -Verbs GET -ScriptBlock {
    @{ kind = 'bson'; ts = (Get-Date).ToUniversalTime(); values = 1..3 } | Write-KrBsonResponse -ContentType 'application/bson'
}

# CBOR route
Add-KrMapRoute -Pattern '/cbor' -Verbs GET -ScriptBlock {
    @{ kind = 'cbor'; ts = (Get-Date).ToUniversalTime(); values = 4..6 } | Write-KrCborResponse -ContentType 'application/cbor'
}

# Plain text route (for comparison)
Add-KrMapRoute -Pattern '/plain' -Verbs GET -ScriptBlock {
    @{ kind = 'json'; ts = (Get-Date).ToUniversalTime(); values = 4..6 } | Write-KrJsonResponse
}


# Start the server
Start-KrServer -CloseLogsOnExit
