<#
    .SYNOPSIS
        Sends a Server-Sent Event (SSE) to connected clients.
    .DESCRIPTION
        This function sends a Server-Sent Event (SSE) to connected clients via the ISseBroadcaster service.
        The event is sent in real-time to all connected clients listening for SSEs.
    .PARAMETER Event
        The name of the event.
    .PARAMETER Data
        The data payload of the event.
    .PARAMETER id
        Optional: The event ID.
    .PARAMETER retryMs
        Optional: The retry interval in milliseconds.
    .EXAMPLE
        Write-KrSseEvent -Level Information -Event "StatusUpdate" -Data "System is operational"
        Sends an SSE with the event name "StatusUpdate" and data "System is operational" at Information level.
    .NOTES
        This function requires that SSE has been configured on the server using Add-KrSseMiddleware.
        The ISseBroadcaster service must be registered for this cmdlet to work.
#>
function Write-KrSseEvent {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Event,
        [Parameter(Mandatory = $true)]
        [string] $Data,
        [Parameter(Mandatory = $false)]
        [string] $id,
        [Parameter(Mandatory = $false)]
        [int] $retryMs
    )

    # Only works inside a route script block where $Context is available
    if ($null -ne $Context) {
        $Context.WriteSseEvent( $Event, $Data, $id, $retryMs)
        Write-KrLog -Level Debug -Message "Sse event sent: $Event - $Data"
        return
    }
    # Only works inside a route script block where $Context is available
    Write-KrOutsideRouteWarning
}
