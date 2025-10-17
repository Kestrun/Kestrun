<#
    Sample Kestrun Server - Forwarded Headers
    This script demonstrates enabling Forwarded Headers middleware
    and exposing a diagnostic route that returns key request fields
    (scheme, host, and remote IP) to show header effects.
    FileName: 10.7-Forwarded-Header.ps1
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# (Optional) Configure console logging so we can see events
New-KrLogger | Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# Create a new Kestrun server
New-KrServer -Name 'Forwarded Headers Demo'

# Add a listener on the configured port and IP address
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Enable Forwarded Headers middleware. Trust loopback so tests/local can pass headers.
# Process X-Forwarded-For, X-Forwarded-Proto, and X-Forwarded-Host.
# Limit to 1 forward (the first value in a comma-delimited list).
# Trust local reverse proxies (loopback)
Add-ForwardedHeader -XForwardedFor -XForwardedProto -XForwardedHost -XForwardedPrefix `
    -KnownProxies '127.0.0.1' -ForwardLimit 1

# Enable Kestrun configuration
Enable-KrConfiguration

# Map a diagnostic route that reveals forwarded effects
Add-KrMapRoute -Verbs Get -Pattern '/forward' -ScriptBlock {
  # 1) Effective host & hostPort (prefer HostString; fallback to raw Host header)
    $hs         = $Context.Request.Host
    $hostOnly   = $hs.Host
    $hostPort   = $hs.Port   # nullable int

    if ([string]::IsNullOrEmpty($hostOnly)) {
        $rawHost = [string]$Context.Request.Headers['Host']
        if ($rawHost) {
            $hs       = [Microsoft.AspNetCore.Http.HostString]::FromUriComponent($rawHost)
            $hostOnly = $hs.Host
            $hostPort = $hs.Port
        }
    }

    $scheme   = $Context.Request.Scheme
    $basePath = $Context.Request.PathBase.ToString()
    $path     = $Context.Request.Path.ToString()

    # 2) Connection info (what Kestrel is bound to, and the peer)
    if ($null -eq $Context.Connection.LocalIpAddress) {
        $localIp = ''
    } else {
        $localIp = $Context.Connection.LocalIpAddress.ToString()
    }

    $localPort  = $Context.Connection.LocalPort    # Kestrun alias may be .Port
    if ($null -eq $Context.Connection.RemoteIpAddress) {
        $remoteIp = ''
    } else {
        $remoteIp = $Context.Connection.RemoteIpAddress.ToString()
    }

    $remotePort = $Context.Connection.RemotePort

    # 3) Host-with-port for display: include port only if non-default for scheme
    $isDefaultPort = ($scheme -eq 'https' -and $hostPort -eq 443) -or
                     ($scheme -eq 'http'  -and $hostPort -eq 80)

    if ($hostPort -and -not $isDefaultPort) {
        $hostWithPort = "$hostOnly`:$hostPort"
    } else {
        $hostWithPort = $hostOnly
    }
    if($null -eq $hostPort) {
        $hostPort = ''
    }

    Write-KrJsonResponse @{
        scheme          = $scheme
        host            = $hostOnly
        hostPort        = $hostPort           # from Host header (may be null)
        hostWithPort    = $hostWithPort
        basePath        = $basePath
        path            = $path
        fullPath        = "$basePath$path"

        # Connection (socket) facts
        localIp         = $localIp
        localPort       = $localPort          # what Kestrel/listener is using
        remoteIp        = $remoteIp           # respects X-Forwarded-For if trusted
        remotePort      = $remotePort
    }
}

# Initial informational log
Write-KrLog -Level Information -Message 'Server {Name} configured.' -Values 'Forwarded Headers Demo'

# Start the server and close all the loggers when the server stops
# This is equivalent to calling Close-KrLogger after Start-KrServer
Start-KrServer -CloseLogsOnExit
