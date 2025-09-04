<#
    .SYNOPSIS
        Initializes the Kestrun root directory for path resolution.
    .DESCRIPTION
        This function sets the Kestrun root directory, which is used as a base for resolving relative paths in Kestrun applications.
        It is typically called during the initialization phase of a Kestrun application.
        This function should be called before any other Kestrun commands that rely on the root directory being set.
    .PARAMETER Path
        The path to the Kestrun root directory.
    .PARAMETER PassThru
        If specified, the cmdlet will return the absolute path to the Kestrun root directory after setting it.
    .EXAMPLE
        Initialize-KrRoot -Path "C:\Kestrun"
        Sets the Kestrun root directory to "C:\Kestrun".
    .EXAMPLE
        Initialize-KrRoot -Path "~/Kestrun"
        Sets the Kestrun root directory to the user's home directory.
    .EXAMPLE
        Initialize-KrRoot -Path "D:\Projects\Kestrun"
        Sets the Kestrun root directory to "D:\Projects\Kestrun".
    .EXAMPLE
        Initialize-KrRoot -Path "C:\Kestrun" -PassThru
        Returns the absolute path to the Kestrun root directory.
    .OUTPUTS
        [string] The absolute path to the Kestrun root directory.
    .NOTES
        This function is designed to be used in the context of a Kestrun server to ensure consistent path resolution.
#>
function Initialize-KrRoot {
    [CmdletBinding()]
    [KestrunRuntimeApi('Definition')]
    [OutputType([string])]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string] $Path,
        [switch] $PassThru
    )

    # Expand ~
    if ($Path -like '~*') {
        $Path = $Path -replace '^~', $HOME
    }

    # Resolve to absolute path
    $resolvedPath = Resolve-Path -Path $Path -ErrorAction Stop |
        Select-Object -First 1 -ExpandProperty Path

    # Save for use in C# runtime
    [Kestrun.KestrunHostManager]::KestrunRoot = $resolvedPath

    if ($PassThru) {
        # Return absolute path for chaining
        return [Kestrun.KestrunHostManager]::KestrunRoot
    }
}

