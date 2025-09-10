<#
    Sample: Mixed HTTP Protocol Versions
    Purpose: Demonstrate configuring multiple listeners each with a different HTTP protocol set (HTTP/1.1, HTTP/2, HTTP/3*) plus a combined HTTP/1.1+HTTP/2 listener.
    File:    7.6-Mixed-HttpProtocols.ps1
    Notes:   HTTP/3 requires platform & runtime support + QUIC enabled. If HTTP/3 is not available, that listener may fail to bind.
#>

# 1. Logging (console)
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Create server
New-KrServer -Name 'Endpoints Mixed Protocols'

# 3. Listeners with explicit protocol selections
#    - 5001: HTTP/1.1 only
Add-KrListener -Port 5001 -IPAddress ([IPAddress]::Loopback) -Protocols ([Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1)
#    - 5002: HTTP/2 only
Add-KrListener -Port 5002 -IPAddress ([IPAddress]::Loopback) -Protocols ([Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http2)
#    - 5003: HTTP/3 only (QUIC)
Add-KrListener -Port 5003 -IPAddress ([IPAddress]::Loopback) -Protocols ([Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http3) 2>$null
#    - 5004: Combined HTTP/1.1 + HTTP/2
Add-KrListener -Port 5004 -IPAddress ([IPAddress]::Loopback) -Protocols ([Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1AndHttp2)

# 4. Runtime
Add-KrPowerShellRuntime

# 5. Enable configuration
Enable-KrConfiguration

# 6. Single route served by all listeners
Add-KrMapRoute -Verbs Get -Pattern '/version' -ScriptBlock {
    $proto = $Context.HttpContext.Request.Protocol
    Write-KrTextResponse -InputObject "Hello via $proto" -ContentType 'text/plain'
}

# 7. Start server (Ctrl+C to stop)
Start-KrServer
