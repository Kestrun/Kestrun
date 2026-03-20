<#
.SYNOPSIS
    Creates a Service.psd1 descriptor file.
.DESCRIPTION
    Creates a Service.psd1 descriptor file used by Kestrun.Tool content-root service install flow.
    Required keys are Name, Description, and Version. Optional keys are Script, ServiceLogPath, and PreservePaths.
.PARAMETER Path
    Target descriptor path. Defaults to Service.psd1 in the current directory.
.PARAMETER Name
    Immutable service name value written to the descriptor.
.PARAMETER Description
    Service description value.
.PARAMETER Version
    Service version. Must be compatible with System.Version.
.PARAMETER Script
    Optional script path relative to the content root.
.PARAMETER ServiceLogPath
    Optional default service log path.
.PARAMETER PreservePaths
    Optional list of relative file/folder paths that must be preserved during service update.
.PARAMETER Force
    Overwrite an existing descriptor file.
.EXAMPLE
    New-KrServiceDescriptor -Name demo -Description 'Demo service' -Version 1.2.0
#>
function New-KrServiceDescriptor {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([pscustomobject])]
    param(
        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]$Path = 'Service.psd1',

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Description,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [version]$Version,

        [Parameter()]
        [string]$Script,

        [Parameter()]
        [string]$ServiceLogPath,

        [Parameter()]
        [string[]]$PreservePaths,

        [Parameter()]
        [switch]$Force
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if ((Test-Path -LiteralPath $fullPath) -and -not $Force) {
        throw "Descriptor file already exists: $fullPath. Use -Force to overwrite."
    }

    $directory = [System.IO.Path]::GetDirectoryName($fullPath)
    if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path -LiteralPath $directory)) {
        $null = New-Item -ItemType Directory -Path $directory -Force
    }

    $escapedName = $Name.Replace("'", "''")
    $escapedDescription = $Description.Replace("'", "''")
    $escapedVersion = $Version.ToString().Replace("'", "''")

    $contentLines = [System.Collections.Generic.List[string]]::new()
    $contentLines.Add('@{')
    $contentLines.Add("    Name = '$escapedName'")
    $contentLines.Add("    Description = '$escapedDescription'")
    $contentLines.Add("    Version = '$escapedVersion'")

    if (-not [string]::IsNullOrWhiteSpace($Script)) {
        $escapedScript = $Script.Replace("'", "''")
        $contentLines.Add("    Script = '$escapedScript'")
    } else {
        $contentLines.Add("    Script = 'Service.ps1'")
    }

    if (-not [string]::IsNullOrWhiteSpace($ServiceLogPath)) {
        $escapedServiceLogPath = $ServiceLogPath.Replace("'", "''")
        $contentLines.Add("    ServiceLogPath = '$escapedServiceLogPath'")
    }

    if ($null -ne $PreservePaths -and $PreservePaths.Count -gt 0) {
        $contentLines.Add('    PreservePaths = @(')
        foreach ($preservePath in $PreservePaths) {
            if ([string]::IsNullOrWhiteSpace($preservePath)) {
                continue
            }

            $escapedPreservePath = $preservePath.Replace("'", "''")
            $contentLines.Add("        '$escapedPreservePath'")
        }

        $contentLines.Add('    )')
    }

    $contentLines.Add('}')

    if ($PSCmdlet.ShouldProcess($fullPath, 'Create Service.psd1 descriptor')) {
        Set-Content -LiteralPath $fullPath -Value ($contentLines -join [Environment]::NewLine) -Encoding utf8NoBOM
    }

    Get-KrServiceDescriptor -Path $fullPath
}
