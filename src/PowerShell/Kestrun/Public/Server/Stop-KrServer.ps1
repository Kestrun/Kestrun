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
    .EXAMPLE
        Start-KrServer -Server $server
        Starts the specified Kestrun server instance and listens for incoming requests.
    .NOTES
        This function is designed to be used after the server has been configured and routes have been added.
        It will block the console until the server is stopped or Ctrl+C is pressed.
#>
function Stop-KrServer {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [switch]$NoWait,
        [Parameter()]
        [switch]$Quiet
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($null -ne $Context -and $null -ne $Context.Response) {
            # If called within a route, send a response before stopping
            Write-KrTextResponse -InputObject 'Server is stopping...' -StatusCode 202
            Start-Sleep -Seconds 2
             $Server.StopAsync() 
            return
        }
        # Stop the Kestrel server
        $Server.StopAsync() | Out-Null
        # Ensure the server is stopped on exit
        if (-not $Quiet.IsPresent) {
            Write-Host 'Stopping Kestrun server...' -NoNewline
        }

        # If NoWait is specified, return immediately
        if ($NoWait.IsPresent) {
            return
        }

        while ($Server.IsRunning) {
            Start-Sleep -Seconds 1
            if (-not $Quiet.IsPresent) {
                Write-Host '#' -NoNewline
            }
        }

        [Kestrun.KestrunHostManager]::Destroy($Server.ApplicationName)

        if (-not $Quiet.IsPresent) {
            Write-Host 'Kestrun server stopped.'
        }
    }
}

