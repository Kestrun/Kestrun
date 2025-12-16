<#
    Sample Kestrun Server on how to configure a static file server.
    These examples demonstrate how to configure static routes with directory browsing in a Kestrun server.
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
New-KrServer -Name "RazorPages"

# Add a listener on the configured port and IP address
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Add a Razor Pages handler to the server
Add-KrPowerShellRazorPagesRuntime #-PathPrefix '/Assets'

# Application-wide metadata (AVAILABLE TO ALL RUNSPACES)
$AppInfo = [pscustomobject]@{
    Name = "Kestrun Razor Demo"
    Environment = "Development"
    StartedUtc = [DateTime]::UtcNow
    Version = "0.9.0-preview"
}

Write-KrLog -Level Information -Message "Starting Kestrun RazorPages server '{name}' version {version} in {environment} environment on {ipaddress}:{port}" -Values $AppInfo.Name, $AppInfo.Version, $AppInfo.Environment, $IPAddress, $Port

# Define feature flags for the application
$FeatureFlags = @{
    RazorPages = $true
    Cancellation = $true
    HotReload = $false
}

Write-KrLog -Level Information -Message "Feature Flags: {featureflags}" -Values $($FeatureFlags | ConvertTo-Json -Depth 3)

# Define a Message of the Day (MOTD) accessible to all pages
$Motd = @"
Welcome to Kestrun.
This message comes from the main server script.
Defined once, visible everywhere.
"@

Write-KrLog -Level Information -Message "Message of the Day: {motd}" -Values $Motd

# Add SignalR with KestrunHub
Add-KrSignalRHubMiddleware -Path '/hubs/kestrun'

# Add Tasks Service
Add-KrTasksService
# Enable Kestrun configuration
Enable-KrConfiguration
# Requires Add-KrSignalRHubMiddleware and Add-KrTasksService before Enable-KrConfiguration :contentReference[oaicite:2]{index=2}

Add-KrMapRoute -Verbs Post -Pattern '/api/cancel/start' {
    $seconds = Get-KrRequestQuery -Name 'seconds' -AsInt
    $stepMs = Get-KrRequestQuery -Name 'stepMs' -AsInt
    if ($seconds -le 0) { $seconds = 60 }
    if ($stepMs -le 0) { $stepMs = 500 }

    $id = New-KrTask -ScriptBlock {
        $steps = [Math]::Ceiling(($seconds * 1000.0) / $stepMs)
        for ($i = 1; $i -le $steps; $i++) {
            Start-Sleep -Milliseconds $stepMs

            # Broadcast progress (include TaskId so the browser can filter)
            Send-KrSignalREvent -EventName 'CancelProgress' -Data @{
                TaskId = $TaskId
                Step = $i
                Steps = $steps
                Timestamp = (Get-Date)
            }
        }

        Send-KrSignalREvent -EventName 'CancelComplete' -Data @{
            TaskId = $TaskId
            Timestamp = (Get-Date)
        }
    } -Arguments @{ seconds = $seconds; stepMs = $stepMs } -AutoStart

    Write-KrJsonResponse -InputObject @{ Success = $true; TaskId = $id } -StatusCode 200
}


Add-KrMapRoute -Verbs Get -Pattern '/api/operation/start' {
    $seconds = Get-KrRequestQuery -Name 'seconds' -AsInt
    Write-Host $seconds
    if ($seconds -le 0) { $seconds = 30 }

    $stepMs = 500
    $steps = [Math]::Ceiling(($seconds * 1000.0) / $stepMs)

    $taskId = New-KrTask -ScriptBlock {
        param($steps, $stepMs)

        Send-KrSignalREvent -EventName 'OperationProgress' -Data @{
            TaskId = $TaskId
            Progress = 0
            Message = "Started"
            Timestamp = (Get-Date)
        }

        for ($i = 1; $i -le $steps; $i++) {

            # Cooperative cancellation (Stop-KrTask triggers this)
            if ($TaskCancellationToken.IsCancellationRequested) {
                Send-KrSignalREvent -EventName 'OperationComplete' -Data @{
                    TaskId = $TaskId
                    Progress = [int](($i - 1) * 100 / $steps)
                    Message = "Cancelled"
                    Timestamp = (Get-Date)
                }
                return
            }

            Start-Sleep -Milliseconds $stepMs

            Send-KrSignalREvent -EventName 'OperationProgress' -Data @{
                TaskId = $TaskId
                Progress = [int]($i * 100 / $steps)
                Message = "Step $i / $steps"
                Timestamp = (Get-Date)
            }
        }

        Send-KrSignalREvent -EventName 'OperationComplete' -Data @{
            TaskId = $TaskId
            Progress = 100
            Message = "Completed"
            Timestamp = (Get-Date)
        }

    } -Arguments @{ steps = $steps; stepMs = $stepMs } -AutoStart

    Write-KrJsonResponse -InputObject @{
        Success = $true
        TaskId = $taskId
        Message = "Task started"
    }
}

Add-KrMapRoute -Verbs Get -Pattern '/tasks/cancel' {

    $id = $Context.Request.Query["id"]
    if ([string]::IsNullOrWhiteSpace($id)) {
        Write-KrJsonResponse -StatusCode 400 -InputObject @{
            Error = "Missing id"
        }
        return
    }

    Stop-KrTask -Id $id

    Write-KrJsonResponse -InputObject @{
        Success = $true
        TaskId = $id
        Message = "Cancel requested"
    }
}

# Start the server asynchronously
Start-KrServer
