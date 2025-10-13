<#
.SYNOPSIS
    Requests cancellation for a running task.
.DESCRIPTION
    Signals the Kestrun Task service to cancel the specified task.
.PARAMETER Server
    The Kestrun server instance.
.PARAMETER Id
    Task id to cancel.
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
