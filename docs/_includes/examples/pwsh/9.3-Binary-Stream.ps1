
<#
    Sample: Binary & Stream Responses
    Purpose: Demonstrate binary and streaming responses in a Kestrun server.
    File:    9.3-Binary-Stream.ps1
    Notes:   Shows file download and streaming with error handling.
#>

# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# 2. Server
New-KrServer -Name 'Responses 9.3'

# 3. Listener
Add-KrListener -IPAddress '127.0.0.1' -Port 5000

# 4. Runtime
Add-KrPowerShellRuntime

# Finalize configuration and start server
Enable-KrConfiguration

# Binary route: download file
Add-KrMapRoute -Pattern '/logo' -Verbs GET -ScriptBlock {
    $path = Resolve-KrPath -Pattern 'Assets/wwwroot/files/sample.bin' -KestrunRoot -Test
    if (Test-Path $path) {
        $bytes = [System.IO.File]::ReadAllBytes($path)
        Write-KrBinaryResponse -InputObject $bytes -ContentType 'application/octet-stream'
    } else {
        Write-KrErrorResponse -Message 'sample.bin not found' -StatusCode 404
    }
}

# Stream route: stream text file
Add-KrMapRoute -Pattern '/stream' -Verbs GET -ScriptBlock {
    $path = Resolve-KrPath -Pattern 'Assets/wwwroot/files/sample.txt' -KestrunRoot -Test
    if (Test-Path $path) {
        $fs = [System.IO.File]::OpenRead($path)
        Write-KrStreamResponse -InputObject $fs -ContentType 'text/plain'
    } else {
        Write-KrErrorResponse -Message 'sample.txt not found' -StatusCode 404
    }
}

# Start the server
Start-KrServer
