<#
.SYNOPSIS
    Removes a finished task from the registry.
.DESCRIPTION
    Deletes a task after it has completed/faulted/cancelled; does not cancel.
.PARAMETER Server
    The Kestrun server instance.
.PARAMETER Id
    Task id to remove.
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
