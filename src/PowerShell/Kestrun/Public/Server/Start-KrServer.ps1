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
    .PARAMETER CloseLogsOnExit
        If specified, closes all loggers when the server stops.
    .PARAMETER PassThru
        If specified, the cmdlet will return the modified server instance after starting it.
    .EXAMPLE
        Start-KrServer -Server $server
        Starts the specified Kestrun server instance and listens for incoming requests.
    .NOTES
        This function is designed to be used after the server has been configured and routes have been added.
        It will block the console until the server is stopped or Ctrl+C is pressed.
#>
function Start-KrServer {
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
        [switch]$PassThru,
        [Parameter()]
        [switch]$CloseLogsOnExit
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        if ($null -eq $Server) {
            throw 'Server is not initialized. Please ensure the server is configured before setting options.'
        }
    }
    process {
        # Start the Kestrel server
        if ( -not $Quiet.IsPresent ) {
            Write-Host "Starting Kestrun server '$($Server.ApplicationName)' ..."
        }
        $Server.StartAsync() | Out-Null
        if (-not $Quiet.IsPresent) {
            Write-Host 'Kestrun server started successfully.'
            foreach ($listener in $Server.Options.Listeners) {
                if ($listener.X509Certificate) {
                    Write-Host "Listening on https://$($listener.IPAddress):$($listener.Port) with protocols: $($listener.Protocols)"
                } else {
                    Write-Host "Listening on http://$($listener.IPAddress):$($listener.Port) with protocols: $($listener.Protocols)"
                }
                if ($listener.X509Certificate) {
                    Write-Host "Using certificate: $($listener.X509Certificate.Subject)"
                } else {
                    Write-Host 'No certificate configured. Running in HTTP mode.'
                }
                if ($listener.UseConnectionLogging) {
                    Write-Host 'Connection logging is enabled.'
                } else {
                    Write-Host 'Connection logging is disabled.'
                }
            }
            Write-Host 'Press Ctrl+C to stop the server.'
        }
        if (-not $NoWait.IsPresent) {
            # Intercept Ctrl+C and gracefully stop the Kestrun server
            try {
                [Console]::TreatControlCAsInput = $true
                while ($true) {
                    if ([Console]::KeyAvailable) {
                        $key = [Console]::ReadKey($true)
                        if (($key.Modifiers -eq 'Control') -and ($key.Key -eq 'C')) {
                            if (-not $Quiet.IsPresent) {
                                Write-Host 'Ctrl+C detected. Stopping Kestrun server...'
                            }
                            $Server.StopAsync().Wait()
                            break
                        }
                    }
                    Start-Sleep -Milliseconds 100
                }
            } finally {
                # Ensure the server is stopped on exit
                if (-not $Quiet.IsPresent) {
                    Write-Host 'Stopping Kestrun server...'
                }
                [Kestrun.KestrunHostManager]::StopAsync($Server.ApplicationName).Wait()
                #$Server.StopAsync().Wait()
                [Kestrun.KestrunHostManager]::Destroy($Server.ApplicationName)

                if ($CloseLogsOnExit.IsPresent) {
                    # Close the Kestrel loggers
                    Close-KrLogger
                }

                if (-not $Quiet.IsPresent) {
                    Write-Host 'Kestrun server stopped.'
                }
            }
        } elseif ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            return $Server
        }
    }
}

