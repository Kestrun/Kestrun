<#
    .SYNOPSIS
        Broadcasts a log message to all connected SignalR clients.
    .DESCRIPTION
        This function sends a log message to all connected SignalR clients via the IRealtimeBroadcaster service.
        The message is broadcast in real-time to all connected clients listening on the hub.
    .PARAMETER Level
        The log level (e.g., Information, Warning, Error, Debug, Verbose).
    .PARAMETER Message
        The log message to broadcast.
    .PARAMETER Server
        The Kestrun server instance. If not specified, the default server is used.
    .EXAMPLE
        Send-KrLog -Level Information -Message "Server started successfully"
        Broadcasts an information log message to all connected SignalR clients.
    .EXAMPLE
        Send-KrLog -Level Error -Message "Failed to process request"
        Broadcasts an error log message to all connected SignalR clients.
    .EXAMPLE
        Get-KrServer | Send-KrLog -Level Warning -Message "High memory usage detected"
        Broadcasts a warning log message using the pipeline.
    .NOTES
        This function requires that SignalR has been configured on the server using Add-KrSignalRHubMiddleware.
        The IRealtimeBroadcaster service must be registered for this cmdlet to work.
#>
function Send-KrLog {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal')]
        [string]$Level,

        [Parameter(Mandatory = $true)]
        [string]$Message,

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

            # Broadcast the log message asynchronously
            $task = $broadcaster.BroadcastLogAsync($Level, $Message, [System.Threading.CancellationToken]::None)
            $task.GetAwaiter().GetResult()

            Write-KrLog -Level Debug -Message "Broadcasted log message: $Level - $Message"
        }
        catch {
            Write-KrLog -Level Error -Message "Failed to broadcast log message: $_" -Exception $_.Exception
        }
    }
}
