<#
    .SYNOPSIS
        Suspends a schedule, preventing it from running until resumed.
    .DESCRIPTION
        This function pauses a scheduled task, allowing it to be resumed later.
    .PARAMETER Server
        The Kestrun host object that manages the schedule.
    .PARAMETER Name
        The name of the schedule to suspend.
    .EXAMPLE
        Suspend-KrSchedule -Name 'ps-inline'
        Suspends the schedule named 'ps-inline'.
    .OUTPUTS
        Returns the Kestrun host object after suspending the schedule.
    .NOTES
        This function is part of the Kestrun scheduling module.
#>
function Suspend-KrSchedule {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if (-not $Server.Scheduler) {
            throw 'SchedulerService is not enabled.'
        }

        if ($Server.Scheduler.Pause($Name)) {
            Write-Information "🛑 schedule '$Name' is now paused."
        } else {
            Write-Warning "No schedule named '$Name' found."
        }
        return $Server
    }
}

