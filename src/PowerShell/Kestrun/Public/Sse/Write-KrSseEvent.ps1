<#
    .SYNOPSIS
        Sends a Server-Sent Event (SSE) to connected clients.
    .DESCRIPTION
        This function writes a Server-Sent Event (SSE) to the current HTTP response stream (per-connection).
        For server-wide broadcasting to all connected clients, use Send-KrSseBroadcastEvent.
    .PARAMETER Event
        The name of the event.
    .PARAMETER Data
        The data payload of the event.
    .PARAMETER id
        Optional: The event ID.
    .PARAMETER retryMs
        Optional: The retry interval in milliseconds.
    .EXAMPLE
        Write-KrSseEvent -Event 'message' -Data 'Hello, SSE!'
        Sends an SSE with event name 'message' and data 'Hello, SSE!'.
    .EXAMPLE
        Write-KrSseEvent -Event 'update' -Data '{"status":"ok"}' -id '123' -retryMs 5000
        Sends an SSE with event name 'update', JSON data, event ID '123', and a retry interval of 5000 milliseconds
    .NOTES
        Use Start-KrSseResponse before sending events.
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

    Write-KrOutsideRouteWarning
}
