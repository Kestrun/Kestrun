<#
.SYNOPSIS
    Sets the name and/or description of a task.
.DESCRIPTION
    This function sets the human-friendly name and/or description of a task identified by its id.
.PARAMETER Server
    The Kestrun server instance.
.PARAMETER Id
    The id of the task to update. This parameter is mandatory.
.PARAMETER Name
    The new name for the task. This parameter is optional but at least one of Name or Description must be provided.
.PARAMETER Description
    The new description for the task. This parameter is optional but at least one of Name or Description must be provided.
.EXAMPLE
    Set-KrTaskName -Id 'task-id' -Name 'My Task'
    This command sets the name of the specified task to 'My Task'.
.EXAMPLE
    Set-KrTaskName -Id 'task-id' -Description 'This is a sample task.'
    This command sets the description of the specified task.
.EXAMPLE
    Set-KrTaskName -Id 'task-id' -Name 'My Task' -Description 'This is a sample task.'
    This command sets both the name and description of the specified task.
.NOTES
    At least one of the Name or Description parameters must be provided and non-empty.
    If the specified task id does not exist, an error will be thrown.
#>
function Set-KrTaskName {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(mandatory = $true)]
        [string]$Id,

        [Parameter(Mandatory = $false)]
        [string]$Name,

        [parameter(Mandatory = $false)]
        [string]$Description
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ([string]::IsNullOrWhiteSpace($Name) -and [string]::IsNullOrWhiteSpace($Description)) {
            throw [System.ArgumentException] 'Either Name or Description must be provided and non-empty.'
        }
        if ($PSBoundParameters.ContainsKey('Name')) {
            $Server.Tasks.SetTaskName($Id, $Name)
        }
        if ($PSBoundParameters.ContainsKey('Description')) {
            $Server.Tasks.SetTaskDescription($Id, $Description)
        }
    }
}
