
<#
.SYNOPSIS
    Resolves a Kestrun server instance from the provided input.

.DESCRIPTION
    The Resolve-KestrunServer function checks if the provided server instance is valid.
    If not, it attempts to retrieve the default Kestrun server instance.

.PARAMETER Server
    The Kestrun server instance to resolve.

.EXAMPLE
    $resolvedServer = Resolve-KestrunServer -Server $myServer
    This will resolve $myServer to a valid Kestrun server instance.

.NOTES
    If no server is provided, the function will look for the default Kestrun server instance
    managed by KestrunHostManager.  If no default instance exists, an error is thrown.
    Used inside kestrun cmdlets to ensure a valid server instance is available for operations
    as:  $Server = Resolve-KestrunServer -Server $Server
#>
function Resolve-KestrunServer {
    param (
        [Kestrun.Hosting.KestrunHost]$Server
    )
    if ($null -eq $Server) {
        $Server = [Kestrun.KestrunHostManager]::Default
        if ($null -eq $Server) {
            throw 'No Kestrun server instance found. Please create a Kestrun server instance.'
        }
    }
    return $Server
}

