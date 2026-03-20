<#
.SYNOPSIS
    Reads a Service.psd1 descriptor file.
.DESCRIPTION
    Reads Service.psd1 and returns a normalized object with FormatVersion, Name, Description, Version, EntryPoint, ServiceLogPath, and PreservePaths.
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
    if (-not $descriptor -or -not ($descriptor -is [hashtable])) {
        throw "Descriptor file '$fullPath' is not a valid hashtable."
    }
    $normalizedDescriptor = Test-KrServiceDescriptorData -Descriptor $descriptor -DescriptorPath $fullPath -PackageRoot ([System.IO.Path]::GetDirectoryName($fullPath))
    if (-not $normalizedDescriptor) {
        throw "Descriptor file '$fullPath' failed validation."
    }

    $preservePaths = @()
    if ($normalizedDescriptor.PSObject.Properties.Match('PreservePaths').Count -gt 0 -and $null -ne $normalizedDescriptor.PreservePaths) {
        $preservePaths = @($normalizedDescriptor.PreservePaths)
    }

    [pscustomobject]([ordered]@{
            FormatVersion = [string]$normalizedDescriptor.FormatVersion
            Path = $fullPath
            Name = [string]$normalizedDescriptor.Name
            Description = if ($normalizedDescriptor.PSObject.Properties.Match('Description').Count -gt 0) { [string]$normalizedDescriptor.Description } else { $null }
            Version = if ($normalizedDescriptor.PSObject.Properties.Match('Version').Count -gt 0 -and -not [string]::IsNullOrWhiteSpace([string]$normalizedDescriptor.Version)) { [string]$normalizedDescriptor.Version } else { $null }
            EntryPoint = if ($normalizedDescriptor.PSObject.Properties.Match('EntryPoint').Count -gt 0) { [string]$normalizedDescriptor.EntryPoint } else { $null }
            ServiceLogPath = if ($normalizedDescriptor.PSObject.Properties.Match('ServiceLogPath').Count -gt 0) { [string]$normalizedDescriptor.ServiceLogPath } else { $null }
            PreservePaths = $preservePaths
        })
}
