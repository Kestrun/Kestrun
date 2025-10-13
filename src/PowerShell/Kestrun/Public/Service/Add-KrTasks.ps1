<#
    .SYNOPSIS
        Adds ad-hoc Tasks support to the Kestrun server.
    .DESCRIPTION
        Registers the Kestrun Task service on the server, enabling one-off script execution (PowerShell, C#, VB.NET)
        with status/result and cancellation support.
    .PARAMETER Server
        The Kestrun server instance.
    .PARAMETER MaxRunspaces
        Optional maximum PowerShell runspaces for task execution; falls back to scheduler sizing when omitted.
    .PARAMETER PassThru
        Returns the server when specified.
#>
function Add-KrTasks {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [int]$MaxRunspaces,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($PSBoundParameters.ContainsKey('MaxRunspaces')) {
            $Server.AddTasks($MaxRunspaces) | Out-Null
        } else {
            $Server.AddTasks() | Out-Null
        }

        if ($PassThru) { return $Server }
    }
}
