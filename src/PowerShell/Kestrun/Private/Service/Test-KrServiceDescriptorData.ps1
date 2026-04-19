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

    function Get-KrNormalizedDescriptorRelativePathArray {
        <#
        .SYNOPSIS
            Normalizes and validates relative paths from a descriptor key.
        .PARAMETER KeyName
            The key name in the descriptor hashtable to process.
        .PARAMETER EntryLabel
            A label for the entry, used in error messages.
        .OUTPUTS
            An array of normalized relative paths.
        #>
        param(
            [string]$KeyName,
            [string]$EntryLabel
        )

        $normalizedPaths = @()
        if (-not $Descriptor.ContainsKey($KeyName) -or $null -eq $Descriptor[$KeyName]) {
            return $normalizedPaths
        }

        $descriptorValue = $Descriptor[$KeyName]
        $rawPaths = @()
        if ($descriptorValue -is [string]) {
            $rawPaths = @([string]$descriptorValue)
        } elseif ($descriptorValue -is [hashtable] -or $descriptorValue -is [System.Collections.IDictionary]) {
            throw "Descriptor '$DescriptorPath' key '$KeyName' must be a string array."
        } elseif ($descriptorValue -is [System.Array] -or $descriptorValue -is [System.Collections.IList]) {
            $rawPaths = @($descriptorValue)
        } else {
            throw "Descriptor '$DescriptorPath' key '$KeyName' must be a string array."
        }

        foreach ($pathValue in $rawPaths) {
            $relativePath = [string]$pathValue
            if ([string]::IsNullOrWhiteSpace($relativePath)) {
                throw "Descriptor '$DescriptorPath' key '$KeyName' cannot contain empty values."
            }

            if ([System.IO.Path]::IsPathRooted($relativePath)) {
                throw "Descriptor '$DescriptorPath' $EntryLabel entry '$relativePath' must be a relative path."
            }

            $combinedPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($packageRootFullPath, $relativePath))
            if (-not (& $isWithinPackageRoot $combinedPath)) {
                throw "Descriptor '$DescriptorPath' $EntryLabel entry '$relativePath' escapes the package root."
            }

            $normalizedPaths += $relativePath
        }

        return $normalizedPaths
    }

    $normalizedPreservePaths = Get-KrNormalizedDescriptorRelativePathArray -KeyName 'PreservePaths' -EntryLabel 'PreservePaths'
    $normalizedApplicationDataFolders = Get-KrNormalizedDescriptorRelativePathArray -KeyName 'ApplicationDataFolders' -EntryLabel 'ApplicationDataFolders'

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
        ApplicationDataFolders = $normalizedApplicationDataFolders
    }
}
