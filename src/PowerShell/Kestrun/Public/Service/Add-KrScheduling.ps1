<#
    .SYNOPSIS
        Adds scheduling support to the Kestrun server.
    .DESCRIPTION
        This cmdlet allows you to register a scheduling service with the Kestrun server.
        It can be used to manage scheduled tasks and jobs in the context of the Kestrun server.
    .PARAMETER Server
        The Kestrun server instance to which the scheduling service will be added.
    .PARAMETER MaxRunspaces
        The maximum number of runspaces to use for scheduling tasks. If not specified, defaults to 0 (unlimited).
    .PARAMETER PassThru
        If specified, the cmdlet will return the modified server instance after adding the scheduling service.
    .EXAMPLE
        $server | Add-KrScheduling -MaxRunspaces 5
        This example adds scheduling support to the server, with a maximum of 5 runspaces.
    .EXAMPLE
        $server | Add-KrScheduling
        This example adds scheduling support to the server without specifying a maximum number of runspaces.
    .NOTES
        This cmdlet is used to register a scheduling service with the Kestrun server, allowing you to manage scheduled tasks and jobs.
#>
function Add-KrScheduling {
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
    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        if ($MaxRunspaces -eq 0) {
            # If MaxRunspaces is 0, use the default configuration
            $Server.AddScheduling() | Out-Null
        } else {
            $Server.AddScheduling($MaxRunspaces) | Out-Null
        }

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}

