<#
    Sample Kestrun Server on how to add a favicon.
    This example demonstrates how to configure a favicon in a Kestrun server.
    FileName: 3.4-Add-FavIcon.ps1
#>

# Import the Kestrun module
Install-PSResource -Name Kestrun

New-KrLogger |
    Set-KrMinimumLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'DefaultLogger' -SetAsDefault

# Initialize Kestrun root directory
# the default value is $PWD
# This is recommended in order to use relative paths without issues
Initialize-KrRoot -Path $PSScriptRoot

# Create a new Kestrun server
New-KrServer -Name "Simple Server"

# Add a listener on port 5000 and IP address 127.0.0.1 (localhost)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# Add the PowerShell runtime
# !!!!Important!!!! this step is required to process PowerShell routes and middlewares
Add-KrPowerShellRuntime

$Visits = [System.Collections.Concurrent.ConcurrentDictionary[string, int]]::new()

$Delay = 3

# Enable Kestrun configuration
Enable-KrConfiguration

# ─────────────────────────────────────────────────────────────────────────────
# Route: GET /ps/show
#   • $Visits is already injected as a PS variable
#   • Just read and write it back in the response
# ─────────────────────────────────────────────────────────────────────────────
Add-KrMapRoute -Server $server -Verbs Get -Pattern '/ps/show' -ScriptBlock {
    # $Visits is available
    Write-KrTextResponse -InputObject "[Runspace: $([System.Management.Automation.Runspaces.Runspace]::DefaultRunspace.Name)] Visits so far: $($script:Visits.Count)" -StatusCode 200
}

# ─────────────────────────────────────────────────────────────────────────────
# Route: GET /ps/visit
#   • Increment $Visits directly
#   • Persist the new value back into the global store
# ─────────────────────────────────────────────────────────────────────────────
Add-KrMapRoute -Server $server -Verbs Get -Pattern '/ps/visit' -ScriptBlock {
    # Simulate some delay
    Start-Sleep -Seconds $script:Delay
    # increment the injected variable
    $script:Visits.Count++

    Write-KrTextResponse -InputObject "[Runspace: $([System.Management.Automation.Runspaces.Runspace]::DefaultRunspace.Name)] Incremented to $($script:Visits.Count)" -StatusCode 200
}

# Start the server asynchronously
Start-KrServer
