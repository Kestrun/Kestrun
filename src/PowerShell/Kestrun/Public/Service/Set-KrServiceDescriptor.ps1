<#
.SYNOPSIS
    Updates a Service.psd1 descriptor file.
.DESCRIPTION
    Updates Description, Version, EntryPoint, ServiceLogPath, PreservePaths, and ApplicationDataFolders values in Service.psd1.
    Name is immutable and cannot be changed by this cmdlet.
.PARAMETER Path
    Descriptor path. Accepts either a descriptor file path or a directory path.
    When a directory path is provided, Service.psd1 is appended automatically.
.PARAMETER Description
    New description value.
.PARAMETER Version
    New version value compatible with System.Version.
.PARAMETER EntryPoint
    New entry point path value.
.PARAMETER ServiceLogPath
    New default service log path.
.PARAMETER ClearServiceLogPath
    Removes ServiceLogPath from the descriptor.
.PARAMETER PreservePaths
    Replaces PreservePaths with a new list of relative file/folder paths.
.PARAMETER ClearPreservePaths
    Removes PreservePaths from the descriptor.
.PARAMETER ApplicationDataFolders
    Replaces ApplicationDataFolders with a new list of relative application-data folder paths.
.PARAMETER ClearApplicationDataFolders
    Removes ApplicationDataFolders from the descriptor.
.PARAMETER WhatIf
    Shows what would happen if the cmdlet runs. The cmdlet is not executed.
.PARAMETER Confirm
    Prompts for confirmation before running the cmdlet.
.EXAMPLE
    Set-KrServiceDescriptor -Path .\Service.psd1 -Description 'Updated' -Version 1.2.1
