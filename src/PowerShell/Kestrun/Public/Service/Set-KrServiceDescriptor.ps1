<#
.SYNOPSIS
    Updates a Service.psd1 descriptor file.
.DESCRIPTION
    Updates Description, Version, Script, and ServiceLogPath values in Service.psd1.
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
        [switch]$ClearScript,

        [Parameter()]
        [switch]$ClearServiceLogPath
    )

    $updateRequested = $PSBoundParameters.ContainsKey('Description') -or
    $PSBoundParameters.ContainsKey('Version') -or
    $PSBoundParameters.ContainsKey('Script') -or
    $PSBoundParameters.ContainsKey('ServiceLogPath') -or
    $ClearScript -or
    $ClearServiceLogPath

    if (-not $updateRequested) {
        throw 'No updates requested. Specify one or more updatable fields.'
    }

    if ($PSBoundParameters.ContainsKey('Script') -and $ClearScript) {
        throw 'Cannot use -Script and -ClearScript together.'
    }

    if ($PSBoundParameters.ContainsKey('ServiceLogPath') -and $ClearServiceLogPath) {
        throw 'Cannot use -ServiceLogPath and -ClearServiceLogPath together.'
    }

    $current = Get-KrServiceDescriptor -Path $Path

    $nextDescription = if ($PSBoundParameters.ContainsKey('Description')) { $Description } else { $current.Description }
    $nextVersion = if ($PSBoundParameters.ContainsKey('Version')) { $Version.ToString() } else { $current.Version }
    $nextScript = if ($ClearScript) { $null } elseif ($PSBoundParameters.ContainsKey('Script')) { $Script } else { $current.Script }
    $nextServiceLogPath = if ($ClearServiceLogPath) { $null } elseif ($PSBoundParameters.ContainsKey('ServiceLogPath')) { $ServiceLogPath } else { $current.ServiceLogPath }

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

    $contentLines.Add('}')

    if ($PSCmdlet.ShouldProcess($current.Path, 'Update Service.psd1 descriptor (Name is immutable)')) {
        Set-Content -LiteralPath $current.Path -Value ($contentLines -join [Environment]::NewLine) -Encoding utf8NoBOM
    }

    Get-KrServiceDescriptor -Path $current.Path
}
