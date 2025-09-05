<#
    Sample Kestrun Server on how to use shared variables.
    This example demonstrates how to use shared variables in a Kestrun server.
    FileName: 4.1-Shared-Variables.ps1
#>

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

# Shared variables
$Visits = [System.Collections.Concurrent.ConcurrentDictionary[string, int]]::new()

# Shared variable for delay to simulate processing time and force the use of multiple runspaces
$Delay = 1

# Enable Kestrun configuration
Enable-KrConfiguration

# Show the current visit count
Add-KrMapRoute -Server $server -Verbs Get -Pattern '/show' -ScriptBlock {
    # $Visits is available
    Write-KrTextResponse -InputObject "[Runspace: $([System.Management.Automation.Runspaces.Runspace]::DefaultRunspace.Name)] Visits so far: $($Visits.Count)" -StatusCode 200
}

# Route: GET /visit
Add-KrMapRoute -Server $server -Verbs Get -Pattern '/visit' -ScriptBlock {
    # Simulate some delay
    Start-Sleep -Seconds $Delay
    # increment the injected variable
    $Visits.AddOrUpdate("Count", 1, { param($k, $v) $v + 1 })
    Write-KrTextResponse -InputObject "[Runspace: $([System.Management.Automation.Runspaces.Runspace]::DefaultRunspace.Name)] Incremented to $($Visits.Count)" -StatusCode 200
}

# Start the server asynchronously
Start-KrServer
