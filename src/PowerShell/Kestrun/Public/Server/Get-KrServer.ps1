<#
.SYNOPSIS
    Gets the current Kestrun server instance.
.DESCRIPTION
    This function retrieves the current Kestrun server instance from the context.
.PARAMETER Server
    The Kestrun server instance to retrieve. If not specified, the function will attempt to resolve the current server context.
.OUTPUTS
    [Kestrun.Hosting.KestrunHost]
    The current Kestrun server instance.
.EXAMPLE
    Get-KrServer
    This command retrieves the current Kestrun server instance.
.NOTES
    This function is part of the Kestrun PowerShell module and is used to manage Kestrun server instances.
    If the server instance is not found in the context, it attempts to resolve it using the Resolve-KestrunServer function.
#>
function Get-KrServer {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param()
    if ($null -eq $Context -or $null -eq $Context.Response) {
        return Resolve-KestrunServer -Server $Server
    } else {
        return $Context.Host
    }
}
