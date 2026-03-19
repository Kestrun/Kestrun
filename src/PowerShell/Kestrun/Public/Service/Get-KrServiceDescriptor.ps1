<#
.SYNOPSIS
    Reads a Service.psd1 descriptor file.
.DESCRIPTION
    Reads Service.psd1 and returns a normalized object with Name, Description, Version, Script, and ServiceLogPath.
.PARAMETER Path
    Descriptor path. Defaults to Service.psd1 in the current directory.
.EXAMPLE
    Get-KrServiceDescriptor
#>
function Get-KrServiceDescriptor {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([pscustomobject])]
    param(
        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]$Path = 'Service.psd1'
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Descriptor file not found: $fullPath"
    }

    $descriptor = Import-PowerShellDataFile -LiteralPath $fullPath
    $requiredKeys = @('Name', 'Description', 'Version')
    foreach ($key in $requiredKeys) {
        if (-not $descriptor.ContainsKey($key) -or [string]::IsNullOrWhiteSpace([string]$descriptor[$key])) {
            throw "Descriptor '$fullPath' is missing required key '$key'."
        }
    }

    $parsedVersion = $null
    if (-not [version]::TryParse([string]$descriptor['Version'], [ref]$parsedVersion)) {
        throw "Descriptor '$fullPath' has invalid Version value '$($descriptor['Version'])'."
    }

    [pscustomobject]([ordered]@{
            Path = $fullPath
            Name = [string]$descriptor['Name']
            Description = [string]$descriptor['Description']
            Version = $parsedVersion.ToString()
            Script = if ($descriptor.ContainsKey('Script')) { [string]$descriptor['Script'] } else { $null }
            ServiceLogPath = if ($descriptor.ContainsKey('ServiceLogPath')) { [string]$descriptor['ServiceLogPath'] } else { $null }
        })
}
