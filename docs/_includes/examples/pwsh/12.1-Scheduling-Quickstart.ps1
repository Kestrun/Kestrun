<#
    Scheduling Quickstart Script
    Demonstrates enabling the SchedulerService and registering jobs
    with both PowerShell and C# code. Also exposes a small route to
    inspect the live schedule report and a counter route.
    FileName: 12.1-Scheduling-Quickstart.ps1
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

## 1. Logging
New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault

## 2. Server
New-KrServer -Name 'Scheduling Demo'

## 3. Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

## 4. Runtime + Scheduler

Add-KrScheduling -MaxRunspaces 4

## 5. Shared state (for demo route)
Set-KrSharedState -Name 'Visits' -Value @{ Count = 0 }

## 6. Apply configuration
Enable-KrConfiguration

## 7. Register scheduled jobs

# (A) PowerShell heartbeat every 10s, run immediately once
Register-KrSchedule -Name 'heartbeat-ps' -Interval '00:00:10' -RunImmediately -ScriptBlock {
    Write-KrLog -Level Information -Message '💓 Heartbeat (PS) at {0:O}' -Values $([DateTimeOffset]::UtcNow)
}

# (B) C# heartbeat every 15s
Register-KrSchedule -Name 'heartbeat-cs' -Interval '00:00:15' -Language CSharp -Code 'Serilog.Log.Information("💓 Heartbeat (C#) at {0:O}", DateTimeOffset.UtcNow);'

## 8. Routes

# /visit increments a shared counter
Add-KrMapRoute -Verbs Get -Pattern '/visit' -ScriptBlock {
    $Visits['Count']++
    Write-KrTextResponse -InputObject ('Visits now: {0}' -f $Visits['Count']) -StatusCode 200
}

# /schedule/report returns the aggregated report
Add-KrMapRoute -Verbs Get -Pattern '/schedule/report' -ScriptBlock {
    $report = Get-KrScheduleReport
    Write-KrJsonResponse -InputObject $report -StatusCode 200
}

## 9. Start server
Start-KrServer -CloseLogsOnExit
