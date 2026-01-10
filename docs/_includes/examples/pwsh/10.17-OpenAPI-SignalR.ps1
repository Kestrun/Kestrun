<#
    Create a SignalR demo server with Kestrun in PowerShell.
    FileName: 10.17-OpenAPI-SignalR.ps1
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

if (-not (Get-Module Kestrun)) { Import-Module Kestrun }

# Initialize Kestrun root directory
Initialize-KrRoot -Path $PSScriptRoot

## 1. Logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole | Register-KrLogger -Name 'SignalRDemoOpenApi' -SetAsDefault

## 2. Create Server
New-KrServer -Name 'Kestrun SignalR Demo with OpenAPI'

## 3. Configure Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

## 4. Add SignalR with KestrunHub
Add-KrSignalRHubMiddleware -Path '/hubs/kestrun' -IncludeNegotiateEndpoint

## 5. Enable Scheduler (must be added before configuration)
Add-KrScheduling

# Register the ad-hoc Tasks feature (PowerShell, C#, VB.NET)
Add-KrTasksService

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Kestrun SignalR Demo API' `
    -Version '1.0.0' `
    -Description 'Demonstrates generating an OpenAPI spec for SignalR-related routes and background tasks.'
# =========================================================
#           OPENAPI COMPONENT DEFINITIONS
# =========================================================

# Parameter components (reusable)
[OpenApiParameterComponent(In = 'Path', Required = $true, Description = 'Log level', Example = 'Information')]
[ValidateSet('Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical')]
[string]$level = NoDefault

[OpenApiParameterComponent(In = 'Path', Required = $true, Description = 'SignalR group name', Example = 'Admins')]
[ValidateNotNullOrEmpty()]
[string]$groupName = NoDefault

[OpenApiParameterComponent(In = 'Path', Required = $true, Description = 'Task identifier', Example = '123')]
[ValidateNotNullOrEmpty()]
[string]$taskId = NoDefault



# Schema components (reusable)
[OpenApiSchemaComponent(RequiredProperties = ('success', 'message'))]
class ApiResult {
    [OpenApiPropertyAttribute(Example = $true)]
    [bool]$success

    [OpenApiPropertyAttribute(Example = 'OK')]
    [string]$message
}

[OpenApiSchemaComponent(RequiredProperties = ('success', 'message', 'groupName'))]
class GroupResult : ApiResult {
    [OpenApiPropertyAttribute(Example = 'Admins')]
    [string]$groupName
}

[OpenApiSchemaComponent(RequiredProperties = ('success', 'taskId', 'message'))]
class OperationStartResult : ApiResult {
    [OpenApiPropertyAttribute(Example = '123')]
    [string]$taskId
}

[OpenApiSchemaComponent()]
class OperationStatusResult {
    [string]$operationId
    [string]$taskId
    [string]$state
    [datetime]$startedAt
    [datetime]$completedAt
    [double]$progress
    [string]$status
    [string]$message
}

## 6. Enable Configuration
Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Redoc -OpenApiEndpoint '/openapi/v3.1/openapi.json'

## 7. Add Routes

# Home page with SignalR client
Add-KrHtmlTemplateRoute -Pattern '/' -HtmlTemplatePath 'Assets/wwwroot/signal-r.html'

<#
.SYNOPSIS
    Broadcast a log message to all connected SignalR clients.
.PARAMETER level
    Log level to broadcast.
#>
function SendPowerShellLog {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/api/ps/log/{level}', Tags = 'SignalR')]
    [OpenApiResponse(StatusCode = '200', Description = 'Broadcasted the log message to SignalR clients', Schema = [string], ContentType = 'text/plain')]
    param(
        [OpenApiParameterRef(ReferenceId = 'level')]
        [string]$level
    )

    $timestamp = (Get-Date -Format 'HH:mm:ss')
    Write-KrLog -Level $level -Message "Test $level message from PowerShell at $timestamp"
    Send-KrSignalRLog -Level $level -Message "Test $level message from PowerShell at $timestamp"
    Write-KrTextResponse -InputObject "Broadcasted $level log message from PowerShell" -StatusCode 200
}

<#
.SYNOPSIS
    Broadcast a custom event to all connected SignalR clients.
#>
function SendPowerShellEvent {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/api/ps/event', Tags = 'SignalR')]
    [OpenApiResponse(StatusCode = '200', Description = 'Broadcasted a custom event', Schema = [string], ContentType = 'text/plain')]
    param()

    Send-KrSignalREvent -EventName 'PowerShellEvent' -Data @{
        Message = 'Hello from PowerShell'
        Timestamp = (Get-Date)
        Random = Get-Random -Minimum 1 -Maximum 100
    }
    Write-KrTextResponse -InputObject 'Broadcasted custom event from PowerShell' -StatusCode 200
}

<#
.SYNOPSIS
    Request that clients join a SignalR group.
.PARAMETER groupName
    Group name.
#>
function RequestGroupJoin {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/api/group/join/{groupName}', Tags = 'Groups')]
    [OpenApiResponse(StatusCode = '200', Description = 'Join request sent', Schema = [GroupResult], ContentType = 'application/json')]
    param(
        [OpenApiParameterRef(ReferenceId = 'groupName')]
        [string]$groupName
    )

    Send-KrSignalREvent -EventName 'GroupJoinRequest' -Data @{
        GroupName = $groupName
        Message = "Request to join group: $groupName"
        Timestamp = (Get-Date)
    }

    Write-KrJsonResponse -InputObject ([GroupResult]@{
            success = $true
            message = "Join request sent for group: $groupName"
            groupName = $groupName
        }) -StatusCode 200
}

<#
.SYNOPSIS
    Request that clients leave a SignalR group.
.PARAMETER groupName
    Group name.
#>
function RequestGroupLeave {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/api/group/leave/{groupName}', Tags = 'Groups')]
    [OpenApiResponse(StatusCode = '200', Description = 'Leave request sent', Schema = [GroupResult], ContentType = 'application/json')]
    param(
        [OpenApiParameterRef(ReferenceId = 'groupName')]
        [string]$groupName
    )

    Send-KrSignalREvent -EventName 'GroupLeaveRequest' -Data @{
        GroupName = $groupName
        Message = "Request to leave group: $groupName"
        Timestamp = (Get-Date)
    }

    Write-KrJsonResponse -InputObject ([GroupResult]@{
            success = $true
            message = "Leave request sent for group: $groupName"
            groupName = $groupName
        }) -StatusCode 200
}

<#
.SYNOPSIS
    Broadcast a message to a specific SignalR group.
.PARAMETER groupName
    Group name.
#>
function BroadcastToGroup {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/api/group/broadcast/{groupName}', Tags = 'Groups')]
    [OpenApiResponse(StatusCode = '200', Description = 'Broadcasted to group', Schema = [GroupResult], ContentType = 'application/json')]
    param(
        [OpenApiParameterRef(ReferenceId = 'groupName')]
        [string]$groupName
    )

    Send-KrSignalRGroupMessage -GroupName $groupName -Method 'ReceiveGroupMessage' -Message @{
        Message = "Hello from PowerShell to group: $groupName"
        Timestamp = (Get-Date)
        Sender = 'PowerShell Route'
    }

    Write-KrJsonResponse -InputObject ([GroupResult]@{
            success = $true
            message = "Broadcasted message to group: $groupName"
            groupName = $groupName
        }) -StatusCode 200
}

<#
.SYNOPSIS
    Start a long-running operation and broadcast progress updates via SignalR.
.PARAMETER seconds
    Duration in seconds.
#>
function StartOperation {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/api/operation/start', Tags = 'Operations')]
    [OpenApiResponse(StatusCode = '200', Description = 'Operation started', Schema = [OperationStartResult], ContentType = 'application/json')]
    param(
        [OpenApiParameter(In = 'Query', Deprecated = $true, Description = 'Duration (seconds) for the long-running operation', Example = 10, Minimum = 1)]
        [ValidateRange(1, 3600)]
        [int]$seconds = 20
    )

    if ($seconds -lt 1) { $seconds = 2 }

    Write-KrLog -Level Information -Message 'Starting long operation for {seconds} seconds' -Values $seconds

    $id = New-KrTask -ScriptBlock {
        for ($i = 1; $i -le $seconds; $i++) {
            Start-Sleep -Seconds 1

            $TaskProgress.StatusMessage = "Sleeping ($i/$seconds)"
            $TaskProgress.PercentComplete = ($i * 100 / $seconds)
            Write-KrLog -Level Information -Message 'Operation {TaskId} progress: {i}/{seconds} {PercentComplete}%' -Values $TaskId, $i, $seconds, $TaskProgress.PercentComplete

            $progressMessage = @{
                TaskId = $TaskId
                Progress = $TaskProgress.PercentComplete
                Step = $i
                Message = "Processing step $i of $seconds..."
                Timestamp = (Get-Date)
            }

            try {
                Send-KrSignalREvent -EventName 'OperationProgress' -Data $progressMessage
            } catch {
                Write-Warning "Failed to broadcast progress: $_"
            }
        }

        $completionMessage = @{
            TaskId = $TaskId
            Progress = $TaskProgress.PercentComplete
            Message = $TaskProgress.StatusMessage
            Timestamp = (Get-Date)
        }

        try {
            Send-KrSignalREvent -EventName 'OperationComplete' -Data $completionMessage
        } catch {
            Write-Warning "Failed to broadcast completion: $_"
        }
    } -Arguments @{ seconds = $seconds } -AutoStart

    $message = "Starting long operation with ID: $id"
    Set-KrTaskName -Id $id -Name "LongOperation-$id" -Description $message
    Write-KrLog -Level Information -Message 'Long operation started: {id}' -Values $id

    Write-KrJsonResponse -InputObject ([OperationStartResult]@{
            success = $true
            taskId = "$id"
            message = $message
        }) -StatusCode 200
}

<#
.SYNOPSIS
    Get status for a long-running operation.
.PARAMETER taskId
    Task identifier.
#>
function GetOperationStatus {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/api/operation/status/{taskId}', Tags = 'Operations')]
    [OpenApiResponse(StatusCode = '200', Description = 'Operation status', Schema = [OperationStatusResult], ContentType = 'application/json')]
    [OpenApiResponse(StatusCode = '404', Description = 'Task not found', Schema = [ApiResult], ContentType = 'application/json')]
    param(
        [OpenApiParameterRef(ReferenceId = 'taskId')]
        [string]$taskId
    )

    Write-KrLog -Level Information -Message 'Status requested for operation: {taskId}' -Values $taskId
    $task = Get-KrTask -Id $taskId

    if ($null -eq $task) {
        Write-KrLog -Level Warning -Message 'No task found for operation {taskId}' -Values $taskId
        Write-KrJsonResponse -InputObject ([ApiResult]@{ success = $false; message = "No task found for operation '$taskId'" }) -StatusCode 404
        return
    }

    $progress = $null
    $status = $null
    if ($task.Progress) {
        $progress = $task.Progress.PercentComplete
        $status = $task.Progress.StatusMessage
    }

    Write-KrJsonResponse -InputObject ([OperationStatusResult]@{
            operationId = $taskId
            taskId = $taskId
            state = $task.StateText
            startedAt = $task.StartedAt
            completedAt = $task.CompletedAt
            progress = $progress
            status = $status
            message = 'Operation status retrieved'
        }) -StatusCode 200
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

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute  # Default pattern '/openapi/{version}/openapi.{format}'

Build-KrOpenApiDocument
Test-KrOpenApiDocument

## 8. Start Server

Write-Host '🟢 Kestrun SignalR Demo Server Started' -ForegroundColor Green
Write-Host '📍 Navigate to http://localhost:5000 to see the demo' -ForegroundColor Cyan
Write-Host '🔌 SignalR Hub available at: http://localhost:5000/hubs/kestrun' -ForegroundColor Cyan

Start-KrServer -CloseLogsOnExit

