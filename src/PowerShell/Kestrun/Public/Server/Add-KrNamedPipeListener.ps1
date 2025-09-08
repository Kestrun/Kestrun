<#
.SYNOPSIS
    Adds a named pipe listener to a Kestrun server instance.
.DESCRIPTION
    This function adds a named pipe listener to the specified Kestrun server instance, allowing it to listen for incoming requests on the specified named pipe.
.PARAMETER Server
    The Kestrun server instance to which the named pipe listener will be added. This parameter is optional and can be provided via pipeline input.
.PARAMETER NamedPipeName
    The name of the named pipe on which the server will listen for incoming requests. This parameter is mandatory.
.PARAMETER PassThru
    If specified, the cmdlet will return the modified server instance after adding the named pipe listener
.EXAMPLE
    Add-KrNamedPipeListener -Server $server -NamedPipeName "MyNamedPipe"
    Adds a named pipe listener with the specified name to the given Kestrun server instance.
.NOTES
    This function is designed to be used in the context of a Kestrun server setup and allows for flexible configuration of named pipe listeners.
    The named pipe listener will be added to the server's options and will be used when the server is started to listen for incoming requests on the specified named pipe.
#>
function Add-KrNamedPipeListener {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'NoCert')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [string]$NamedPipeName,

        [Parameter()]
        [switch]$PassThru
    )

    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        # Add the named pipe listener to the server options
        $Server.Options.NamedPipeNames += $NamedPipeName
        if ($PassThru.IsPresent) {
            # Return the modified server instance
            return $Server
        }
    }
}

