<#
    .SYNOPSIS
        Returns the full schedule report.
    .DESCRIPTION
        This function retrieves the current schedule report, including all scheduled jobs and their next run times.
    .PARAMETER Server
        The Kestrun host object containing the scheduler.
    .PARAMETER TimeZoneId
        Optional Windows / IANA time-zone id to convert timestamps.
        Example: "Pacific Standard Time"  or  "Europe/Berlin"
    .PARAMETER AsHashtable
        If set, returns a hashtable instead of a ScheduleReport object.
    .EXAMPLE
        Get-KrScheduleReport -Server $myServer
        Retrieves the schedule report from the specified Kestrun server.
    .EXAMPLE
        Get-KrScheduleReport -Server $myServer -TimeZoneId "Europe/Berlin"
        Retrieves the schedule report with timestamps converted to the specified time zone.
    .OUTPUTS
        Returns a ScheduleReport object or a hashtable if AsHashtable is set.
#>
function Get-KrScheduleReport {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Kestrun.Scheduling.ScheduleReport])]
    [OutputType([Hashtable])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [string]$TimeZoneId,
        [switch]$AsHashtable
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if (-not $Server.Scheduler) {
            throw 'SchedulerService is not enabled.'
        }

        $tz = if ($TimeZoneId) {
            [TimeZoneInfo]::FindSystemTimeZoneById($TimeZoneId)
        } else { [TimeZoneInfo]::Utc }

        if ($AsHashtable) {
            return $Server.Scheduler.GetReportHashtable($tz)
        } else {
            return $Server.Scheduler.GetReport($tz)
        }
    }
}

