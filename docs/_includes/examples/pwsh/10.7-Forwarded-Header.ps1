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



# Enable Forwarded Headers middleware. Trust loopback so tests/locals can pass headers.
# Process X-Forwarded-For, X-Forwarded-Proto, and X-Forwarded-Host.
# Limit to 1 forward (the first value in a comma-delimited list).
# Trust local reverse proxies (loopback)
Add-ForwardedHeader -ForwardedHeaders 'XForwardedFor', 'XForwardedProto'

# Map a diagnostic route that reveals forwarded effects
Add-KrMapRoute -Verbs Get -Pattern '/forward' -ScriptBlock {
    Expand-KrObject -InputObject $context.HttpContext
    $scheme = $Context.Request.Scheme
    $requestHost = $Context.Request.Host
    $remoteIp = $null
    if ($Context.HttpContext.Connection.RemoteIpAddress) {
        $remoteIp = $Context.HttpContext.Connection.RemoteIpAddress.ToString()
    }
    $payload = [ordered]@{
        scheme = $scheme
        host = $requestHost
        remoteIp = $remoteIp
    }
    Write-KrJsonResponse -InputObject $payload -StatusCode 200
}

# Enable Kestrun configuration
Enable-KrConfiguration

# Initial informational log
Write-KrLog -Level Information -Message 'Server {Name} configured.' -Values 'Forwarded Headers Demo'

# Start the server and close all the loggers when the server stops
# This is equivalent to calling Close-KrLogger after Start-KrServer
Start-KrServer -CloseLogsOnExit
