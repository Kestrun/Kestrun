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
        [object]$Data,

        [Parameter(ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server
    )

    begin {
        # Resolve the server instance
        if (-not $Server) {
            $Server = Get-KrServer
        }
    }

    process {
        try {
            # Get the IRealtimeBroadcaster service from the server
            $broadcaster = $Server.App.Services.GetService([Kestrun.SignalR.IRealtimeBroadcaster])

            if (-not $broadcaster) {
                Write-KrLog -Level Warning -Message 'IRealtimeBroadcaster service is not registered. Make sure SignalR is configured with KestrunHub.'
                return
            }

            # Broadcast the event asynchronously
            $task = $broadcaster.BroadcastEventAsync($EventName, $Data, [System.Threading.CancellationToken]::None)
            $task.GetAwaiter().GetResult()

            Write-KrLog -Level Debug -Message "Broadcasted event: $EventName"
        }
        catch {
            Write-KrLog -Level Error -Message "Failed to broadcast event: $_" -Exception $_.Exception
        }
    }
}
