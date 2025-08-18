<#
    .SYNOPSIS
        Starts the Kestrun server and listens for incoming requests.
    .DESCRIPTION
        This function starts the Kestrun server, allowing it to accept incoming HTTP requests.
    .PARAMETER Server
        The Kestrun server instance to start. This parameter is mandatory.
    .PARAMETER NoWait
        If specified, the function will not wait for the server to start and will return immediately.
    .PARAMETER Quiet
        If specified, suppresses output messages during the startup process.
    .PARAMETER NoClearVariable
        If specified, prevents clearing of Kestrun-related variables after the server is stopped.
    .EXAMPLE
        Start-KrServer -Server $server
        Starts the specified Kestrun server instance and listens for incoming requests.
    .NOTES
        This function is designed to be used after the server has been configured and routes have been added.
        It will block the console until the server is stopped or Ctrl+C is pressed.
#>
function Stop-KrServer {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [switch]$NoWait,
        [Parameter()]
        [switch]$Quiet,
        [Parameter()]
        [switch]$NoClearVariable
    )
    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        # Stop the Kestrel server
        Write-Host 'Stopping Kestrun ...'
        $Server.StopAsync() | Out-Null
        if ($NoWait.IsPresent) {
            return
        }
        # Ensure the server is stopped on exit
        if (-not $Quiet.IsPresent) {
            Write-Host 'Stopping Kestrun server...' -NoNewline
        }
        while ($Server.IsRunning) {
            Start-Sleep -Seconds 1
            if (-not $Quiet.IsPresent) {
                Write-Host '#' -NoNewline
            }
        }
        #$Server.StopAsync().Wait()
        [Kestrun.KestrunHostManager]::Destroy($Server.ApplicationName)
        #$Server.Dispose()
        if (-not $Quiet.IsPresent) {
            Write-Host 'Kestrun server stopped.'
        }
        if (-not $NoClearVariable.IsPresent) {
            # Clear Kestrun variables
            Clear-KsVariable
        }
    }
}
