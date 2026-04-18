<#
.SYNOPSIS
    Creates a Kestrun service package (.krpack).
.DESCRIPTION
    Creates a .krpack archive from either:
    - A source folder that already contains Service.psd1 (validated before packaging), or
    - A script file plus Name/Version metadata (a Service.psd1 descriptor is generated automatically).

    For generated descriptors, FormatVersion is set to '1.0' and EntryPoint is set to the script file name.
.PARAMETER SourceFolder
    Folder to package. Must contain a valid Service.psd1 descriptor.
.PARAMETER ScriptPath
    Script file to package. A Service.psd1 descriptor is generated automatically.
.PARAMETER Name
    Service name used when generating Service.psd1 from ScriptPath.
    If omitted, defaults to the script filename without extension.
.PARAMETER Version
    Service version used when generating Service.psd1 from ScriptPath.
.PARAMETER Description
    Optional description used when generating Service.psd1 from ScriptPath.
    Defaults to Name.
.PARAMETER ServiceLogPath
    Optional ServiceLogPath written to generated Service.psd1.
.PARAMETER PreservePaths
    Optional relative file/folder paths to preserve during service update.
.PARAMETER ApplicationDataFolders
    Optional relative application-data folders to preserve during service update.
.PARAMETER ExcludeApplicationDataFolders
    In SourceFolder mode, excludes files under descriptor ApplicationDataFolders from the package archive.
    The descriptor values are kept unchanged so those folders can still be preserved during service update.
.PARAMETER ExcludePaths
    In SourceFolder mode, excludes specific relative files or folders from the package archive.
    Paths must stay under SourceFolder and cannot exclude Service.psd1 or the EntryPoint file.
.PARAMETER OutputPath
    Output .krpack path.
    Defaults:
    - SourceFolder mode: <SourceFolderName>.krpack in current directory
    - ScriptPath mode: <Name>-<Version>.krpack in current directory
.PARAMETER Force
    Overwrite an existing output file.
.PARAMETER WhatIf
    Shows what would happen if the cmdlet runs. The cmdlet is not executed.
.PARAMETER Confirm
    Prompts for confirmation before running the cmdlet.
.EXAMPLE
    New-KrServicePackage -SourceFolder .\my-service -OutputPath .\my-service.krpack
.EXAMPLE
    New-KrServicePackage -ScriptPath .\server.ps1 -Name demo -Version 1.2.0 -OutputPath .\demo.krpack
.EXAMPLE
    New-KrServicePackage -ScriptPath .\server.ps1 -Version 1.2.0
.EXAMPLE
    New-KrServicePackage -SourceFolder .\my-service -ExcludeApplicationDataFolders -ExcludePaths @('secrets/dev.json', 'scratch/') -OutputPath .\my-service.krpack
