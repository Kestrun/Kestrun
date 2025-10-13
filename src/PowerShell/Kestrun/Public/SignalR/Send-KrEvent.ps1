<#
    .SYNOPSIS
        Broadcasts a custom event to all connected SignalR clients.
    .DESCRIPTION
        This function sends a custom event with optional data to all connected SignalR clients via the IRealtimeBroadcaster service.
        The event is broadcast in real-time to all connected clients listening on the hub.
    .PARAMETER EventName
        The name of the event to broadcast.
    .PARAMETER Data
        Optional data to include with the event. Can be any object that will be serialized to JSON.
    .PARAMETER Server
        The Kestrun server instance. If not specified, the default server is used.
    .EXAMPLE
        Send-KrEvent -EventName "UserLoggedIn" -Data @{ Username = "admin"; Timestamp = (Get-Date) }
        Broadcasts a custom event with data to all connected SignalR clients.
    .EXAMPLE
        Send-KrEvent -EventName "ServerHealthCheck" -Data @{ Status = "Healthy"; Uptime = 3600 }
        Broadcasts a health check event with status information.
    .EXAMPLE
        Get-KrServer | Send-KrEvent -EventName "TaskCompleted" -Data @{ TaskId = 123; Success = $true }
        Broadcasts a task completion event using the pipeline.
    .NOTES
        This function requires that SignalR has been configured on the server using Add-KrSignalRHubMiddleware.
        The IRealtimeBroadcaster service must be registered for this cmdlet to work.
#>
function Send-KrEvent {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$EventName,

        [Parameter()]
        [object]$Data
    )

      # Only works inside a route script block where $Context is available
    if ($null -ne $Context) {
        # Call the C# method on the $Context object
        if ( $Context.BroadcastLog( $EventName, $Data, [System.Threading.CancellationToken]::None)) {
            Write-KrLog -Level Debug -Message "Broadcasted log message: $EventName - $Data"
            return
        } else {
            Write-KrLog -Level Error -Message 'Failed to broadcast log message: Unknown error'
            return
        }
    } else {
        Write-KrOutsideRouteWarning
    }
}
