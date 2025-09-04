<#
    .SYNOPSIS
        Adds a favicon to the Kestrun server.
    .DESCRIPTION
        This cmdlet allows you to register a favicon for the Kestrun server.
        It can be used to set a custom favicon for the server's web interface.
    .PARAMETER Server
        The Kestrun server instance to which the favicon will be added.
    .PARAMETER IconPath
        The path to the favicon file. If not specified, a default embedded favicon will be used.
    .PARAMETER PassThru
        If specified, returns the modified server instance after adding the favicon.
    .EXAMPLE
        $server | Add-KrFavicon -IconPath 'C:\path\to\favicon.ico'
        This example adds a custom favicon to the server from the specified path.
    .EXAMPLE
        $server | Add-KrFavicon
        This example adds the default embedded favicon to the server.
    .NOTES
        This cmdlet is used to register a favicon for the Kestrun server, allowing you to set a custom favicon for the server's web interface.
        If no icon path is specified, the default embedded favicon will be used.
#>
function Add-KrFavicon {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [string]$IconPath = $null,

        [Parameter()]
        [switch]$PassThru
    )
    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        [Kestrun.Hosting.KestrunHostStaticFilesExtensions]::AddFavicon($Server, $IconPath) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}

