<#
    15.1 Start / Stop Patterns
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Create server host
$srv = New-KrServer -Name 'Lifecycle Demo' -PassThru


# 3. Add loopback listener on port 5000 (auto unlinks existing file if present)
# This listener will be used to demonstrate server limits configuration.
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# 4. Add PowerShell runtime for script routes and middlewares
Add-KrPowerShellRuntime

# 6. Finalize configuration and set server limits
Enable-KrConfiguration

# 7. Map a simple info route to demonstrate server options in action
Add-KrMapRoute -Verbs Get -Pattern '/status' -ScriptBlock {
    Write-KrLog -Level Debug -Message 'Health check'
    Write-KrJsonResponse -InputObject @{ status = 'healthy' }
}

# 8. Start the server (runs asynchronously; press Ctrl+C to stop)
Start-KrServer -Server $srv -NoWait



Write-KrLog -Level Information -Message 'Server started (non-blocking).'
Write-Host 'Server running for 30 seconds'
Start-Sleep 30

Stop-KrServer -Server $srv
Write-KrLog -Level Information -Message 'Server stopped.'
Close-KrLogger
