<#
    .SYNOPSIS
        Returns a snapshot of the current schedule.
    .DESCRIPTION
        This function retrieves the current schedule snapshot, including all scheduled jobs and their next run times.
    .PARAMETER Server
        The Kestrun host object containing the scheduler.
    .PARAMETER Name
        Optional job name filter. If specified, only jobs matching the name will be returned.
    .PARAMETER TimeZoneId
        Optional Windows / IANA time-zone id to convert timestamps.
        Example: "Pacific Standard Time"  or  "Europe/Berlin"
    .PARAMETER AsHashtable
        If set, returns a hashtable instead of a ScheduleReport object.
    .EXAMPLE
        Get-KrScheduleSnapshot -Server $myServer
        Retrieves the schedule snapshot from the specified Kestrun server.
    .EXAMPLE
        Get-KrScheduleSnapshot -Server $myServer -Name 'Cache'
        Retrieves the schedule snapshot for the job named 'Cache'.
    .EXAMPLE
        Get-KrScheduleSnapshot -Server $myServer -TimeZoneId "Europe/Berlin"
        Retrieves the schedule snapshot with timestamps converted to the specified time zone.
    .EXAMPLE
        Get-KrScheduleSnapshot -Name 'ps-*' -TimeZoneId 'Pacific Standard Time'
        Retrieves all jobs matching the pattern 'ps-*' with timestamps converted to Pacific Standard Time.
    .OUTPUTS
        Returns a ScheduleReport object or a hashtable if AsHashtable is set.
#>
function Get-KrScheduleSnapshot {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Kestrun.Scheduling.JobInfo[]])]
    [OutputType([Hashtable])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [string[]]$Name,
        [string]$TimeZoneId,
        [switch]$AsHashtable
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        if ($null -eq $Server) {
            throw 'Server is not initialized. Please ensure the server is configured before setting options.'
        }
    }
    process {
        $jobs = $Server.Scheduler.GetSnapshot()

        if (-not $AsHashtable) {
            return $jobs          # strongly-typed JobInfo records
        }

        # PowerShell-friendly shape
        return $jobs | ForEach-Object {
            [ordered]@{
                Name = $_.Name
                LastRunAt = $_.LastRunAt
                NextRunAt = $_.NextRunAt
                IsSuspended = $_.IsSuspended
            }
        }

        $tz = if ($TimeZoneId) {
            [TimeZoneInfo]::FindSystemTimeZoneById($TimeZoneId)
        } else { [TimeZoneInfo]::Utc }

        $Server.Scheduler.GetSnapshot($tz, $AsHashtable, $Name)
    }
}

