<#
    Scheduling Report Example
    Demonstrates exposing a report endpoint with optional timezone.
    FileName: 12.3-Scheduling-Report.ps1
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

## 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

## 2. Server
New-KrServer -Name 'Scheduling Report Demo'

## 3. Listener
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

## 4. Scheduler
Add-KrScheduling

## 5. Apply configuration
Enable-KrConfiguration

## 6. Register a couple of jobs so the report has content
Register-KrSchedule -Name 'rep-ps' -Interval '00:00:05' -RunImmediately -ScriptBlock {
    Write-KrLog -Level Debug -Message 'rep-ps ran at {0:O}' -Values $([DateTimeOffset]::UtcNow)
}
Register-KrSchedule -Name 'rep-cs' -Interval '00:00:07' -Language CSharp -RunImmediately -Code 'Serilog.Log.Debug("rep-cs ran at {0:O}", DateTimeOffset.UtcNow);'

## 7. Report route (supports ?tz=<TimeZoneId>)
Add-KrMapRoute -Verbs Get -Pattern '/schedule/report' -ScriptBlock {
    $tz = Get-KrRequestQuery -Name 'tz'
    Write-KrLog -Level Information -Message "Requested timezone: $tz"

    if ($tz) {
        try {
            [void][TimeZoneInfo]::FindSystemTimeZoneById($tz)
            $report = Get-KrScheduleReport -TimeZoneId $tz
        } catch {
            Write-KrLog -Level Warning -Message "Invalid timezone ID '$tz' provided. Falling back to UTC."
            $report = Get-KrScheduleReport
        }
    } else {
        $report = Get-KrScheduleReport
    }

    Write-KrJsonResponse -InputObject $report -StatusCode 200
}

## 8. Start
Start-KrServer -CloseLogsOnExit
