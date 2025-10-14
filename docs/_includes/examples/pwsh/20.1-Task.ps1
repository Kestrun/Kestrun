<#
    Sample Kestrun Server Configuration – Tasks Demo
    This script shows how to enable the Tasks feature and use it via HTTP routes.
    FileName: 20.1-Task.ps1
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Configure default logging
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'myLogger' -SetAsDefault

# Create a new Kestrun server
New-KrServer -Name 'Tasks Demo Server'

# Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress -SelfSignedCert

# --- Tasks setup ------------------------------------------------------------
# Register the ad-hoc Tasks feature (PowerShell, C#, VB.NET)
Add-KrTasks

# Enable configuration
Enable-KrConfiguration

# --- Routes -----------------------------------------------------------------

# 1) Create a PowerShell task (does NOT start it). Example:
#    /tasks/create/ps?seconds=2
Add-KrMapRoute -Verbs Get -Path '/tasks/create/ps' -ScriptBlock {
    $seconds = Get-KrRequestQuery -Name 'seconds' -AsInt
    if ($seconds -le 0) { $seconds = 2 }
    Write-KrLog -Level Debug -Message 'Creating PS task that sleeps for {seconds} seconds' -Values $seconds

    # Create a new PowerShell task that sleeps for the specified seconds and then returns the current
    $id = New-KrTask -ScriptBlock {
        write-debug "Task started, will sleep for $sec seconds"
        Start-Sleep -Seconds $sec;
        'PS task done at ' + (Get-Date).ToString('o')
    } -Arguments @{ "sec" = $seconds }
    Write-KrJsonResponse -StatusCode 200 -InputObject @{ id = $id; language = 'PowerShell' }
}

# 2) Create a C# task (does NOT start it). Example:
#    /tasks/create/cs?ms=1500
Add-KrMapRoute -Verbs Get -Path '/tasks/create/cs' -ScriptBlock {
    $ms = [int](Get-KrRequestQuery -Name 'ms')
    if ($ms -le 0) { $ms = 1500 }

    $code = @'
await Task.Delay({0});
"CS task done at " + DateTime.UtcNow.ToString("o")
'@ -f $ms

    $id = New-KrTask -Language CSharp -Code $code
    Write-KrJsonResponse -StatusCode 200 -InputObject @{ id = $id; language = 'CSharp' }
}

# 3) Start a previously created task. Example:
#    /tasks/start?id=<taskId>
Add-KrMapRoute -Verbs Get -Path '/tasks/start' -ScriptBlock {
    $id = Get-KrRequestQuery -Name 'id'
    if ([string]::IsNullOrWhiteSpace($id)) {
        Write-KrJsonResponse -StatusCode 400 -InputObject @{ error = "Missing 'id' query parameter" }
        return
    }
    $ok = Start-KrTask -Id $id
    $status = if ($ok) { 202 } else { 409 }
    Write-KrJsonResponse -StatusCode $status -InputObject @{ started = $ok; id = $id }
}

# 4) Get task state. Example:
#    /tasks/state?id=<taskId>
Add-KrMapRoute -Verbs Get -Path '/tasks/state' -ScriptBlock {
    $id = Get-KrRequestQuery -Name 'id'
    if ([string]::IsNullOrWhiteSpace($id)) {
        Write-KrJsonResponse -StatusCode 400 -InputObject @{ error = "Missing 'id' query parameter" }
        return
    }
    $task = Get-KrTask -Id $id
    if ($null -eq $task) {
        Write-KrJsonResponse -StatusCode 404 -InputObject @{ error = 'Task not found' }
        return
    }
    Write-KrJsonResponse -StatusCode 200 -InputObject $task
}

# 5) Get detailed result snapshot. Example:
#    /tasks/result?id=<taskId>
Add-KrMapRoute -Verbs Get -Path '/tasks/result' -ScriptBlock {
    $id = Get-KrRequestQuery -Name 'id'
    if ([string]::IsNullOrWhiteSpace($id)) {
        Write-KrJsonResponse -StatusCode 400 -InputObject @{ error = "Missing 'id' query parameter" }
        return
    }
    $state = Get-KrTask -Id $id -State
    if( $null -eq $state) {
        Write-KrJsonResponse -StatusCode 404 -InputObject @{ error = 'Task not found' }
        return
    }
    if ($state -ne 'Completed' -and $state -ne 'Cancelled' -and $state -ne 'Faulted') {
        Write-KrJsonResponse -StatusCode 409 -InputObject @{ error = 'Task is not in a completed state' }
        return
    }
    $r = Get-KrTask -Id $id -Result

    Write-KrJsonResponse -StatusCode 200 -InputObject $r
}

# 6) Cancel a task. Example:
#    /tasks/cancel?id=<taskId>
Add-KrMapRoute -Verbs Get -Path '/tasks/cancel' -ScriptBlock {
    $id = Get-KrRequestQuery -Name 'id'
    if ([string]::IsNullOrWhiteSpace($id)) {
        Write-KrJsonResponse -StatusCode 400 -InputObject @{ error = "Missing 'id' query parameter" }
        return
    }
    $ok = Stop-KrTask -Id $id
    $status = if ($ok) { 202 } else { 409 }
    Write-KrJsonResponse -StatusCode $status -InputObject @{ cancelled = $ok; id = $id }
}

# 7) Remove a finished task. Example:
#    /tasks/remove?id=<taskId>
Add-KrMapRoute -Verbs Get -Path '/tasks/remove' -ScriptBlock {
    $id = Get-KrRequestQuery -Name 'id'
    if ([string]::IsNullOrWhiteSpace($id)) {
        Write-KrJsonResponse -StatusCode 400 -InputObject @{ error = "Missing 'id' query parameter" }
        return
    }
    $ok = Remove-KrTask -Id $id
    $status = if ($ok) { 200 } else { 404 }
    Write-KrJsonResponse -StatusCode $status -InputObject @{ removed = $ok; id = $id }
}

# 8) List all tasks.
#    /tasks/list
Add-KrMapRoute -Verbs Get -Path '/tasks/list' -ScriptBlock {
    $list = Get-KrTask
    Write-KrJsonResponse -StatusCode 200 -InputObject $list
}

# 9) Convenience: One-shot create+start PS task.
#    /tasks/run/ps?seconds=1
Add-KrMapRoute -Verbs Get -Path '/tasks/run/ps' -ScriptBlock {
    $seconds = [int](Get-KrRequestQuery -Name 'seconds')
    if ($seconds -le 0) { $seconds = 1 }
    $code = "Start-Sleep -Seconds $seconds; Get-Date"
    # One-shot = create + start
    $id = New-KrTask -Language PowerShell -Code $code
    $null = Start-KrTask -Id $id
    Write-KrJsonResponse -StatusCode 202 -InputObject @{ id = $id; started = $true }
}

# Convenience: hello
Add-KrMapRoute -Verbs Get -Path '/hello' -ScriptBlock {
    Write-KrTextResponse -StatusCode 200 -InputObject 'Hello, Tasks World!'
}

# Start the server asynchronously
Start-KrServer -CloseLogsOnExit
