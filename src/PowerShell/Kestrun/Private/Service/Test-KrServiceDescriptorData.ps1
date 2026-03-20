<#
.SYNOPSIS
    Validates and processes a service descriptor hashtable.
.DESCRIPTION
    Validates the structure and required keys of a service descriptor hashtable, ensuring it conforms to expected formats (either '1.0' or 'legacy').
    Also checks that referenced script files exist within the package root and do not escape it. Returns a processed descriptor object with consistent properties for downstream use.
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
.EXAMPLE
    $descriptor = @{
        Name = 'MyLegacyService'
        Description = 'A legacy service.'
        Version = '0.9.0'
        Script = 'legacy-server.ps1'
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
            if (-not $combinedPath.StartsWith($packageRootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Descriptor '$DescriptorPath' PreservePaths entry '$preservePath' escapes the package root."
            }

            $normalizedPreservePaths += $preservePath
        }
    }

    $formatVersion = if ($Descriptor.ContainsKey('FormatVersion')) { [string]$Descriptor['FormatVersion'] } else { $null }

    if (-not [string]::IsNullOrWhiteSpace($formatVersion)) {
        if (-not [string]::Equals($formatVersion.Trim(), '1.0', [System.StringComparison]::Ordinal)) {
            throw "Descriptor '$DescriptorPath' has unsupported FormatVersion '$formatVersion'. Expected '1.0'."
        }

        if (-not $Descriptor.ContainsKey('EntryPoint') -or [string]::IsNullOrWhiteSpace([string]$Descriptor['EntryPoint'])) {
            throw "Descriptor '$DescriptorPath' is missing required key 'EntryPoint'."
        }

        if (-not $Descriptor.ContainsKey('Description') -or [string]::IsNullOrWhiteSpace([string]$Descriptor['Description'])) {
            throw "Descriptor '$DescriptorPath' is missing required key 'Description'."
        }

        $entryPoint = [string]$Descriptor['EntryPoint']
        if ([System.IO.Path]::IsPathRooted($entryPoint)) {
            throw "Descriptor '$DescriptorPath' EntryPoint must be a relative path."
        }

        $entryPointFullPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($packageRootFullPath, $entryPoint))
        if (-not $entryPointFullPath.StartsWith($packageRootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Descriptor '$DescriptorPath' EntryPoint escapes the package root."
        }

        if (-not (Test-Path -LiteralPath $entryPointFullPath -PathType Leaf)) {
            throw "EntryPoint file '$entryPoint' was not found under '$PackageRoot'."
        }

        if ($Descriptor.ContainsKey('Version') -and -not [string]::IsNullOrWhiteSpace([string]$Descriptor['Version'])) {
            $parsedVersion = $null
            if (-not [version]::TryParse([string]$Descriptor['Version'], [ref]$parsedVersion)) {
                throw "Descriptor '$DescriptorPath' has invalid Version value '$($Descriptor['Version'])'."
            }
        }

        return [pscustomobject]@{
            Name = [string]$Descriptor['Name']
            FormatVersion = '1.0'
            EntryPoint = [string]$Descriptor['EntryPoint']
            Description = [string]$Descriptor['Description']
            Version = if ($Descriptor.ContainsKey('Version')) { [string]$Descriptor['Version'] } else { $null }
            ServiceLogPath = if ($Descriptor.ContainsKey('ServiceLogPath')) { [string]$Descriptor['ServiceLogPath'] } else { $null }
            PreservePaths = $normalizedPreservePaths
        }
    }

    foreach ($requiredKey in @('Description', 'Version')) {
        if (-not $Descriptor.ContainsKey($requiredKey) -or [string]::IsNullOrWhiteSpace([string]$Descriptor[$requiredKey])) {
            throw "Descriptor '$DescriptorPath' is missing required key '$requiredKey'."
        }
    }

    $legacyVersion = $null
    if (-not [version]::TryParse([string]$Descriptor['Version'], [ref]$legacyVersion)) {
        throw "Descriptor '$DescriptorPath' has invalid Version value '$($Descriptor['Version'])'."
    }

    $legacyScript = if ($Descriptor.ContainsKey('Script') -and -not [string]::IsNullOrWhiteSpace([string]$Descriptor['Script'])) {
        [string]$Descriptor['Script']
    } else {
        'server.ps1'
    }

    if ([System.IO.Path]::IsPathRooted($legacyScript)) {
        throw "Descriptor '$DescriptorPath' Script must be a relative path."
    }

    $legacyScriptFullPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($packageRootFullPath, $legacyScript))
    if (-not $legacyScriptFullPath.StartsWith($packageRootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Descriptor '$DescriptorPath' Script escapes the package root."
    }

    if (-not (Test-Path -LiteralPath $legacyScriptFullPath -PathType Leaf)) {
        throw "Script file '$legacyScript' was not found under '$PackageRoot'."
    }

    [pscustomobject]@{
        Name = [string]$Descriptor['Name']
        FormatVersion = 'legacy'
        EntryPoint = $legacyScript
        Description = [string]$Descriptor['Description']
        Version = $legacyVersion.ToString()
        ServiceLogPath = if ($Descriptor.ContainsKey('ServiceLogPath')) { [string]$Descriptor['ServiceLogPath'] } else { $null }
        PreservePaths = $normalizedPreservePaths
    }
}
