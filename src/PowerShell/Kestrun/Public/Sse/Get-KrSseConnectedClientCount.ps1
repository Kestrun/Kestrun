<#
    .SYNOPSIS
        Gets the number of connected SSE broadcast clients.
    .DESCRIPTION
        Returns the connected client count for the server-wide ISseBroadcaster service.
    .PARAMETER Server
        The Kestrun server instance. If not specified, the default server is used.
    .EXAMPLE
        Get-KrSseConnectedClientCount
#>
function Get-KrSseConnectedClientCount {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server
    )

    process {
        if (-not $Server) {
            $Server = Resolve-KestrunServer -Server $Server
        }

        return $Server.GetSseConnectedClientCount()
    }
}
