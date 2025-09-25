
<#
    Sample: Binary & Stream Responses
    Purpose: Demonstrate binary and streaming responses in a Kestrun server.
    File:    9.3-Binary-Stream.ps1
    Notes:   Shows file download and streaming with error handling.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
Initialize-KrRoot -Path $PSScriptRoot
# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# 2. Server
New-KrServer -Name 'Responses 9.3'

# 3. Listener
Add-KrEndpoint -IPAddress $IPAddress -Port $Port

# 4. Runtime
Add-KrPowerShellRuntime

# Finalize configuration and start server
Enable-KrConfiguration

# Binary route: download file
Add-KrMapRoute -Pattern '/logo' -Verbs GET -ScriptBlock {
    $path = Resolve-KrPath -Path './Assets/wwwroot/files/kestrun.png' -KestrunRoot
    Write-KrLog -Level Information -Message "Resolved path: {path}" -PropertyValues $path
    if (Test-Path -Path $path) {
        $bytes = [System.IO.File]::ReadAllBytes($path)
        Write-KrBinaryResponse -InputObject $bytes -ContentType 'image/png'
    } else {
        Write-KrErrorResponse -Message 'kestrun.png not found' -StatusCode 404
    }
}

# Stream route: stream text file
Add-KrMapRoute -Pattern '/stream' -Verbs GET -ScriptBlock {
    $path = Resolve-KrPath -Path './Assets/wwwroot/files/sample.txt' -KestrunRoot
    Write-KrLog -Level Information -Message "Resolved path: {path}" -PropertyValues $path
    if (Test-Path $path) {
        $fs = [System.IO.File]::OpenRead($path)
        Write-KrStreamResponse -InputObject $fs -ContentType 'text/plain'
    } else {
        Write-KrErrorResponse -Message 'sample.txt not found' -StatusCode 404
    }
}

# Start the server
Start-KrServer -CloseLogsOnExit
