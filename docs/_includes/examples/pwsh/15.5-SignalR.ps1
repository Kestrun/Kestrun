<#
    Create a SignalR demo server with Kestrun in PowerShell.
    FileName: 15.5-SignalR.ps1
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Initialize Kestrun root directory
Initialize-KrRoot -Path $PSScriptRoot

## 1. Logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole | Register-KrLogger -Name 'SignalRDemo' -SetAsDefault

## 2. Create Server
New-KrServer -Name 'Kestrun SignalR Demo'

## 3. Configure Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

## 4. Add SignalR with KestrunHub
Add-KrSignalRHubMiddleware -Path '/hubs/kestrun'

## 5. Enable Scheduler (must be added before configuration)
Add-KrScheduling

# Register the ad-hoc Tasks feature (PowerShell, C#, VB.NET)
Add-KrTasksService

## 6. Enable Configuration
Enable-KrConfiguration

## 7. Add Routes

# Home page with SignalR client
Add-KrHtmlTemplateRoute -Pattern '/' -HtmlTemplatePath 'Assets/wwwroot/signal-r.html'

# Route to broadcast logs via PowerShell
Add-KrMapRoute -Verbs Get -Pattern '/api/ps/log/{level}' {
    $level = Get-KrRequestRouteParam -Name 'level'

    $timestamp = (Get-Date -Format 'HH:mm:ss')
    Write-KrLog -Level $level -Message "Test $level message from PowerShell at $timestamp"
    Send-KrSignalRLog -Level $level -Message "Test $level message from PowerShell at $timestamp"
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
    $seconds = Get-KrRequestQuery -Name 'seconds' -AsInt
    if ($seconds -le 0) { $seconds = 2 }

    Write-KrLog -Level Information -Message 'Starting long operation for {seconds} seconds' -Values $seconds

    $id = New-KrTask -ScriptBlock {
        for ($i = 1; $i -le $seconds; $i++) {
            Start-Sleep -Milliseconds 1000

            $TaskProgress.StatusMessage = "Sleeping ($i/$seconds)"
            $TaskProgress.PercentComplete = ($i * 100 / $seconds)
            Write-KrLog -Level Information -Message 'Operation {TaskId} progress: {i}/{seconds} {PercentComplete}%' -Values $TaskId, $i, $seconds, $TaskProgress.PercentComplete
            $message = @{
                TaskId = $TaskId
                Progress = $TaskProgress.PercentComplete
                Step = $i
                Message = "Processing step $i of $seconds..."
                Timestamp = (Get-Date)
            }

            # Broadcast progress to all clients using the generic event channel the HTML listens to (ReceiveEvent)
            try {
                Send-KrSignalREvent -EventName 'OperationProgress' -Data $message
                # Alternatively, to use the dedicated handler: Send-KrSignalRGroupMessage -GroupName 'OperationProgress' -Method 'ReceiveOperationProgress' -Message $message
            } catch {
                Write-Warning "Failed to broadcast progress: $_"
            }
        }

        # Send completion message
        $completionMessage = @{
            TaskId = $TaskId
            Progress = $TaskProgress.PercentComplete
            Message = $TaskProgress.StatusMessage
            Timestamp = (Get-Date)
        }

        try {
            # Broadcast completion on the same event channel the HTML expects
            Send-KrSignalREvent -EventName 'OperationComplete' -Data $completionMessage
            # Alternatively, for the dedicated handler: Send-KrSignalRGroupMessage -GroupName 'OperationProgress' -Method 'ReceiveOperationProgress' -Message $completionMessage
        } catch {
            Write-Warning "Failed to broadcast completion: $_"
        }
    } -Arguments @{ seconds = $seconds } -AutoStart

    $message = "Starting long operation with ID: $id"
    Set-KrTaskName -Id $id -Name "LongOperation-$id" -Description $message

    Write-KrLog -Level Information -Message 'Long operation started: {id}' -Values $id
    
    Write-KrJsonResponse -InputObject @{
        Success = $true
        TaskId = $id
        Message = $message
    } -StatusCode 200
}

# Route to get operation status
Add-KrMapRoute -Verbs Get -Pattern '/api/operation/status/{taskId}' {
    $taskId = Get-KrRequestRouteParam -Name 'taskId'
    Write-KrLog -Level Information -Message 'Status requested for operation: {taskId}' -Values $taskId
    $task = Get-KrTask -Id $taskId

    if ( $null -ne $task ) {
        Write-KrLog -Level Information -Message 'Found task for operation {taskId} with status {Status}' -Values $taskId, $task.Status

    } else {
        Write-KrLog -Level Warning -Message 'No task found for operation {taskId}' -Values $taskId
        Write-KrJsonResponse -InputObject @{
            taskId = $taskId
            Message = "No task found for operation '$taskId'"
        } -StatusCode 404
        return
    }


    $progress = $null
    $status = $null
    if ($task.Progress) {
        $progress = $task.Progress.PercentComplete
        $status = $task.Progress.StatusMessage
    }

    Write-KrJsonResponse -InputObject @{
        OperationId = $operationId
        TaskId = $taskId
        State = $task.StateText
        StartedAt = $task.StartedAt
        CompletedAt = $task.CompletedAt
        Progress = $progress
        Status = $status
        Message = 'Operation status retrieved'
    } -StatusCode 200
}

# Register a scheduled task that broadcasts to all clients every 30 seconds
Register-KrSchedule -Name 'HeartbeatBroadcast' -Cron '*/30 * * * * *' -ScriptBlock {
    $heartbeatMessage = @{
        Type = 'Heartbeat'
        ServerTime = (Get-Date)
        Uptime = Get-KrServer -Uptime
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

