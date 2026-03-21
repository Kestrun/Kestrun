<#
.SYNOPSIS
    Validates and processes a service descriptor hashtable.
.DESCRIPTION
    Validates the structure and required keys of a format 1.0 service descriptor hashtable.
    Also checks that referenced entry point files exist within the package root and do not escape it.
.PARAMETER Descriptor
    The service descriptor as a hashtable, typically parsed from Service.psd1.
.PARAMETER DescriptorPath
    The file path of the descriptor, used for error messages.
.PARAMETER PackageRoot
    The root directory of the package, used to resolve and validate script paths.
.EXAMPLE
    $descriptor = @{
        Name = 'MyService'
        FormatVersion = '1.0'
        EntryPoint = 'server.ps1'
        Description = 'A sample service.'
        Version = '1.0.0'
    }
    Test-KrServiceDescriptorData -Descriptor $descriptor -DescriptorPath '.\Service.psd1' -PackageRoot '.\'
#>
function Test-KrServiceDescriptorData {
    param(
        [hashtable]$Descriptor,
        [string]$DescriptorPath,
        [string]$PackageRoot
    )

    if (-not $Descriptor.ContainsKey('Name') -or [string]::IsNullOrWhiteSpace([string]$Descriptor['Name'])) {
        throw "Descriptor '$DescriptorPath' is missing required key 'Name'."
    }

    $packageRootFullPath = [System.IO.Path]::GetFullPath($PackageRoot)
    $packageRootNormalized = [System.IO.Path]::TrimEndingDirectorySeparator($packageRootFullPath)

    $isWithinPackageRoot = {
        param([string]$PathToValidate)

        $normalizedPath = [System.IO.Path]::TrimEndingDirectorySeparator($PathToValidate)
        $relativePath = [System.IO.Path]::GetRelativePath($packageRootNormalized, $normalizedPath)

        if ([string]::Equals($relativePath, '.', [System.StringComparison]::Ordinal)) {
            return $true
        }

        if ([System.IO.Path]::IsPathRooted($relativePath)) {
            return $false
        }

        return -not (
            [string]::Equals($relativePath, '..', [System.StringComparison]::Ordinal) -or
            $relativePath.StartsWith("..$([System.IO.Path]::DirectorySeparatorChar)", [System.StringComparison]::Ordinal) -or
            $relativePath.StartsWith("..$([System.IO.Path]::AltDirectorySeparatorChar)", [System.StringComparison]::Ordinal)
        )
    }

    $normalizedPreservePaths = @()
    if ($Descriptor.ContainsKey('PreservePaths') -and $null -ne $Descriptor['PreservePaths']) {
        $rawPreservePaths = @()
        if ($Descriptor['PreservePaths'] -is [string]) {
            $rawPreservePaths = @([string]$Descriptor['PreservePaths'])
        } elseif ($Descriptor['PreservePaths'] -is [System.Collections.IEnumerable]) {
            $rawPreservePaths = @($Descriptor['PreservePaths'])
        } else {
            throw "Descriptor '$DescriptorPath' key 'PreservePaths' must be a string array."
        }

        foreach ($preservePathValue in $rawPreservePaths) {
            $preservePath = [string]$preservePathValue
            if ([string]::IsNullOrWhiteSpace($preservePath)) {
                throw "Descriptor '$DescriptorPath' key 'PreservePaths' cannot contain empty values."
            }

            if ([System.IO.Path]::IsPathRooted($preservePath)) {
                throw "Descriptor '$DescriptorPath' PreservePaths entry '$preservePath' must be a relative path."
            }

            $combinedPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($packageRootFullPath, $preservePath))
            if (-not (& $isWithinPackageRoot $combinedPath)) {
                throw "Descriptor '$DescriptorPath' PreservePaths entry '$preservePath' escapes the package root."
            }

            $normalizedPreservePaths += $preservePath
        }
    }

    if (-not $Descriptor.ContainsKey('FormatVersion') -or [string]::IsNullOrWhiteSpace([string]$Descriptor['FormatVersion'])) {
        throw "Descriptor '$DescriptorPath' is missing required key 'FormatVersion'."
    }

    $formatVersion = [string]$Descriptor['FormatVersion']
    if (-not [string]::Equals($formatVersion.Trim(), '1.0', [System.StringComparison]::Ordinal)) {
        throw "Descriptor '$DescriptorPath' has unsupported FormatVersion '$formatVersion'. Expected '1.0'."
    }

    foreach ($requiredKey in @('Description', 'Version', 'EntryPoint')) {
        if (-not $Descriptor.ContainsKey($requiredKey) -or [string]::IsNullOrWhiteSpace([string]$Descriptor[$requiredKey])) {
            throw "Descriptor '$DescriptorPath' is missing required key '$requiredKey'."
        }
    }

    $parsedVersion = $null
    if (-not [version]::TryParse([string]$Descriptor['Version'], [ref]$parsedVersion)) {
        throw "Descriptor '$DescriptorPath' has invalid Version value '$($Descriptor['Version'])'."
    }

    $entryPoint = [string]$Descriptor['EntryPoint']
    if ([System.IO.Path]::IsPathRooted($entryPoint)) {
        throw "Descriptor '$DescriptorPath' EntryPoint must be a relative path."
    }

    $entryPointFullPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($packageRootFullPath, $entryPoint))
    if (-not (& $isWithinPackageRoot $entryPointFullPath)) {
        throw "Descriptor '$DescriptorPath' EntryPoint escapes the package root."
    }

    if (-not (Test-Path -LiteralPath $entryPointFullPath -PathType Leaf)) {
        throw "EntryPoint file '$entryPoint' was not found under '$PackageRoot'."
    }

    [pscustomobject]@{
        Name = [string]$Descriptor['Name']
        FormatVersion = '1.0'
        EntryPoint = $entryPoint
        Description = [string]$Descriptor['Description']
        Version = $parsedVersion.ToString()
        ServiceLogPath = if ($Descriptor.ContainsKey('ServiceLogPath')) { [string]$Descriptor['ServiceLogPath'] } else { $null }
        PreservePaths = $normalizedPreservePaths
    }
}
