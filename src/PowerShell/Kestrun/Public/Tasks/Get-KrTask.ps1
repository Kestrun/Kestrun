<#
.SYNOPSIS
    Gets the status or result of a task by id.
.DESCRIPTION
    Without -Detailed returns task state; with -Detailed returns TaskResult snapshot.
.PARAMETER Server
    The Kestrun server instance.
.PARAMETER Id
    Task id to query.
.PARAMETER State
    When present, return only the task state string.
.EXAMPLE
    Get-KrTask -Id 'task-id'

    Returns the current state of the specified task.
.EXAMPLE
    Get-KrTask -Id 'task-id' -Detailed
    Returns the detailed result of the specified task.
.EXAMPLE
    Get-KrTask

    Returns a list of all tasks with their current states.
.OUTPUTS
    When -Detailed is specified, returns a [Kestrun.Tasks.TaskResult] object; otherwise returns a string with the task state.
    When Id is not specified, returns an array of [Kestrun.Tasks.TaskResult] objects for all tasks.
#>
function Get-KrTask {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(defaultParameterSetName = 'Default')]
    [OutputType([Kestrun.Tasks.KrTask[]])]
    [OutputType([Kestrun.Tasks.KrTask])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string]$Id,

        [Parameter(parameterSetName = 'State')]
        [switch]$State
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ([string]::IsNullOrEmpty($Id)) {
            return $Server.Tasks.List()
        }
        if ($State.IsPresent) {
            return $Server.Tasks.GetState($Id)
        } else {
            return $Server.Tasks.Get($Id)
        }
    }
}
