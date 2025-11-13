<#
    Sample Kestrun Server demonstrating shared state management.
    This example shows how to use Set-KrSharedState, Get-KrSharedState, and Remove-KrSharedState cmdlets.
    FileName: 4.2-Shared-State.ps1
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Initialize Kestrun root directory
Initialize-KrRoot -Path $PSScriptRoot

# Create a new Kestrun server
New-KrServer -Name 'Shared State Server'

# Add a listener using provided parameters
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Define shared state using Set-KrSharedState
# This creates thread-safe, cross-runspace accessible state
Set-KrSharedState -Name 'Visits' -Value @{ Count = 0 } -ThreadSafe

Set-KrSharedState -Name 'Config' -Value @{ MaxVisits = 100; AppName = 'SharedStateDemo' } -ThreadSafe
Set-KrSharedState -Name 'StartTime' -Value @{ Time = (Get-Date) } -ThreadSafe

# Enable Kestrun configuration
Enable-KrConfiguration

# Route: GET /info - Show configuration from shared state
Add-KrMapRoute -Verbs Get -Pattern '/info' -ScriptBlock {
    $config = Get-KrSharedState -Name 'Config'
    $startTime = Get-KrSharedState -Name 'StartTime'
    $uptime = (Get-Date) - $startTime.Time

    $response = @{
        appName = $config.AppName
        maxVisits = $config.MaxVisits
        uptimeSeconds = [math]::Round($uptime.TotalSeconds, 2)
        runspace = [System.Management.Automation.Runspaces.Runspace]::DefaultRunspace.Name
    }

    Write-KrJsonResponse -InputObject $response -StatusCode 200
}

# Route: GET /visits - Show current visit count
Add-KrMapRoute -Verbs Get -Pattern '/visits' -ScriptBlock {
    $visits = Get-KrSharedState -Name 'Visits'

    Write-KrJsonResponse -InputObject @{
        count = $visits.Count
        runspace = [System.Management.Automation.Runspaces.Runspace]::DefaultRunspace.Name
    } -StatusCode 200
}

# Route: POST /visit - Increment visit counter
Add-KrMapRoute -Verbs Post -Pattern '/visit' -ScriptBlock {
    $visits = Get-KrSharedState -Name 'Visits'
    $config = Get-KrSharedState -Name 'Config'

    Start-Sleep -Seconds 2  # Simulate some processing delay
    # Thread-safe increment
    $visits.Count = [int]$visits.Count + 1

    $response = @{
        count = $visits.Count
        maxVisits = $config.MaxVisits
        remaining = $config.MaxVisits - $visits.Count
        runspace = [System.Management.Automation.Runspaces.Runspace]::DefaultRunspace.Name
    }

    Write-KrJsonResponse -InputObject $response -StatusCode 200
}

# Route: POST /reset - Reset visit counter
Add-KrMapRoute -Verbs Post -Pattern '/reset' -ScriptBlock {
    $visits = Get-KrSharedState -Name 'Visits'
    $visits.Count = 0

    Write-KrJsonResponse -InputObject @{
        message = 'Visit counter reset'
        count = 0
        runspace = [System.Management.Automation.Runspaces.Runspace]::DefaultRunspace.Name
    } -StatusCode 200
}

# Route: PUT /config - Update configuration
Add-KrMapRoute -Verbs Put -Pattern '/config' -ScriptBlock {
    $body = Get-KrRequestBody
    $config = Get-KrSharedState -Name 'Config'

    if ($body.maxVisits) {
        $config.MaxVisits = [int]$body.maxVisits
    }
    if ($body.appName) {
        $config.AppName = $body.appName
    }

    Write-KrJsonResponse -InputObject @{
        message = 'Configuration updated'
        config = $config
        runspace = [System.Management.Automation.Runspaces.Runspace]::DefaultRunspace.Name
    } -StatusCode 200
}

# Route: DELETE /state/{name} - Remove shared state variable
Add-KrMapRoute -Verbs Delete -Pattern '/state/{name}' -ScriptBlock {
    $name = Get-KrRequestRouteParam -Name 'name'

    if ($name -in @('Visits', 'Config', 'StartTime')) {
        Write-KrJsonResponse -InputObject @{
            error = 'Cannot remove core state variables'
            name = $name
        } -StatusCode 403
        return
    }

    $removed = Remove-KrSharedState -Name $name

    if ($removed) {
        Write-KrJsonResponse -InputObject @{
            message = 'State variable removed'
            name = $name
        } -StatusCode 200
    } else {
        Write-KrJsonResponse -InputObject @{
            error = 'State variable not found'
            name = $name
        } -StatusCode 404
    }
}

# Route: GET /state/{name} - Get specific shared state value
Add-KrMapRoute -Verbs Get -Pattern '/state/{name}' -ScriptBlock {
    $name = Get-KrRequestRouteParam -Name 'name'
    $value = Get-KrSharedState -Name $name

    if ($null -eq $value) {
        Write-KrJsonResponse -InputObject @{
            error = 'State variable not found'
            name = $name
        } -StatusCode 404
        return
    }

    Write-KrJsonResponse -InputObject @{
        name = $name
        value = $value
        type = $value.GetType().FullName
        runspace = [System.Management.Automation.Runspaces.Runspace]::DefaultRunspace.Name
    } -StatusCode 200
}

# Start the server
Start-KrServer
