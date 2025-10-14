<#
.SYNOPSIS
    Starts a previously created task by id.
.DESCRIPTION
    Transitions the task from Created state to Running.
.PARAMETER Server
    The Kestrun server instance.
.PARAMETER Id
    Task id to start.
.PARAMETER WhatIf
    Shows what would happen if the cmdlet runs. The cmdlet is not run.
.PARAMETER Confirm
    Prompts you for confirmation before running the cmdlet.
.EXAMPLE
    Start-KrTask -Id 'task-id'
    Starts the specified task.
.NOTES
    Requires the Kestrun Task service to be added to the server via Add-KrTasksService.
    Returns $true if the task was found and started; $false if the task was not found or could not be started.
    A task can only be started once; subsequent attempts to start a task will return $false.
    Starting a task is asynchronous; the task will run in the background after being started.
    Use Get-KrTask or Get-KrTaskState to monitor the task state.
#>
function Start-KrTask {
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
        if ($PSCmdlet.ShouldProcess("Task $Id", 'Start')) {
            return $Server.Tasks.Start($Id)
        }
    }
}
