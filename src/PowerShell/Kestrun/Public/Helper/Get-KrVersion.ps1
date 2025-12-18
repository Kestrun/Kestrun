<#
.SYNOPSIS
    Retrieves the Kestrun module version information.
.DESCRIPTION
    This function returns the version information of the Kestrun PowerShell module.
    It can return either a formatted version string or a detailed object containing version components.
.PARAMETER AsString
    If specified, the function returns the version as a formatted string (e.g., "1.2.3-preview").
    If not specified, the function returns a custom object with detailed version information.
.EXAMPLE
    Get-KrVersion -AsString
    Returns the Kestrun module version as a formatted string.
.EXAMPLE
    Get-KrVersion
    Returns a custom object with detailed version information.
.NOTES
    This function is useful for retrieving version information for logging, diagnostics, or display purposes.
#>
function Get-KrVersion {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([string])]
    [OutputType([psobject])]
    param(
        [Parameter()]
        [switch] $AsString
    )

    $module = Get-Module -Name Kestrun -ErrorAction SilentlyContinue

    if (-not $module) {
        Write-Verbose 'Kestrun module is not loaded.'
        return $null
    }

    $version = $module.Version
    $prerelease = $module.PrivateData?.PSData?.Prerelease

    $full = if ($prerelease) {
        "$version-$prerelease"
    } else {
        "$version"
    }

    if ($AsString) {
        return $full
    }

    [pscustomobject]@{
        Name = $module.Name
        Version = $version
        Prerelease = $prerelease
        FullVersion = $full
        Path = $module.Path
    }
}
