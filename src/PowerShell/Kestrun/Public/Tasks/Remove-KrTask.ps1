<#
.SYNOPSIS
    Removes a finished task from the registry.
.DESCRIPTION
    Deletes a task after it has completed/faulted/cancelled; does not cancel.
.PARAMETER Server
    The Kestrun server instance.
.PARAMETER Id
    Task id to remove.
.PARAMETER WhatIf
    Shows what would happen if the cmdlet runs. The cmdlet is not run.
.PARAMETER Confirm
    Prompts you for confirmation before running the cmdlet.
.EXAMPLE
    Remove-KrTask -Id 'task-id'
    Removes the specified task.
.NOTES
    Requires the Kestrun Task service to be added to the server via Add-KrTasksService.
    A task can only be removed if it is in a final state (Completed, Failed, Stopped).
    Returns $true if the task was found and removed; $false if the task was not found or could not be removed.
    This cmdlet supports ShouldProcess for confirmation prompts.
#>
function Remove-KrTask {
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
        if ($PSCmdlet.ShouldProcess("Task $Id", 'Remove')) {
            return $Server.Tasks.Remove($Id)
        }
    }
}
