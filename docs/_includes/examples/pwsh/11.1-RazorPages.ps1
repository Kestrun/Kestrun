<#
    Sample Kestrun Server showing PowerShell-backed Razor Pages.
    This example demonstrates enabling the Razor Pages runtime and serving pages from a root folder.
    FileName: 11.1-RazorPages.ps1
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Initialize Kestrun root directory
# the default value is $PWD
# This is recommended in order to use relative paths without issues
Initialize-KrRoot -Path $PSScriptRoot

# Create a new logger
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -SetAsDefault -Name 'DefaultLogger'

# Create a new Kestrun server
New-KrServer -Name 'RazorPages'

# Add a listener on the configured port and IP address
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Add a Razor Pages handler to the server
Add-KrPowerShellRazorPagesRuntime -RootPath './Assets/Pages'

# Application-wide metadata (AVAILABLE TO ALL RUNSPACES)
$AppInfo = [pscustomobject]@{
    Name = 'Kestrun Razor Demo'
    Environment = 'Development'
    StartedUtc = [DateTime]::UtcNow
    Version = Get-KrVersion -AsString
}

Write-KrLog -Level Information -Message "Starting Kestrun RazorPages server '{name}' version {version} in {environment} environment on {ipaddress}:{port}" `
    -Values $AppInfo.Name, $AppInfo.Version, $AppInfo.Environment, $IPAddress, $Port

# Define feature flags for the application
$FeatureFlags = @{
    RazorPages = $true
    Cancellation = $true
    HotReload = $false
}

Write-KrLog -Level Information -Message 'Feature Flags: {featureflags}' -Values $($FeatureFlags | ConvertTo-Json -Depth 3)

# Define a Message of the Day (MOTD) accessible to all pages
$Motd = @'
Welcome to Kestrun.
This message comes from the main server script.
Defined once, visible everywhere.
'@

Write-KrLog -Level Information -Message 'Message of the Day: {motd}' -Values $Motd

# Enable Kestrun configuration
Enable-KrConfiguration


# Start the server asynchronously
Start-KrServer