#>
function New-KrServicePackage {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'FromFolder', SupportsShouldProcess)]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory, ParameterSetName = 'FromFolder')]
        [ValidateNotNullOrEmpty()]
        [string]$SourceFolder,

        [Parameter(Mandatory, ParameterSetName = 'FromScript')]
        [ValidateNotNullOrEmpty()]
        [string]$ScriptPath,

        [Parameter(ParameterSetName = 'FromScript')]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory, ParameterSetName = 'FromScript')]
        [ValidateNotNullOrEmpty()]
        [version]$Version,

        [Parameter(ParameterSetName = 'FromScript')]
        [string]$Description,

        [Parameter(ParameterSetName = 'FromScript')]
        [string]$ServiceLogPath,

        [Parameter(ParameterSetName = 'FromScript')]
        [string[]]$PreservePaths,

        [Parameter(ParameterSetName = 'FromScript')]
        [string[]]$ApplicationDataFolders,

        [Parameter(ParameterSetName = 'FromFolder')]
        [switch]$ExcludeApplicationDataFolders,

        [Parameter(ParameterSetName = 'FromFolder')]
        [string[]]$ExcludePaths,

        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]$OutputPath,

        [Parameter()]
        [switch]$Force
    )

    <#
    .SYNOPSIS
        Resolves the output path for the .krpack file.
    .DESCRIPTION
        Determines the full path for the output .krpack file based on the provided path or a default base name.
    .PARAMETER ProvidedOutputPath
        The user-provided output path.
    .PARAMETER DefaultBaseName
        The default base name to use if no output path is provided.
    .EXAMPLE
        Get-KrResolvedOutputPath -ProvidedOutputPath .\output.krpack -DefaultBaseName demo
    .EXAMPLE
        Get-KrResolvedOutputPath -ProvidedOutputPath '' -DefaultBaseName demo
    .EXAMPLE
        Get-KrResolvedOutputPath -ProvidedOutputPath $null -DefaultBaseName demo
    .OUTPUTS
        [string] The resolved full path for the .krpack file.
    #>
    function Get-KrResolvedOutputPath {
        param(
            [string]$ProvidedOutputPath,
            [string]$DefaultBaseName
        )

        $resolved = if ([string]::IsNullOrWhiteSpace($ProvidedOutputPath)) {
            [System.IO.Path]::Combine((Get-Location).Path, "$DefaultBaseName.krpack")
        } else {
            [System.IO.Path]::GetFullPath($ProvidedOutputPath)
        }

        if (-not $resolved.EndsWith('.krpack', [System.StringComparison]::OrdinalIgnoreCase)) {
            $resolved = "$resolved.krpack"
        }

        return $resolved
    }

    <#
    .SYNOPSIS
        Validates and resolves a relative package path.
    .DESCRIPTION
        Resolves a relative path under the package root and optionally requires that the path exists.
    .PARAMETER PackageRoot
        The package root directory.
    .PARAMETER RelativePath
        The relative file or folder path to validate.
    .PARAMETER PathLabel
        Label used in error messages.
    .PARAMETER RequireExisting
        Requires the path to exist under the package root.
    .PARAMETER RejectPackageRoot
        Rejects paths that resolve to the package root itself.
    .OUTPUTS
        [pscustomobject] with FullPath, RelativePath, and IsDirectory properties.
    #>
    function Get-KrPackagePathInfo {
        param(
            [string]$PackageRoot,
            [string]$RelativePath,
            [string]$PathLabel,
            [switch]$RequireExisting,
            [switch]$RejectPackageRoot
        )

        if ([string]::IsNullOrWhiteSpace($RelativePath)) {
            throw "$PathLabel cannot contain empty values."
        }

        if ([System.IO.Path]::IsPathRooted($RelativePath)) {
            throw "$PathLabel entry '$RelativePath' must be a relative path."
        }

        $packageRootFullPath = [System.IO.Path]::GetFullPath($PackageRoot)
        $packageRootNormalized = [System.IO.Path]::TrimEndingDirectorySeparator($packageRootFullPath)
        $combinedPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($packageRootFullPath, $RelativePath))
        $relativeToRoot = [System.IO.Path]::GetRelativePath($packageRootNormalized, $combinedPath)

        if ([System.IO.Path]::IsPathRooted($relativeToRoot) -or
            [string]::Equals($relativeToRoot, '..', [System.StringComparison]::Ordinal) -or
            $relativeToRoot.StartsWith("..$([System.IO.Path]::DirectorySeparatorChar)", [System.StringComparison]::Ordinal) -or
            $relativeToRoot.StartsWith("..$([System.IO.Path]::AltDirectorySeparatorChar)", [System.StringComparison]::Ordinal)) {
            throw "$PathLabel entry '$RelativePath' escapes the package root."
        }

        if ($RejectPackageRoot -and [string]::Equals($relativeToRoot, '.', [System.StringComparison]::Ordinal)) {
            throw "$PathLabel entry '$RelativePath' resolves to the package root and cannot be excluded."
        }

        $pathExists = Test-Path -LiteralPath $combinedPath
        if ($RequireExisting -and -not $pathExists) {
            throw "$PathLabel entry '$RelativePath' was not found under '$PackageRoot'."
        }

        $isDirectory = if ($pathExists) {
            Test-Path -LiteralPath $combinedPath -PathType Container
        } else {
            $RelativePath.EndsWith([System.IO.Path]::DirectorySeparatorChar) -or
            $RelativePath.EndsWith([System.IO.Path]::AltDirectorySeparatorChar)
        }

        [pscustomobject]@{
            FullPath = $combinedPath
            RelativePath = $relativeToRoot -replace '\\', '/'
            IsDirectory = $isDirectory
        }
    }

    <#
    .SYNOPSIS
        Tests whether a path is covered by package exclusions.
    .DESCRIPTION
        Matches both exact file exclusions and directory exclusions that cover descendant files.
    .PARAMETER Path
        The file path to test.
    .PARAMETER ExcludedEntries
        Exclusion entries returned by Get-KrPackagePathInfo.
    .OUTPUTS
        [bool] True when the path should be excluded.
    #>
    function Test-KrPathIsExcluded {
        param(
            [string]$Path,
            [object[]]$ExcludedEntries
        )

        if ($null -eq $ExcludedEntries -or $ExcludedEntries.Count -eq 0) {
            return $false
        }

        $candidatePath = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($Path))
        foreach ($entry in $ExcludedEntries) {
            $excludedPath = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($entry.FullPath))
            $relativeToExcluded = [System.IO.Path]::GetRelativePath($excludedPath, $candidatePath)

            if ($entry.IsDirectory) {
                if ([string]::Equals($relativeToExcluded, '.', [System.StringComparison]::Ordinal)) {
                    return $true
                }

                if (-not [System.IO.Path]::IsPathRooted($relativeToExcluded) -and
                    -not [string]::Equals($relativeToExcluded, '..', [System.StringComparison]::Ordinal) -and
                    -not $relativeToExcluded.StartsWith("..$([System.IO.Path]::DirectorySeparatorChar)", [System.StringComparison]::Ordinal) -and
                    -not $relativeToExcluded.StartsWith("..$([System.IO.Path]::AltDirectorySeparatorChar)", [System.StringComparison]::Ordinal)) {
                    return $true
                }

                continue
            }

            if ([string]::Equals($candidatePath, $excludedPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                return $true
            }
        }

        return $false
    }

    <#
    .SYNOPSIS
        Builds normalized package exclusion entries.
    .DESCRIPTION
        Combines ApplicationDataFolders and explicit ExcludePaths into a deduplicated exclusion list.
    .PARAMETER PackageRoot
        The package root directory.
    .PARAMETER DescriptorInfo
        The validated descriptor info object.
    .PARAMETER DescriptorPath
        The Service.psd1 path.
    .PARAMETER ExcludeApplicationDataFolders
        Excludes descriptor ApplicationDataFolders from the archive.
    .PARAMETER ExcludePaths
        Additional relative file or folder paths to exclude.
    .OUTPUTS
        An array of exclusion entry objects.
    #>
    function Get-KrPackageExclusionEntries {
        param(
            [string]$PackageRoot,
            [pscustomobject]$DescriptorInfo,
            [string]$DescriptorPath,
            [bool]$ExcludeApplicationDataFolders,
            [string[]]$ExcludePaths
        )

        $entries = [System.Collections.Generic.List[object]]::new()
        $seenKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

        $addEntry = {
            param([pscustomobject]$Entry)

            $entryKey = '{0}|{1}' -f ([System.IO.Path]::TrimEndingDirectorySeparator($Entry.FullPath)), $Entry.IsDirectory
            if ($seenKeys.Add($entryKey)) {
                $entries.Add($Entry)
            }
        }

        if ($ExcludeApplicationDataFolders -and $null -ne $DescriptorInfo.ApplicationDataFolders) {
            foreach ($applicationDataFolder in @($DescriptorInfo.ApplicationDataFolders)) {
                if ([string]::IsNullOrWhiteSpace($applicationDataFolder)) {
                    continue
                }

                & $addEntry (Get-KrPackagePathInfo -PackageRoot $PackageRoot -RelativePath $applicationDataFolder -PathLabel 'ApplicationDataFolders' -RejectPackageRoot)
            }
        }

        if ($null -ne $ExcludePaths) {
            foreach ($excludePath in @($ExcludePaths)) {
                if ([string]::IsNullOrWhiteSpace($excludePath)) {
                    continue
                }

                & $addEntry (Get-KrPackagePathInfo -PackageRoot $PackageRoot -RelativePath $excludePath -PathLabel 'ExcludePaths' -RequireExisting -RejectPackageRoot)
            }
        }

        if ($entries.Count -eq 0) {
            return @()
        }

        if (Test-KrPathIsExcluded -Path $DescriptorPath -ExcludedEntries $entries) {
            throw 'Requested package exclusions cannot exclude Service.psd1 because the service descriptor is required.'
        }

        $entryPointPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PackageRoot, $DescriptorInfo.EntryPoint))
        if (Test-KrPathIsExcluded -Path $entryPointPath -ExcludedEntries $entries) {
            throw "Requested package exclusions cannot exclude the EntryPoint '$($DescriptorInfo.EntryPoint)'."
        }

        return $entries.ToArray()
    }

    <#
    .SYNOPSIS
        Creates a zip archive from a directory.
    .DESCRIPTION
        Compresses the contents of a directory into a .krpack file.
    .PARAMETER DirectoryPath
        The path of the directory to compress.
    .PARAMETER DestinationPath
        The path of the resulting .krpack file.
    .EXAMPLE
        Invoke-KrZipFromDirectory -DirectoryPath .\my-service -DestinationPath .\my-service.krpack
    .OUTPUTS
        None. Creates a .krpack file at the specified destination.
    #>
    function Invoke-KrZipFromDirectory {
        param(
            [string]$DirectoryPath,
            [string]$DestinationPath,
            [object[]]$ExcludedEntries = @()
        )

        Add-Type -AssemblyName System.IO.Compression
        Add-Type -AssemblyName System.IO.Compression.FileSystem

        $destinationDirectory = [System.IO.Path]::GetDirectoryName($DestinationPath)
        if (-not [string]::IsNullOrWhiteSpace($destinationDirectory) -and -not (Test-Path -LiteralPath $destinationDirectory)) {
            $null = New-Item -ItemType Directory -Path $destinationDirectory -Force
        }

        if (Test-Path -LiteralPath $DestinationPath -PathType Leaf) {
            Remove-Item -LiteralPath $DestinationPath -Force
        }

        $zip = [System.IO.Compression.ZipFile]::Open($DestinationPath, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($file in (Get-ChildItem -LiteralPath $DirectoryPath -File -Recurse -Force)) {
                if (Test-KrPathIsExcluded -Path $file.FullName -ExcludedEntries $ExcludedEntries) {
                    continue
                }

                $relativePath = [System.IO.Path]::GetRelativePath($DirectoryPath, $file.FullName)
                $normalizedRelativePath = $relativePath -replace '\\', '/'
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $normalizedRelativePath, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }
        } finally {
            $zip.Dispose()
        }
    }

    $stagingRoot = $null
    $packageRoot = $null
    $descriptorInfo = $null
    $packageExclusions = @()

    try {
        if ($PSCmdlet.ParameterSetName -eq 'FromFolder') {
            $packageRoot = [System.IO.Path]::GetFullPath($SourceFolder)
            if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
                throw "Source folder not found: $packageRoot"
            }

            $descriptorPath = [System.IO.Path]::Combine($packageRoot, 'Service.psd1')
            if (-not (Test-Path -LiteralPath $descriptorPath -PathType Leaf)) {
                throw "Service descriptor not found: $descriptorPath"
            }

            $descriptorData = Import-PowerShellDataFile -LiteralPath $descriptorPath
            $descriptorInfo = Test-KrServiceDescriptorData -Descriptor $descriptorData -DescriptorPath $descriptorPath -PackageRoot $packageRoot
            $packageExclusions = Get-KrPackageExclusionEntries -PackageRoot $packageRoot -DescriptorInfo $descriptorInfo -DescriptorPath $descriptorPath -ExcludeApplicationDataFolders $ExcludeApplicationDataFolders.IsPresent -ExcludePaths $ExcludePaths

            $defaultBaseName = [System.IO.Path]::GetFileName($packageRoot)
            $resolvedOutputPath = Get-KrResolvedOutputPath -ProvidedOutputPath $OutputPath -DefaultBaseName $defaultBaseName
        } else {
            $resolvedScriptPath = [System.IO.Path]::GetFullPath($ScriptPath)
            if (-not (Test-Path -LiteralPath $resolvedScriptPath -PathType Leaf)) {
                throw "Script file not found: $resolvedScriptPath"
            }

            if (-not $resolvedScriptPath.EndsWith('.ps1', [System.StringComparison]::OrdinalIgnoreCase)) {
                throw 'ScriptPath must point to a .ps1 file.'
            }

            $scriptFileName = [System.IO.Path]::GetFileName($resolvedScriptPath)
            $effectiveName = if ([string]::IsNullOrWhiteSpace($Name)) { [System.IO.Path]::GetFileNameWithoutExtension($resolvedScriptPath) } else { $Name }
            $effectiveDescription = if ([string]::IsNullOrWhiteSpace($Description)) { $effectiveName } else { $Description }

            $stagingRoot = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "kestrun-krpack-$([Guid]::NewGuid().ToString('N'))")
            $null = New-Item -ItemType Directory -Path $stagingRoot -Force
            $packageRoot = $stagingRoot

            Copy-Item -LiteralPath $resolvedScriptPath -Destination ([System.IO.Path]::Combine($packageRoot, $scriptFileName)) -Force

            $escapedName = $effectiveName.Replace("'", "''")
            $escapedDescription = $effectiveDescription.Replace("'", "''")
            $escapedVersion = $Version.ToString().Replace("'", "''")
            $escapedEntryPoint = $scriptFileName.Replace("'", "''")

            $descriptorLines = [System.Collections.Generic.List[string]]::new()
            $descriptorLines.Add('@{')
            $descriptorLines.Add("    FormatVersion = '1.0'")
            $descriptorLines.Add("    Name = '$escapedName'")
            $descriptorLines.Add("    Description = '$escapedDescription'")
            $descriptorLines.Add("    Version = '$escapedVersion'")
            $descriptorLines.Add("    EntryPoint = '$escapedEntryPoint'")

            if (-not [string]::IsNullOrWhiteSpace($ServiceLogPath)) {
                $escapedServiceLogPath = $ServiceLogPath.Replace("'", "''")
                $descriptorLines.Add("    ServiceLogPath = '$escapedServiceLogPath'")
            }

            if ($null -ne $PreservePaths -and $PreservePaths.Count -gt 0) {
                $descriptorLines.Add('    PreservePaths = @(')
                foreach ($preservePath in $PreservePaths) {
                    if ([string]::IsNullOrWhiteSpace($preservePath)) {
                        continue
                    }

                    $escapedPreservePath = $preservePath.Replace("'", "''")
                    $descriptorLines.Add("        '$escapedPreservePath'")
                }

                $descriptorLines.Add('    )')
            }

            if ($null -ne $ApplicationDataFolders -and $ApplicationDataFolders.Count -gt 0) {
                $descriptorLines.Add('    ApplicationDataFolders = @(')
                foreach ($applicationDataFolder in $ApplicationDataFolders) {
                    if ([string]::IsNullOrWhiteSpace($applicationDataFolder)) {
                        continue
                    }

                    $escapedApplicationDataFolder = $applicationDataFolder.Replace("'", "''")
                    $descriptorLines.Add("        '$escapedApplicationDataFolder'")
                }

                $descriptorLines.Add('    )')
            }

            $descriptorLines.Add('}')

            $descriptorPath = [System.IO.Path]::Combine($packageRoot, 'Service.psd1')
            Set-Content -LiteralPath $descriptorPath -Value ($descriptorLines -join [Environment]::NewLine) -Encoding utf8NoBOM

            $descriptorData = Import-PowerShellDataFile -LiteralPath $descriptorPath
            $descriptorInfo = Test-KrServiceDescriptorData -Descriptor $descriptorData -DescriptorPath $descriptorPath -PackageRoot $packageRoot

            $defaultBaseName = "$effectiveName-$($Version.ToString())"
            $resolvedOutputPath = Get-KrResolvedOutputPath -ProvidedOutputPath $OutputPath -DefaultBaseName $defaultBaseName
        }

        if ((Test-Path -LiteralPath $resolvedOutputPath -PathType Leaf) -and -not $Force) {
            throw "Output package already exists: $resolvedOutputPath. Use -Force to overwrite."
        }

        if ($PSCmdlet.ShouldProcess($resolvedOutputPath, 'Create .krpack package')) {
            Invoke-KrZipFromDirectory -DirectoryPath $packageRoot -DestinationPath $resolvedOutputPath -ExcludedEntries $packageExclusions
        }

        [pscustomobject]([ordered]@{
                PackagePath = $resolvedOutputPath
                Name = $descriptorInfo.Name
                FormatVersion = $descriptorInfo.FormatVersion
                EntryPoint = $descriptorInfo.EntryPoint
                Description = $descriptorInfo.Description
                Version = $descriptorInfo.Version
                ServiceLogPath = $descriptorInfo.ServiceLogPath
                PreservePaths = @($descriptorInfo.PreservePaths)
                ApplicationDataFolders = @($descriptorInfo.ApplicationDataFolders)
            }
        )
    } finally {
        if (-not [string]::IsNullOrWhiteSpace($stagingRoot) -and (Test-Path -LiteralPath $stagingRoot -PathType Container)) {
            Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
