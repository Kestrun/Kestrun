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
        # Resolve the server instance if available (not strictly required when $Context is present)
        if (-not $Server) {
            $Server = Get-KrServer
        }
    }

    process {
        try {
            # Prefer resolving from the current request's service provider when running inside a route
            $svcProvider = $null
            if ($null -ne $Context -and $null -ne $Context.HttpContext) {
                $svcProvider = $Context.HttpContext.RequestServices
            } elseif ($null -ne $Server) {
                # Fallback: best-effort reflection access to internal App property to get Services
                try {
                    $appProp = $Server.GetType().GetProperty('App', [System.Reflection.BindingFlags] 'NonPublic,Instance')
                    if ($appProp) {
                        $appVal = $appProp.GetValue($Server)
                        if ($null -ne $appVal) { $svcProvider = $appVal.Services }
                    }
                } catch { }
            }

            if (-not $svcProvider) {
                Write-KrLog -Level Warning -Message 'No service provider available to resolve IRealtimeBroadcaster.'
                return
            }

            # Get the IRealtimeBroadcaster service from DI
            $broadcaster = $svcProvider.GetService([Kestrun.SignalR.IRealtimeBroadcaster])

            if (-not $broadcaster) {
                Write-KrLog -Level Warning -Message 'IRealtimeBroadcaster service is not registered. Make sure SignalR is configured with KestrunHub.'
                return
            }

            # Broadcast the log message asynchronously
            $task = $broadcaster.BroadcastLogAsync($Level, $Message, [System.Threading.CancellationToken]::None)
            $task.GetAwaiter().GetResult()

            Write-KrLog -Level Debug -Message "Broadcasted log message: $Level - $Message"
        } catch {
            Write-KrLog -Level Error -Message "Failed to broadcast log message: $_" -Exception $_.Exception
        }
    }
}
