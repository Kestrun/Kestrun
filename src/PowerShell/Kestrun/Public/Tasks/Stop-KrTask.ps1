<#
.SYNOPSIS
    Requests cancellation for a running task.
.DESCRIPTION
    Signals the Kestrun Task service to cancel the specified task.
.PARAMETER Server
    The Kestrun server instance.
.PARAMETER Id
    Task id to cancel.
.PARAMETER WhatIf
    Shows what would happen if the cmdlet runs. The cmdlet is not run.
.PARAMETER Confirm
    Prompts you for confirmation before running the cmdlet.
.EXAMPLE
    Stop-KrTask -Id 'task-id'
    Requests cancellation for the specified task.
.NOTES
    Requires the Kestrun Task service to be added to the server via Add-KrTasksService.
    Cancellation is cooperative; the task script must periodically check for cancellation and stop itself.
    Returns $true if the task was found and cancellation was requested; $false if the task was not found or could not be cancelled.
    If the task is already completed, cancellation will not be requested and $false will be returned.
    Cancellation may not be immediate; the task may take some time to stop after cancellation is requested.
    If the task does not support cancellation, it will continue to run until completion.
    This cmdlet supports ShouldProcess for confirmation prompts.
#>
function Stop-KrTask {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory)]
        [string]$Id
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($PSCmdlet.ShouldProcess("Task $Id", 'Cancel')) {
            return $Server.Tasks.Cancel($Id)
        }
    }
}
