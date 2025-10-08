<#
    Scheduling Cron Example
    Demonstrates CRON-based jobs (seconds precision) with PowerShell and C#.
    FileName: 12.2-Scheduling-Cron.ps1
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

## 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

## 2. Server
New-KrServer -Name 'Scheduling Cron Demo'

## 3. Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

## 4. Runtime + Scheduler
Add-KrScheduling -MaxRunspaces 2

## 5. Apply configuration
Enable-KrConfiguration

## 6. CRON jobs

# (A) PowerShell job every 10 seconds, run once immediately
Register-KrSchedule -Name 'cron-ps' -Cron '*/10 * * * * *' -RunImmediately -ScriptBlock {
    Write-KrLog -Level Information -Message '⏰ cron-ps fired at {0:O}' -Values $([DateTimeOffset]::UtcNow)
}

# (B) C# job every 15 seconds, run once immediately
Register-KrSchedule -Name 'cron-cs' -Cron '*/15 * * * * *' -Language CSharp -RunImmediately -Code 'Serilog.Log.Information("⏰ cron-cs fired at {0:O}", DateTimeOffset.UtcNow);'

## 7. Routes
Add-KrMapRoute -Verbs Get -Pattern '/schedule/report' -ScriptBlock {
    $report = Get-KrScheduleReport
    Write-KrJsonResponse -InputObject $report -StatusCode 200
}

## 8. Start
Start-KrServer -CloseLogsOnExit

