<#
    .SYNOPSIS
        Resumes a previously-paused schedule.
    .DESCRIPTION
        This function resumes a scheduled task that was previously paused.
    .PARAMETER Server
        The Kestrun host object that manages the schedule.
    .PARAMETER Name
        The name of the schedule to resume.
    .EXAMPLE
        Resume-KrSchedule -Name 'ps-inline'
        Resumes the schedule named 'ps-inline'.
    .OUTPUTS
        Returns the Kestrun host object after resuming the schedule.
    .NOTES
        This function is part of the Kestrun scheduling module.
#>
function Resume-KrSchedule {
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
        if ($null -eq $Server) {
            throw 'Server is not initialized. Please ensure the server is configured before setting options.'
        }
    }
    process {
        if (-not $Server.Scheduler) {
            throw 'SchedulerService is not enabled.'
        }

        if ($Server.Scheduler.Resume($Name)) {
            Write-Information "▶️ schedule '$Name' resumed."
        } else {
            Write-Warning "No schedule named '$Name' found."
        }
        return $Server
    }
}

