<#
    .SYNOPSIS
        Gets the number of connected SignalR clients.
    .DESCRIPTION
        This function retrieves the current number of connected SignalR clients from the IConnectionTracker service.
        It can be used to monitor the number of active connections to the SignalR hub.
    .PARAMETER Server
        The Kestrun server instance. If not specified, the default server is used.
    .EXAMPLE
        Get-KrSignalRConnectedClient
        Retrieves the number of connected SignalR clients from the default Kestrun server.
    .EXAMPLE
        Get-KrServer | Get-KrSignalRConnectedClient
        Retrieves the number of connected SignalR clients using the pipeline.
    .NOTES
        This function requires that SignalR has been configured on the server using Add-KrSignalR
#>
function Get-KrSignalRConnectedClient {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server
    )begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        return [Kestrun.Hosting.KestrunHostSignalRExtensions]::GetConnectedClientCount($Server)
    }
}
