<#
.SYNOPSIS
    Gets the state of a task by id.
.DESCRIPTION
    Returns the task state string for the specified task id.
.PARAMETER Server
    The Kestrun server instance.
.PARAMETER Id
    Task id to query.
.EXAMPLE
    Get-KrTaskState -Id 'task-id'
    Returns the state of the specified task as a [Kestrun.Tasks.TaskState] object.
.OUTPUTS
    [Kestrun.Tasks.TaskState]
#>
function Get-KrTaskState {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(defaultParameterSetName = 'Default')]
    [OutputType([Kestrun.Tasks.TaskState])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(mandatory = $true)]
        [string]$Id
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        return $Server.Tasks.GetState($Id)
    }
}
