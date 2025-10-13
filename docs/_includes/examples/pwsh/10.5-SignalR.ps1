<#
    Create a SignalR demo server with Kestrun in PowerShell.
    FileName: 10.5-SignalR.ps1
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Initialize Kestrun root directory
Initialize-KrRoot -Path $PSScriptRoot

## 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'SignalRDemo' -SetAsDefault

## 2. Create Server
New-KrServer -Name 'Kestrun SignalR Demo'

## 3. Configure Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

## 4. Add SignalR with KestrunHub
Add-KrSignalRHubMiddleware -Path '/hubs/kestrun'

## 5. Enable Scheduler (must be added before configuration)
Add-KrScheduling

## 6. Enable Configuration
Enable-KrConfiguration

## 7. Add Routes

# Home page with SignalR client
Add-KrHtmlTemplateRoute -Pattern '/' -HtmlTemplatePath 'Assets/wwwroot/signal-r.html'

# Route to broadcast logs via PowerShell
Add-KrMapRoute -Verbs Get -Pattern '/api/ps/log/{level}' {
    $level = Get-KrRequestRouteParam -Name 'level'
    Write-KrLog -Level $level -Message "Test $level message from PowerShell at $(Get-Date -Format 'HH:mm:ss')"
    Send-KrSignalRLog -Level $level -Message "Test $level message from PowerShell at $(Get-Date -Format 'HH:mm:ss')"
    Write-KrTextResponse -InputObject "Broadcasted $level log message from PowerShell" -StatusCode 200
}

# Route to broadcast custom events via PowerShell
Add-KrMapRoute -Verbs Get -Pattern '/api/ps/event' {
    Send-KrSignalREvent -EventName 'PowerShellEvent' -Data @{
        Message = 'Hello from PowerShell'
        Timestamp = (Get-Date)
        Random = Get-Random -Minimum 1 -Maximum 100
    }
    Write-KrTextResponse -InputObject 'Broadcasted custom event from PowerShell' -StatusCode 200
}

# Route to join a SignalR group
Add-KrMapRoute -Verbs Post -Pattern '/api/group/join/{groupName}' {
    $groupName = Get-KrRequestRouteParam -Name 'groupName'

    # This would normally be handled by the hub itself, but we can broadcast a notification
    Send-KrSignalREvent -EventName 'GroupJoinRequest' -Data @{
        GroupName = $groupName
        Message = "Request to join group: $groupName"
        Timestamp = (Get-Date)
    }

    Write-KrJsonResponse -InputObject @{
        Success = $true
        Message = "Join request sent for group: $groupName"
        GroupName = $groupName
    } -StatusCode 200
}

# Route to leave a SignalR group
Add-KrMapRoute -Verbs Post -Pattern '/api/group/leave/{groupName}' {
    $groupName = Get-KrRequestRouteParam -Name 'groupName'

    # This would normally be handled by the hub itself, but we can broadcast a notification
    Send-KrSignalREvent -EventName 'GroupLeaveRequest' -Data @{
        GroupName = $groupName
        Message = "Request to leave group: $groupName"
        Timestamp = (Get-Date)
    }

    Write-KrJsonResponse -InputObject @{
        Success = $true
        Message = "Leave request sent for group: $groupName"
        GroupName = $groupName
    } -StatusCode 200
}

# Route to broadcast to a specific group
Add-KrMapRoute -Verbs Post -Pattern '/api/group/broadcast/{groupName}' {
    $groupName = Get-KrRequestRouteParam -Name 'groupName'

    Send-KrSignalRGroupMessage -GroupName $groupName -Method 'ReceiveGroupMessage' -Message @{
        Message = "Hello from PowerShell to group: $groupName"
        Timestamp = (Get-Date)
        Sender = 'PowerShell Route'
    }

    Write-KrJsonResponse -InputObject @{
        Success = $true
        Message = "Broadcasted message to group: $groupName"
        GroupName = $groupName
    } -StatusCode 200
}

