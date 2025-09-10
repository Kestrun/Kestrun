<#
.SYNOPSIS
    Sets the named pipe options for a Kestrun server instance. (Windows Operating Systems only)
.DESCRIPTION
    This function sets the named pipe options for the specified Kestrun server instance, allowing for configuration of various named pipe transport settings.
.PARAMETER Server
    The Kestrun server instance to configure. This parameter is mandatory and must be a valid server object.
.PARAMETER Options
    The NamedPipeTransportOptions object containing the desired named pipe configuration settings.
    This parameter is mandatory when using the 'Options' parameter set.
.PARAMETER ListenerQueueCount
    Specifies the number of named pipe listener queues to create for the server. This parameter is optional and can be set to a specific value or left unset to use defaults.
.PARAMETER MaxReadBufferSize
    Specifies the maximum size, in bytes, of the read buffer for named pipe connections. This parameter is optional and can be set to a specific value or left unset to use defaults.
.PARAMETER CurrentUserOnly
    If specified, the named pipe will only be accessible by the current user. This parameter is optional and can be left unset to use defaults.
.PARAMETER MaxWriteBufferSize
    Specifies the maximum size, in bytes, of the write buffer for named pipe connections. This parameter is optional and can be set to a specific value or left unset to use defaults.
.PARAMETER PipeSecurity
    Specifies the PipeSecurity object to apply to the named pipe. This parameter is optional and can be set to a specific value or left unset to use defaults.
.PARAMETER PassThru
    If specified, the cmdlet will return the modified server instance after applying the named pipe options.
.OUTPUTS
    [Kestrun.Hosting.KestrunHost]
    The modified Kestrun server instance with the updated named pipe options.
.EXAMPLE
    Set-KrServerNamedPipeOptions -Server $server -ListenerQueueCount 5 -MaxReadBufferSize 65536
    This command sets the named pipe options for the specified Kestrun server instance, configuring the listener queue count and maximum read buffer size.
.EXAMPLE
    Set-KrServerNamedPipeOptions -Server $server -CurrentUserOnly
    This command configures the named pipe options for the specified Kestrun server instance to restrict access to the current user only.
.NOTES
    This function is for Windows Operating Systems only, as named pipes are not supported on Unix-based systems.
    The named pipe options will be applied to the server's options and will be used when the server is started to listen for incoming requests on the specified named pipe.
    The named pipe transport options can be configured to optimize performance and security based on the specific requirements of the Kestrun server deployment.
    The named pipe transport options can be set either by providing a complete NamedPipeTransportOptions object
    This function is designed to be used in the context of a Kestrun server setup and allows for flexible configuration of named pipe transport options.
#>
function Set-KrServerNamedPipeOptions {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes.NamedPipeTransportOptions]$Options,
        [Parameter( ParameterSetName = 'Items')]
        [int]$ListenerQueueCount,
        [Parameter( ParameterSetName = 'Items')]
        [long]$MaxReadBufferSize,
        [Parameter( ParameterSetName = 'Items')]
        [switch]$CurrentUserOnly,
        [Parameter( ParameterSetName = 'Items')]
        [long]$MaxWriteBufferSize,
        [Parameter( ParameterSetName = 'Items')]
        [System.IO.Pipes.PipeSecurity]$PipeSecurity,
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
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            $Options = [Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes.NamedPipeTransportOptions]::new()
            if ($null -ne $ListenerQueueCount) {
                Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting NamedPipeOptions.ListenerQueueCount to {ListenerQueueCount}" -Properties $ListenerQueueCount
                $options.ListenerQueueCount = $ListenerQueueCount
            }
            if ($null -ne $MaxReadBufferSize) {
                Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting NamedPipeOptions.MaxReadBufferSize to {MaxReadBufferSize}" -Properties $MaxReadBufferSize
                $options.MaxReadBufferSize = $MaxReadBufferSize
            }
            if ($CurrentUserOnly.IsPresent) {
                Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting NamedPipeOptions.CurrentUserOnly to {CurrentUserOnly}" -Properties $CurrentUserOnly
                $Options.CurrentUserOnly = $true
            }
            if ($null -ne $MaxWriteBufferSize) {
                Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting NamedPipeOptions.MaxWriteBufferSize to {MaxWriteBufferSize}" -Properties $MaxWriteBufferSize
                $Options.MaxWriteBufferSize = $MaxWriteBufferSize
            }
            if ($null -ne $PipeSecurity) {
                Write-KrLog -Logger $Server.HostLogger -Level Verbose -Message "Setting NamedPipeOptions.PipeSecurity to {PipeSecurity}" -Properties $PipeSecurity
                $Options.PipeSecurity = $PipeSecurity
            }
        }

        $Server.Options.NamedPipeOptions = $Options

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}

