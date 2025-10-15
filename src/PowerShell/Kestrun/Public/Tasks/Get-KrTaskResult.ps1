<#
.SYNOPSIS
    Gets the detailed result of a task by id.
.DESCRIPTION
    Returns a TaskResult snapshot for the specified task id.
.PARAMETER Server
    The Kestrun server instance.
.PARAMETER Id
    Task id to query.
.EXAMPLE
    Get-KrTaskResult -Id 'task-id'
    Returns the detailed result of the specified task.
.OUTPUTS
    Returns a [object].
#>
function Get-KrTaskResult {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(defaultParameterSetName = 'Default')]
    [OutputType([object])]
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
        return $Server.Tasks.GetResult($Id)
    }
}
