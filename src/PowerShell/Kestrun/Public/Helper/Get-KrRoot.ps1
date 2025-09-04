<#
    .SYNOPSIS
        Retrieves the Kestrun root directory.
    .DESCRIPTION
        This function returns the path to the Kestrun root directory, which is used as a base for resolving relative paths in Kestrun applications.
    .EXAMPLE
        $kestrunRoot = get-KrRoot
        Retrieves the Kestrun root directory and stores it in the variable $kestrunRoot.
    .NOTES
        This function is designed to be used in the context of a Kestrun server to ensure consistent path resolution.
#>
function Get-KrRoot {
    [CmdletBinding()]
    [KestrunRuntimeApi('Everywhere')]
    [OutputType([string])]
    param()
    return [Kestrun.KestrunHostManager]::KestrunRoot
}

