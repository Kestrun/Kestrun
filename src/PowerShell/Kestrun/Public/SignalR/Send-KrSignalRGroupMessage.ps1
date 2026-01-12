<#
    .SYNOPSIS
        Broadcasts a message to a specific SignalR group.
    .DESCRIPTION
        This function sends a message to all clients in a specific SignalR group via the IRealtimeBroadcaster service.
        The message is broadcast in real-time to all connected clients listening in the specified group.
    .PARAMETER GroupName
        The name of the group to broadcast to.
    .PARAMETER Method
        The hub method name to invoke on clients.
    .PARAMETER Message
        The message to broadcast to the group.
    .PARAMETER Server
        The Kestrun server instance. If not specified, the default server is used.
    .EXAMPLE
        Send-KrSignalRGroupMessage -GroupName "Admins" -Method "ReceiveAdminUpdate" -Message @{ Update = "System maintenance scheduled" }
        Broadcasts an admin update to all clients in the "Admins" group.
    .EXAMPLE
        Send-KrSignalRGroupMessage -GroupName "Workers" -Method "ReceiveTaskUpdate" -Message @{ TaskId = 123; Progress = 75 }
        Broadcasts a task progress update to all clients in the "Workers" group.
    .NOTES
        This function requires that SignalR has been configured on the server using Add-KrSignalRHubMiddleware.
        The IRealtimeBroadcaster service must be registered for this cmdlet to work.
#>
function Send-KrSignalRGroupMessage {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$GroupName,

        [Parameter(Mandatory = $true)]
        [string]$Method,

        [Parameter(Mandatory = $true)]
        [object]$Message,

        [Parameter(ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server
    )

    process {
        try {
            if ($null -ne $Context) {
                if ($Context.BroadcastToGroup($GroupName, $Method, $Message)) {
                    Write-KrLog -Level Debug -Message "Broadcasted to group '$GroupName' via method '$Method': $Message"
                    return
                } else {
                    Write-KrLog -Level Error -Message "Failed to broadcast to group '$GroupName': Unknown error"
                    return
                }
            }

            # Fallback: allow explicit host usage outside a route (no HttpContext)
            if (-not $Server) {
                try { $Server = Get-KrServer } catch { $Server = $null }
            }
            if ($null -ne $Server) {
                $task = [Kestrun.Hosting.KestrunHostSignalRExtensions]::BroadcastToGroupAsync($Server, $GroupName, $Method, $Message, $null, [System.Threading.CancellationToken]::None)
                $null = $task.GetAwaiter().GetResult()
                Write-KrLog -Level Debug -Message "Broadcasted to group '$GroupName' via method '$Method' (host fallback): $Message"
            } else {
                Write-KrOutsideRouteWarning
            }
        } catch {
            Write-KrLog -Level Error -Message "Failed to broadcast to group '$GroupName': $_" -Exception $_.Exception
        }
    }
}
