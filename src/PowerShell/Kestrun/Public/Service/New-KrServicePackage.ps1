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
.PARAMETER OutputPath
    Output .krpack path.
    Defaults:
    - SourceFolder mode: <SourceFolderName>.krpack in current directory
    - ScriptPath mode: <Name>-<Version>.krpack in current directory
.PARAMETER Force
    Overwrite an existing output file.
.EXAMPLE
    New-KrServicePackage -SourceFolder .\my-service -OutputPath .\my-service.krpack
.EXAMPLE
    New-KrServicePackage -ScriptPath .\server.ps1 -Name demo -Version 1.2.0 -OutputPath .\demo.krpack
.EXAMPLE
    New-KrServicePackage -ScriptPath .\server.ps1 -Version 1.2.0
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
            Invoke-KrZipFromDirectory -DirectoryPath $packageRoot -DestinationPath $resolvedOutputPath
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
            }
        )
    } finally {
        if (-not [string]::IsNullOrWhiteSpace($stagingRoot) -and (Test-Path -LiteralPath $stagingRoot -PathType Container)) {
            Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
