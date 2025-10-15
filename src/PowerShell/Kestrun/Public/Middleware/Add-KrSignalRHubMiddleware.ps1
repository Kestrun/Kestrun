<#
    .SYNOPSIS
        Maps a SignalR hub class to the given URL path.
    .DESCRIPTION
        This function allows you to map a SignalR hub class to a specific URL path on the Kestrun server.
    .PARAMETER Server
        The Kestrun server instance to which the SignalR hub will be added.
    .PARAMETER Path
        The URL path where the SignalR hub will be accessible. Defaults to '/hubs/kestrun'.
    .PARAMETER PassThru
        If specified, the cmdlet will return the modified server instance after adding the SignalR hub.
    .EXAMPLE
        Add-KrSignalRHubMiddleware -Path '/hubs/notifications' -PassThru
        Adds a SignalR hub at the path '/hubs/notifications' and returns the modified server instance.
    .NOTES
        This function is part of the Kestrun PowerShell module and is used to manage SignalR hubs on the Kestrun server.
        The Server parameter accepts a KestrunHost instance; if not provided, the default server is used.
        The Path parameter specifies the URL path where the SignalR hub will be accessible.
        The PassThru switch allows the function to return the modified server instance for further use.
#>
function Add-KrSignalRHubMiddleware {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $false)]
        [string]$Path = '/hubs/kestrun',

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {

        $server.AddSignalR($Path) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}

