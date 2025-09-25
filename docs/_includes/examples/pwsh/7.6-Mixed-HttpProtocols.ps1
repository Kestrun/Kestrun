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

# For HTTPS listeners, create a self-signed cert (or load from store/file as needed)
$cert = New-KrSelfSignedCertificate -DnsNames localhost, 127.0.0.1 -Exportable -ValidDays 30

# 3. Listeners with explicit protocol selections
#    - 5001: HTTP/1.1 only
Add-KrEndpoint -Port 5001 -IPAddress ([IPAddress]::Loopback) -Protocols ([Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1) -X509Certificate $cert
#    - 5002: HTTP/2 only (no HTTP/1.1)
Add-KrEndpoint -Port 5002 -IPAddress ([IPAddress]::Loopback) -Protocols ([Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http2) -X509Certificate $cert
#    - 5003: HTTP/3 only (QUIC) Requires OS + runtime support; may need environment variable ASPNETCORE_URLS / alt config.
Add-KrEndpoint -Port 5003 -IPAddress ([IPAddress]::Loopback) -Protocols ([Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http3) -X509Certificate $cert
#    - 5004: Combined HTTP/1.1 + HTTP/2 (single listener negotiating via ALPN when TLS used; here plain for demo)
Add-KrEndpoint -Port 5004 -IPAddress ([IPAddress]::Loopback) -Protocols ([Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols]::Http1AndHttp2) -X509Certificate $cert

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
Start-KrServer -CloseLogsOnExit
