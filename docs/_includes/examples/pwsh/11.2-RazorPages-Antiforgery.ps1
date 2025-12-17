<#
    Sample Kestrun Server on how to configure antiforgery protection with Razor Pages.
    These examples demonstrate how to configure antiforgery protection in a Kestrun server with Razor Pages.
    FileName: 11.2-RazorPages-Antiforgery.ps1
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
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -SelfSignedCertificate

# Add a Razor Pages handler to the server
Add-KrPowerShellRazorPagesRuntime -RootPath './Assets/Pages'

# Application-wide metadata (AVAILABLE TO ALL RUNSPACES)
$AppInfo = [pscustomobject]@{
    Name = 'Kestrun Razor Demo with Antiforgery'
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

# Add SignalR with KestrunHub
Add-KrSignalRHubMiddleware -Path '/hubs/kestrun'

# Add Tasks Service
Add-KrTasksService

# --- Antiforgery: modern SPA preset ---
# Cookie: .Kestrun.AntiXSRF (HttpOnly, Secure, SameSite=Lax)
# Header: X-CSRF-TOKEN
Add-KrAntiforgeryMiddleware -CookieName '.Kestrun.AntiXSRF' -HeaderName 'X-CSRF-TOKEN'

# Enable Kestrun configuration
Enable-KrConfiguration

# Add token endpoint
Add-KrAntiforgeryTokenRoute -Path '/csrf-token'

Add-KrMapRoute -Verbs Post -Pattern '/api/operation/start' {
    # Start a long-running operation as a Task and report progress via SignalR
    $seconds = Get-KrRequestQuery -Name 'seconds' -AsInt
    # Default to 30 seconds if not specified or invalid
    if ($seconds -le 0) { $seconds = 30 }

    # Define steps
    $stepMs = 500
    $steps = [Math]::Ceiling(($seconds * 1000.0) / $stepMs)

    # Start the task
    $taskId = New-KrTask -ScriptBlock {
        Send-KrSignalREvent -EventName 'OperationProgress' -Data @{
            TaskId = $TaskId
            Progress = 0
            Message = 'Started'
            Timestamp = (Get-Date)
        }

        for ($i = 1; $i -le $steps; $i++) {
            # Cooperative cancellation (Stop-KrTask triggers this)
            if ($TaskCancellationToken.IsCancellationRequested) {
                Send-KrSignalREvent -EventName 'OperationComplete' -Data @{
                    TaskId = $TaskId
                    Progress = [int](($i - 1) * 100 / $steps)
                    Message = 'Cancelled'
                    Timestamp = (Get-Date)
                }
                return
            }
            # Sleep to simulate work
            Start-Sleep -Milliseconds $stepMs
            # Report progress
            Send-KrSignalREvent -EventName 'OperationProgress' -Data @{
                TaskId = $TaskId
                Progress = [int]($i * 100 / $steps)
                Message = "Step $i / $steps"
                Timestamp = (Get-Date)
            }
        }
        # Report completion
        Send-KrSignalREvent -EventName 'OperationComplete' -Data @{
            TaskId = $TaskId
            Progress = 100
            Message = 'Completed'
            Timestamp = (Get-Date)
        }
    } -Arguments @{ steps = $steps; stepMs = $stepMs } -AutoStart

    Write-KrJsonResponse -InputObject @{
        Success = $true
        TaskId = $taskId
        Message = 'Task started'
    }
}

Add-KrMapRoute -Verbs Post -Pattern '/tasks/cancel' {

    $id = Get-KrRequestQuery -Name 'id' -AsString
    if ([string]::IsNullOrWhiteSpace($id)) {
        Write-KrJsonResponse -StatusCode 400 -InputObject @{
            Error = 'Missing id'
        }
        return
    }

    Stop-KrTask -Id $id

    Write-KrJsonResponse -InputObject @{
        Success = $true
        TaskId = $id
        Message = 'Cancel requested'
    }
}

# Start the server asynchronously
Start-KrServer