#>
function Set-KrServiceDescriptor {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([pscustomobject])]
    param(
        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]$Path = 'Service.psd1',

        [Parameter()]
        [string]$Description,

        [Parameter()]
        [version]$Version,

        [Parameter()]
        [string]$EntryPoint,

        [Parameter()]
        [string]$ServiceLogPath,

        [Parameter()]
        [string[]]$PreservePaths,

        [Parameter()]
        [string[]]$ApplicationDataFolders,

        [Parameter()]
        [switch]$ClearServiceLogPath,

        [Parameter()]
        [switch]$ClearPreservePaths,

        [Parameter()]
        [switch]$ClearApplicationDataFolders
    )

    $updateRequested = $PSBoundParameters.ContainsKey('Description') -or
    $PSBoundParameters.ContainsKey('Version') -or
    $PSBoundParameters.ContainsKey('EntryPoint') -or
    $PSBoundParameters.ContainsKey('ServiceLogPath') -or
    $PSBoundParameters.ContainsKey('PreservePaths') -or
    $PSBoundParameters.ContainsKey('ApplicationDataFolders') -or
    $ClearServiceLogPath -or
    $ClearPreservePaths -or
    $ClearApplicationDataFolders

    if (-not $updateRequested) {
        throw 'No updates requested. Specify one or more updatable fields.'
    }

    if ($PSBoundParameters.ContainsKey('ServiceLogPath') -and $ClearServiceLogPath) {
        throw 'Cannot use -ServiceLogPath and -ClearServiceLogPath together.'
    }

    if ($PSBoundParameters.ContainsKey('PreservePaths') -and $ClearPreservePaths) {
        throw 'Cannot use -PreservePaths and -ClearPreservePaths together.'
    }

    if ($PSBoundParameters.ContainsKey('ApplicationDataFolders') -and $ClearApplicationDataFolders) {
        throw 'Cannot use -ApplicationDataFolders and -ClearApplicationDataFolders together.'
    }

    $current = Get-KrServiceDescriptor -Path $Path

    $nextDescription = if ($PSBoundParameters.ContainsKey('Description')) { $Description } else { $current.Description }
    $nextVersion = if ($PSBoundParameters.ContainsKey('Version')) {
        if ($null -eq $Version) {
            $null
        } else {
            $Version.ToString()
        }
    } else {
        $current.Version
    }
    $nextEntryPoint = if ($PSBoundParameters.ContainsKey('EntryPoint')) { $EntryPoint } else { $current.EntryPoint }
    $nextServiceLogPath = if ($ClearServiceLogPath) { $null } elseif ($PSBoundParameters.ContainsKey('ServiceLogPath')) { $ServiceLogPath } else { $current.ServiceLogPath }
    $nextPreservePaths = if ($ClearPreservePaths) { @() } elseif ($PSBoundParameters.ContainsKey('PreservePaths')) { @($PreservePaths) } else { @($current.PreservePaths) }
    $nextApplicationDataFolders = if ($ClearApplicationDataFolders) { @() } elseif ($PSBoundParameters.ContainsKey('ApplicationDataFolders')) { @($ApplicationDataFolders) } else { @($current.ApplicationDataFolders) }

    if ([string]::IsNullOrWhiteSpace($nextDescription)) {
        if ($PSBoundParameters.ContainsKey('Description')) {
            throw 'Parameter -Description cannot be null or empty.'
        }

        throw 'Service descriptor is missing a valid Description. Update the descriptor or pass -Description with a non-empty value.'
    }

    if ([string]::IsNullOrWhiteSpace($nextVersion)) {
        if ($PSBoundParameters.ContainsKey('Version')) {
            throw 'Parameter -Version cannot be null or empty and must be compatible with [System.Version].'
        }

        throw 'Service descriptor is missing a valid Version. Pass -Version with a value compatible with [System.Version].'
    }

    try {
        [void][version]$nextVersion
    } catch {
        if ($PSBoundParameters.ContainsKey('Version')) {
            throw 'Parameter -Version cannot be null or empty and must be compatible with [System.Version].'
        }

        throw 'Service descriptor is missing a valid Version. Pass -Version with a value compatible with [System.Version].'
    }

    if ([string]::IsNullOrWhiteSpace($nextEntryPoint)) {
        if ($PSBoundParameters.ContainsKey('EntryPoint')) {
            throw 'Parameter -EntryPoint cannot be null or empty.'
        }

        throw 'Service descriptor is missing a valid EntryPoint. Update the descriptor or pass -EntryPoint with a non-empty value.'
    }

    if ([System.IO.Path]::IsPathRooted($nextEntryPoint)) {
        throw "EntryPoint must be a relative path under the descriptor directory: $nextEntryPoint"
    }

    $descriptorDirectory = [System.IO.Path]::GetDirectoryName($current.Path)
    $entryPointPath = [System.IO.Path]::GetFullPath((Join-Path -Path $descriptorDirectory -ChildPath $nextEntryPoint))
    $descriptorRootWithSeparator = if ($descriptorDirectory.EndsWith([System.IO.Path]::DirectorySeparatorChar)) { $descriptorDirectory } else { $descriptorDirectory + [System.IO.Path]::DirectorySeparatorChar }
    if (-not $entryPointPath.StartsWith($descriptorRootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "EntryPoint must resolve to a file under descriptor path: $nextEntryPoint"
    }

    if (-not (Test-Path -LiteralPath $entryPointPath -PathType Leaf)) {
        throw "EntryPoint file not found under descriptor path: $nextEntryPoint"
    }

    $escapedName = $current.Name.Replace("'", "''")
    $escapedDescription = $nextDescription.Replace("'", "''")
    $escapedVersion = $nextVersion.Replace("'", "''")

    $contentLines = [System.Collections.Generic.List[string]]::new()
    $contentLines.Add('@{')
    $contentLines.Add("    FormatVersion = '1.0'")
    $contentLines.Add("    Name = '$escapedName'")
    $contentLines.Add("    Description = '$escapedDescription'")
    $contentLines.Add("    Version = '$escapedVersion'")

    $escapedEntryPoint = $nextEntryPoint.Replace("'", "''")
    $contentLines.Add("    EntryPoint = '$escapedEntryPoint'")

    if (-not [string]::IsNullOrWhiteSpace($nextServiceLogPath)) {
        $escapedServiceLogPath = $nextServiceLogPath.Replace("'", "''")
        $contentLines.Add("    ServiceLogPath = '$escapedServiceLogPath'")
    }

    if ($null -ne $nextPreservePaths -and $nextPreservePaths.Count -gt 0) {
        $contentLines.Add('    PreservePaths = @(')
        foreach ($preservePath in $nextPreservePaths) {
            if ([string]::IsNullOrWhiteSpace($preservePath)) {
                continue
            }

            $escapedPreservePath = $preservePath.Replace("'", "''")
            $contentLines.Add("        '$escapedPreservePath'")
        }

        $contentLines.Add('    )')
    }

    if ($null -ne $nextApplicationDataFolders -and $nextApplicationDataFolders.Count -gt 0) {
        $contentLines.Add('    ApplicationDataFolders = @(')
        foreach ($applicationDataFolder in $nextApplicationDataFolders) {
            if ([string]::IsNullOrWhiteSpace($applicationDataFolder)) {
                continue
            }

            $escapedApplicationDataFolder = $applicationDataFolder.Replace("'", "''")
            $contentLines.Add("        '$escapedApplicationDataFolder'")
        }

        $contentLines.Add('    )')
    }

    $contentLines.Add('}')

    if ($PSCmdlet.ShouldProcess($current.Path, 'Update Service.psd1 descriptor (Name is immutable)')) {
        Set-Content -LiteralPath $current.Path -Value ($contentLines -join [Environment]::NewLine) -Encoding utf8NoBOM
    }

    Get-KrServiceDescriptor -Path $current.Path
}
