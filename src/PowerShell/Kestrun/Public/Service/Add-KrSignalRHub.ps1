<#
    .SYNOPSIS
        Maps a SignalR hub class to the given URL path.
    .DESCRIPTION
        This function allows you to map a SignalR hub class to a specific URL path on the Kestrun server.
    .PARAMETER Server
        The Kestrun server instance to which the SignalR hub will be added.
    .PARAMETER HubType
        The type of the SignalR hub class to be mapped.
    .PARAMETER Path
        The URL path where the SignalR hub will be accessible.
    .PARAMETER PassThru
        If specified, the cmdlet will return the modified server instance after adding the SignalR hub.
    .EXAMPLE
        $server | Add-KrSignalRHub -HubType ([ChatHub]) -Path "/chat"
        This example maps the ChatHub class to the "/chat" URL path on the specified Kestrun server.
    .EXAMPLE
        Get-KrServer | Add-KrSignalRHub -HubType ([ChatHub]) -Path "/chat"
        This example retrieves the current Kestrun server and maps the ChatHub class to the "/chat" URL path.
    .NOTES
        This function is part of the Kestrun PowerShell module and is used to manage SignalR hubs on the Kestrun server.
        The HubType parameter must be a valid SignalR hub class type.
        The Path parameter specifies the URL path where the SignalR hub will be accessible.
        The function uses reflection to find and invoke the generic AddSignalR<T>(string) method on the KestrunHost instance.
        This allows for dynamic mapping of SignalR hubs to specific URL paths at runtime.
        The function returns the modified server instance for further chaining if needed.
        The function ensures that the server instance is resolved before proceeding with the mapping.
        The function is designed to be used in a pipeline, allowing for easy integration with other Kestrun commands.
        The function is part of the Kestrun.Hosting namespace and is used to extend the functionality of the Kestrun server.
        The function is designed to be used in a modular way, allowing for easy addition of SignalR hubs to the Kestrun server.
        The function is intended for use in scenarios where SignalR hubs need to be dynamically mapped to specific URL paths at runtime.
        The function is part of the Kestrun.Hosting library and is used to manage SignalR hubs on the Kestrun server.
        The function is designed to be used in a modular way, allowing for easy addition of SignalR hubs to the Kestrun server.
        The function is intended for use in scenarios where SignalR hubs need to be dynamically mapped to specific URL paths at runtime.
#>
function Add-KrSignalRHub {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory)]
        [Type]$HubType,

        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        if ($null -eq $Server) {
            throw 'Server is not initialized. Please ensure the server is configured before setting options.'
        }
    }
    process {
        Write-KrLog -Level Warning 'Add-KrSignalRHub is an experimental feature and may not work as expected.'

        # 1.  Find the generic method definition on KestrunHost
        $method = $Server.GetType().GetMethods() |
            Where-Object {
                $_.Name -eq 'AddSignalR' -and
                $_.IsGenericMethod -and
                $_.GetParameters().Count -eq 1        # (string path)
            }

        if (-not $method) {
            throw 'Could not locate the generic AddSignalR<T>(string) method.'
        }

        # 2.  Close the generic with the hub type from the parameter
        $closed = $method.MakeGenericMethod(@($HubType))

        # 3.  Invoke it, passing the path; return the resulting server for chaining
        $closed.Invoke($Server, @($Path)) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}

