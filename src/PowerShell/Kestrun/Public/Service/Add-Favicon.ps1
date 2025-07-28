function Add-Favicon {
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
.EXAMPLE
    $server | Add-Favicon -IconPath 'C:\path\to\favicon.ico'
    This example adds a custom favicon to the server from the specified path.
.EXAMPLE
    $server | Add-Favicon
    This example adds the default embedded favicon to the server.
.NOTES
    This cmdlet is used to register a favicon for the Kestrun server, allowing you to set a custom favicon for the server's web interface.
    If no icon path is specified, the default embedded favicon will be used.
 #>
    [CmdletBinding()]
    [OutputType([Kestrun.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,

        [string]$IconPath = $null
    )
    process { 
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        $Server.AddFavicon($IconPath) | Out-Null
        # Return the modified server instance
        return $Server
    }
}
