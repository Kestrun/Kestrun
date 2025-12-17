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
        Send-KrSignalREvent -EventName "UserLoggedIn" -Data @{ Username = "admin"; Timestamp = (Get-Date) }
        Broadcasts a custom event with data to all connected SignalR clients.
    .EXAMPLE
        Send-KrSignalREvent -EventName "ServerHealthCheck" -Data @{ Status = "Healthy"; Uptime = 3600 }
        Broadcasts a health check event with status information.
    .EXAMPLE
        Get-KrServer | Send-KrSignalREvent -EventName "TaskCompleted" -Data @{ TaskId = 123; Success = $true }
        Broadcasts a task completion event using the pipeline.
    .NOTES
        This function requires that SignalR has been configured on the server using Add-KrSignalRHubMiddleware.
        The IRealtimeBroadcaster service must be registered for this cmdlet to work.
#>
function Send-KrSignalREvent {
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

    process {
        try {
            if ($null -ne $Context) {
                # Prefer the centralized KestrunContext implementation when inside a route
                if ($Context.BroadcastEvent($EventName, $Data, [System.Threading.CancellationToken]::None)) {
                    return
                } else {
                    Write-KrLog -Level Error -Message 'Failed to broadcast event: Unknown error'
                    return
                }
            }

            # Fallback: allow explicit host usage outside a route (no HttpContext)
            if (-not $Server) {
                try { $Server = Get-KrServer } catch { $Server = $null }
            }
            if ($null -ne $Server) {
                $task = [Kestrun.Hosting.KestrunHostSignalRExtensions]::BroadcastEventAsync($Server, $EventName, $Data, $null, [System.Threading.CancellationToken]::None)
                $null = $task.GetAwaiter().GetResult()
            } else {
                Write-KrOutsideRouteWarning
            }
        } catch {
            Write-KrLog -Level Error -Message 'Failed to broadcast event: {error}' -Values $_.Exception.Message -Exception $_.Exception
        }
    }
}
