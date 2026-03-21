<#
.SYNOPSIS
    Reads a Service.psd1 descriptor file.
.DESCRIPTION
    Reads Service.psd1 and returns a normalized object with FormatVersion, Name, Description, Version, EntryPoint, ServiceLogPath, and PreservePaths.
.PARAMETER Path
    Descriptor path. Accepts either a descriptor file path or a directory path.
    When a directory path is provided, Service.psd1 is appended automatically.
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
    if (Test-Path -LiteralPath $fullPath -PathType Container) {
        $fullPath = Join-Path -Path $fullPath -ChildPath 'Service.psd1'
    }

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

    $props = $normalizedDescriptor.PSObject.Properties

    [pscustomobject]([ordered]@{
            FormatVersion = [string]$normalizedDescriptor.FormatVersion
            Path = $fullPath
            Name = [string]$normalizedDescriptor.Name
            Description = if ($props['Description']) { [string]$normalizedDescriptor.Description } else { $null }
            Version = if ($props['Version'] -and -not [string]::IsNullOrWhiteSpace($normalizedDescriptor.Version)) {
                [string]$normalizedDescriptor.Version
            } else {
                $null
            }
            EntryPoint = if ($props['EntryPoint']) { [string]$normalizedDescriptor.EntryPoint } else { $null }
            ServiceLogPath = if ($props['ServiceLogPath']) { [string]$normalizedDescriptor.ServiceLogPath } else { $null }
            PreservePaths = $preservePaths
        })
}
