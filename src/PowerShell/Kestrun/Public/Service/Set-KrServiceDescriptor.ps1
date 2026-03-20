<#
.SYNOPSIS
    Updates a Service.psd1 descriptor file.
.DESCRIPTION
    Updates Description, Version, Script, ServiceLogPath, and PreservePaths values in Service.psd1.
    Name is immutable and cannot be changed by this cmdlet.
.PARAMETER Path
    Descriptor path. Defaults to Service.psd1 in the current directory.
.PARAMETER Description
    New description value.
.PARAMETER Version
    New version value compatible with System.Version.
.PARAMETER Script
    New script path value.
.PARAMETER ServiceLogPath
    New default service log path.
.PARAMETER ClearScript
    Removes Script from the descriptor.
.PARAMETER ClearServiceLogPath
    Removes ServiceLogPath from the descriptor.
.PARAMETER PreservePaths
    Replaces PreservePaths with a new list of relative file/folder paths.
.PARAMETER ClearPreservePaths
    Removes PreservePaths from the descriptor.
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
        [string]$Script,

        [Parameter()]
        [string]$ServiceLogPath,

        [Parameter()]
        [string[]]$PreservePaths,

        [Parameter()]
        [switch]$ClearScript,

        [Parameter()]
        [switch]$ClearServiceLogPath,

        [Parameter()]
        [switch]$ClearPreservePaths
    )

    $updateRequested = $PSBoundParameters.ContainsKey('Description') -or
    $PSBoundParameters.ContainsKey('Version') -or
    $PSBoundParameters.ContainsKey('Script') -or
    $PSBoundParameters.ContainsKey('ServiceLogPath') -or
    $PSBoundParameters.ContainsKey('PreservePaths') -or
    $ClearScript -or
    $ClearServiceLogPath -or
    $ClearPreservePaths

    if (-not $updateRequested) {
        throw 'No updates requested. Specify one or more updatable fields.'
    }

    if ($PSBoundParameters.ContainsKey('Script') -and $ClearScript) {
        throw 'Cannot use -Script and -ClearScript together.'
    }

    if ($PSBoundParameters.ContainsKey('ServiceLogPath') -and $ClearServiceLogPath) {
        throw 'Cannot use -ServiceLogPath and -ClearServiceLogPath together.'
    }

    if ($PSBoundParameters.ContainsKey('PreservePaths') -and $ClearPreservePaths) {
        throw 'Cannot use -PreservePaths and -ClearPreservePaths together.'
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
    $nextScript = if ($ClearScript) { $null } elseif ($PSBoundParameters.ContainsKey('Script')) { $Script } else { $current.Script }
    $nextServiceLogPath = if ($ClearServiceLogPath) { $null } elseif ($PSBoundParameters.ContainsKey('ServiceLogPath')) { $ServiceLogPath } else { $current.ServiceLogPath }
    $nextPreservePaths = if ($ClearPreservePaths) { @() } elseif ($PSBoundParameters.ContainsKey('PreservePaths')) { @($PreservePaths) } else { @($current.PreservePaths) }

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

    $escapedName = $current.Name.Replace("'", "''")
    $escapedDescription = $nextDescription.Replace("'", "''")
    $escapedVersion = $nextVersion.Replace("'", "''")

    $contentLines = [System.Collections.Generic.List[string]]::new()
    $contentLines.Add('@{')
    $contentLines.Add("    Name = '$escapedName'")
    $contentLines.Add("    Description = '$escapedDescription'")
    $contentLines.Add("    Version = '$escapedVersion'")

    if (-not [string]::IsNullOrWhiteSpace($nextScript)) {
        $escapedScript = $nextScript.Replace("'", "''")
        $contentLines.Add("    Script = '$escapedScript'")
    }

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

    $contentLines.Add('}')

    if ($PSCmdlet.ShouldProcess($current.Path, 'Update Service.psd1 descriptor (Name is immutable)')) {
        Set-Content -LiteralPath $current.Path -Value ($contentLines -join [Environment]::NewLine) -Encoding utf8NoBOM
    }

    Get-KrServiceDescriptor -Path $current.Path
}
