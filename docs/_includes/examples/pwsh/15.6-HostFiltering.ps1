<#
    Sample Kestrun Server – Host Filtering
    Demonstrates enabling Host Filtering middleware and a simple route.
    FileName: 15.6-HostFiltering.ps1
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Optional: console logger so we can see events
New-KrLogger | Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 1) Create server
New-KrServer -Name 'Endpoints HostFiltering'

# 2) Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# 3)  Configure Host Filtering via parameters
#    - Only allow requests with Host header "example.com" or "www.example.com"
#    - Reject empty Host headers
#    - Exclude failure message body for blocked hosts
Add-KrHostFiltering `
    -AllowedHosts @('example.com', 'www.example.com') `
    -NotAllowEmptyHosts

# 4) Enable Kestrun configuration
Enable-KrConfiguration

# 5) Map a simple route
Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    Write-KrLog -Level Information -Message 'Processing /hello request'
    Write-KrTextResponse -InputObject 'Hello from host-filtered server' -StatusCode 200
}

# 6) Run
Start-KrServer -CloseLogsOnExit

