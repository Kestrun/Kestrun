<#
.SYNOPSIS
    Gets the current Kestrun server instance.
.DESCRIPTION
    This function retrieves the current Kestrun server instance. If a server instance is not provided,
    it attempts to resolve the server from the current context.
.PARAMETER Server
    The Kestrun server instance to retrieve. If not specified, the function will attempt to resolve the current server context.
.PARAMETER StartTime
    If specified, returns the server's start time as a DateTime object.
.PARAMETER StopTime
    If specified, returns the server's stop time as a DateTime object.
.PARAMETER Uptime
    If specified, returns the server's uptime as a TimeSpan object.
.EXAMPLE
    Get-KrServer
    This command retrieves the current Kestrun server instance.
.EXAMPLE
    Get-KrServer -StartTime
    This command retrieves the start time of the current Kestrun server instance.
.EXAMPLE
    Get-KrServer -StopTime
    This command retrieves the stop time of the current Kestrun server instance.
.EXAMPLE
    Get-KrServer -Uptime
    This command retrieves the uptime of the current Kestrun server instance.
.OUTPUTS
    [Kestrun.Hosting.KestrunHost]
        The current Kestrun server instance.
    [DateTime]
        The server's start time or stop time if the corresponding switch is used.
    [TimeSpan]
        The server's uptime if the Uptime switch is used.
.NOTES
    This function is part of the Kestrun PowerShell module and is used to manage Kestrun server instances.
    If the server instance is not found in the context, it attempts to resolve it using the Resolve-KestrunServer function.
#>
function Get-KrServer {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'Default')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    [OutputType([DateTime])]
    [OutputType([TimeSpan])]
    param(
        [Parameter(ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter(ParameterSetName = 'StartTime')]
        [switch]$StartTime,
        [Parameter(ParameterSetName = 'StopTime')]
        [switch]$StopTime,
        [Parameter(ParameterSetName = 'Uptime')]
        [switch]$Uptime
    )
    process {
        if ($null -eq $Context -or $null -eq $Context.Response) {
            $Server = Resolve-KestrunServer -Server $Server
        } else {
            $Server = $Context.Host
        }

        if ($StartTime.IsPresent) {
            return $Server.StartTime
        } elseif ($StopTime.IsPresent) {
            return $Server.StopTime
        } elseif ($Uptime.IsPresent) {
            return $Server.Uptime
        } else {
            return $Server
        }
    }
}
