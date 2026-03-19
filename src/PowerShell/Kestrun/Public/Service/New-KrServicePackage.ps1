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
.PARAMETER Version
    Service version used when generating Service.psd1 from ScriptPath.
.PARAMETER Description
    Optional description used when generating Service.psd1 from ScriptPath.
    Defaults to Name.
.PARAMETER ServiceLogPath
    Optional ServiceLogPath written to generated Service.psd1.
.PARAMETER OutputPath
    Output .krpack path.
    Defaults:
    - SourceFolder mode: <SourceFolderName>.krpack in current directory
    - ScriptPath mode: <ScriptBaseName>.krpack in current directory
.PARAMETER Force
    Overwrite an existing output file.
.EXAMPLE
    New-KrServicePackage -SourceFolder .\my-service -OutputPath .\my-service.krpack
.EXAMPLE
    New-KrServicePackage -ScriptPath .\server.ps1 -Name demo -Version 1.2.0 -OutputPath .\demo.krpack
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

        [Parameter(Mandatory, ParameterSetName = 'FromScript')]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory, ParameterSetName = 'FromScript')]
        [ValidateNotNullOrEmpty()]
        [version]$Version,

        [Parameter(ParameterSetName = 'FromScript')]
        [string]$Description,

        [Parameter(ParameterSetName = 'FromScript')]
        [string]$ServiceLogPath,

        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]$OutputPath,

        [Parameter()]
        [switch]$Force
    )

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

    function Test-KrServiceDescriptorData {
        param(
            [hashtable]$Descriptor,
            [string]$DescriptorPath,
            [string]$PackageRoot
        )

        if (-not $Descriptor.ContainsKey('Name') -or [string]::IsNullOrWhiteSpace([string]$Descriptor['Name'])) {
            throw "Descriptor '$DescriptorPath' is missing required key 'Name'."
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

            $entryPointFullPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PackageRoot, $entryPoint))
            if (-not $entryPointFullPath.StartsWith([System.IO.Path]::GetFullPath($PackageRoot), [System.StringComparison]::OrdinalIgnoreCase)) {
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

        $legacyScriptFullPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PackageRoot, $legacyScript))
        if (-not $legacyScriptFullPath.StartsWith([System.IO.Path]::GetFullPath($PackageRoot), [System.StringComparison]::OrdinalIgnoreCase)) {
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
        }
    }

    function New-KrZipFromDirectory {
        param(
            [string]$DirectoryPath,
            [string]$DestinationPath
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

            $defaultBaseName = [System.IO.Path]::GetFileName($packageRoot)
            $resolvedOutputPath = Get-KrResolvedOutputPath -ProvidedOutputPath $OutputPath -DefaultBaseName $defaultBaseName
        } else {
            $resolvedScriptPath = [System.IO.Path]::GetFullPath($ScriptPath)
            if (-not (Test-Path -LiteralPath $resolvedScriptPath -PathType Leaf)) {
                throw "Script file not found: $resolvedScriptPath"
            }

            if (-not $resolvedScriptPath.EndsWith('.ps1', [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "ScriptPath must point to a .ps1 file."
            }

            $scriptFileName = [System.IO.Path]::GetFileName($resolvedScriptPath)
            $effectiveDescription = if ([string]::IsNullOrWhiteSpace($Description)) { $Name } else { $Description }

            $stagingRoot = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "kestrun-krpack-$([Guid]::NewGuid().ToString('N'))")
            $null = New-Item -ItemType Directory -Path $stagingRoot -Force
            $packageRoot = $stagingRoot

            Copy-Item -LiteralPath $resolvedScriptPath -Destination ([System.IO.Path]::Combine($packageRoot, $scriptFileName)) -Force

            $escapedName = $Name.Replace("'", "''")
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

            $descriptorLines.Add('}')

            $descriptorPath = [System.IO.Path]::Combine($packageRoot, 'Service.psd1')
            Set-Content -LiteralPath $descriptorPath -Value ($descriptorLines -join [Environment]::NewLine) -Encoding utf8NoBOM

            $descriptorData = Import-PowerShellDataFile -LiteralPath $descriptorPath
            $descriptorInfo = Test-KrServiceDescriptorData -Descriptor $descriptorData -DescriptorPath $descriptorPath -PackageRoot $packageRoot

            $defaultBaseName = [System.IO.Path]::GetFileNameWithoutExtension($scriptFileName)
            $resolvedOutputPath = Get-KrResolvedOutputPath -ProvidedOutputPath $OutputPath -DefaultBaseName $defaultBaseName
        }

        if ((Test-Path -LiteralPath $resolvedOutputPath -PathType Leaf) -and -not $Force) {
            throw "Output package already exists: $resolvedOutputPath. Use -Force to overwrite."
        }

        if ($PSCmdlet.ShouldProcess($resolvedOutputPath, 'Create .krpack package')) {
            New-KrZipFromDirectory -DirectoryPath $packageRoot -DestinationPath $resolvedOutputPath
        }

        [pscustomobject]([ordered]@{
            PackagePath = $resolvedOutputPath
            Name = $descriptorInfo.Name
            FormatVersion = $descriptorInfo.FormatVersion
            EntryPoint = $descriptorInfo.EntryPoint
            Description = $descriptorInfo.Description
            Version = $descriptorInfo.Version
            ServiceLogPath = $descriptorInfo.ServiceLogPath
        })
    }
    finally {
        if (-not [string]::IsNullOrWhiteSpace($stagingRoot) -and (Test-Path -LiteralPath $stagingRoot -PathType Container)) {
            Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
