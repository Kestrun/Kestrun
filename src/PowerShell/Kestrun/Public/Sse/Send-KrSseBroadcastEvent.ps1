<#
    .SYNOPSIS
        Broadcasts an SSE event to all connected SSE broadcast clients.
    .DESCRIPTION
        Sends an SSE event via the server-wide ISseBroadcaster service (configured by Add-KrSseBroadcastMiddleware).
        This works both inside a route and outside a route (tasks, scripts) by using the server's service provider.
    .PARAMETER Event
        The name of the event.
    .PARAMETER Data
        The data payload of the event.
    .PARAMETER Id
        Optional: The event ID.
    .PARAMETER RetryMs
        Optional: The retry interval in milliseconds.
    .PARAMETER Server
        The Kestrun server instance. If not specified, the default server is used.
    .EXAMPLE
        Send-KrSseBroadcastEvent -Event 'tick' -Data '{"i":1}'
    .EXAMPLE
        Get-KrServer | Send-KrSseBroadcastEvent -Event 'status' -Data 'OK'
    .NOTES
        Requires Add-KrSseBroadcastMiddleware to be configured on the server.
#>
function Send-KrSseBroadcastEvent {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Event,

        [Parameter(Mandatory = $true)]
        [string]$Data,

        [Parameter(Mandatory = $false)]
        [string]$Id,

        [Parameter(Mandatory = $false)]
        [int]$RetryMs,

        [Parameter(ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server
    )

    process {
        try {
            if (-not $Server) {
                try { $Server = Get-KrServer } catch { $Server = $null }
            }

            if ($null -eq $Server) {
                Write-KrOutsideRouteWarning
                return
            }

            $task = [Kestrun.Hosting.KestrunHostSseExtensions]::BroadcastSseEventAsync(
                $Server,
                $Event,
                $Data,
                $Id,
                ($RetryMs -as [Nullable[int]]),
                [System.Threading.CancellationToken]::None
            )

            $ok = $task.GetAwaiter().GetResult()

            if (-not $ok) {
                Write-KrLog -Level Warning -Message 'SSE broadcast failed (is Add-KrSseBroadcastMiddleware configured?)'
            }
        } catch {
            Write-KrLog -Level Error -Message "Failed to broadcast SSE event '$Event': $_" -Exception $_.Exception
        }
    }
}
