<#
.SYNOPSIS
    Adds a Unix socket listener to a Kestrun server instance.
.DESCRIPTION
    This function adds a Unix socket listener to the specified Kestrun server instance, allowing it to listen for incoming requests on the specified Unix socket.
.PARAMETER Server
    The Kestrun server instance to which the Unix socket listener will be added. This parameter is optional and can be provided via pipeline input.
.PARAMETER SocketPath
    The path of the Unix domain socket on which the server will listen for incoming requests. This parameter is mandatory.
.PARAMETER PassThru
    If specified, the cmdlet will return the modified server instance after adding the Unix socket listener
.EXAMPLE
    Add-KrListenUnixSocket -Server $server -SocketPath "/tmp/mysocket"
    Adds a Unix socket listener with the specified path to the given Kestrun server instance.
.NOTES
    This function is designed to be used in the context of a Kestrun server setup and allows for flexible configuration of Unix socket listeners.
    The Unix socket listener will be added to the server's options and will be used when the server is started to listen for incoming requests on the specified Unix socket.
#>
function Add-KrListenUnixSocket {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'NoCert')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [string]$SocketPath,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        if (-not $IsWindows) {
            Write-Warning 'This function is for Windows Operating Systems only, as named pipes are not supported nativelyon Unix-based systems.'
        }
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        if ($null -eq $Server) {
            throw 'Server is not initialized. Please ensure the server is configured before setting options.'
        }
    }
    process {
        # Add the Unix socket listener to the server options
        $Server.Options.ListenUnixSockets.Add($SocketPath)

        if ($PassThru.IsPresent) {
            # Return the modified server instance
            return $Server
        }
    }
}