# Route to start a long-running operation with progress updates
Add-KrMapRoute -Verbs Post -Pattern '/api/operation/start' {
    $operationId = [Guid]::NewGuid().ToString()
    # Expand-KrObject $krserver
    # Start a background job to simulate long operation
    $job = Start-Job -ScriptBlock {
        param($OperationId, $ServerInstance)

        # Import the Kestrun module in the background job
        Import-Module $using:PSScriptRoot\..\..\..\src\PowerShell\Kestrun\Kestrun.psm1 -Force

        for ($i = 1; $i -le 10; $i++) {
            Start-Sleep -Seconds 2

            $progress = $i * 10
            $message = @{
                OperationId = $OperationId
                Progress = $progress
                Step = $i
                Message = "Processing step $i of 10..."
                Timestamp = (Get-Date)
            }

            # Broadcast progress to all clients
            try {
                [Kestrun.Hosting.KestrunHostSignalRExtensions]::BroadcastEventAsync($ServerInstance, 'OperationProgress', $message, $null, [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
            } catch {
                Write-Warning "Failed to broadcast progress: $_"
            }
        }

        # Send completion message
        $completionMessage = @{
            OperationId = $OperationId
            Progress = 100
            Message = 'Operation completed successfully!'
            Timestamp = (Get-Date)
        }

        try {
            [Kestrun.Hosting.KestrunHostSignalRExtensions]::BroadcastEventAsync($ServerInstance, 'OperationComplete', $completionMessage, $null, [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
        } catch {
            Write-Warning "Failed to broadcast completion: $_"
        }
    } -ArgumentList $operationId, $KrServer

    Write-KrJsonResponse -InputObject @{
        Success = $true
        OperationId = $operationId
        JobId = $job.Id
        Message = 'Long operation started'
    } -StatusCode 200
}

# Route to get operation status
Add-KrMapRoute -Verbs Get -Pattern '/api/operation/status/{operationId}' {
    $operationId = Get-KrRequestRouteParam -Name 'operationId'

    # In a real application, you'd track operation status in a database or cache
    Write-KrJsonResponse -InputObject @{
        OperationId = $operationId
        Message = 'Operation status retrieved'
        Note = 'This is a demo - real status tracking would require persistent storage'
    } -StatusCode 200
}

# Register a scheduled task that broadcasts to all clients every 30 seconds
Register-KrSchedule -Name 'HeartbeatBroadcast' -Cron '*/30 * * * * *' -ScriptBlock {
    $heartbeatMessage = @{
        Type = 'Heartbeat'
        ServerTime = (Get-Date)
        Uptime =  Get-KrServer -Uptime
        ConnectedClients = Get-KrSignalRConnectedClient
        Message = 'Server heartbeat from scheduled task'
    }
    Write-KrLog -Level Information -Message 'Broadcasting heartbeat:{heartbeatMessage}' -Values $heartbeatMessage
    # Broadcast heartbeat to all connected clients
    Send-KrSignalREvent -EventName 'ServerHeartbeat' -Data $heartbeatMessage
}

# Register a scheduled task that broadcasts to the "Admins" group every minute
Register-KrSchedule -Name 'AdminStatusUpdate' -Cron '0 * * * * *' -ScriptBlock {
    $statusMessage = @{
        Type = 'AdminUpdate'
        SystemInfo = @{
            ProcessorCount = $env:NUMBER_OF_PROCESSORS
            MachineName = $env:COMPUTERNAME
            UserName = $env:USERNAME
        }
        Timestamp = (Get-Date)
        Message = 'Scheduled admin status update'
    }
    Write-KrLog -Level Information -Message 'Broadcasting admin status update :{statusMessage}' -Values $statusMessage
    # Broadcast to admin group only
    Send-KrSignalRGroupMessage -GroupName 'Admins' -Method 'ReceiveAdminUpdate' -Message $statusMessage
}

## 8. Start Server

Write-Host '🟢 Kestrun SignalR Demo Server Started' -ForegroundColor Green
Write-Host '📍 Navigate to http://localhost:5000 to see the demo' -ForegroundColor Cyan
Write-Host '🔌 SignalR Hub available at: http://localhost:5000/hubs/kestrun' -ForegroundColor Cyan

Start-KrServer -CloseLogsOnExit
