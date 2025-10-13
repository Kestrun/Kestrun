<#
.SYNOPSIS
    Starts a previously created task by id.
.DESCRIPTION
    Transitions the task from Created state to Running.
.PARAMETER Server
    The Kestrun server instance.
.PARAMETER Id
    Task id to start.
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
