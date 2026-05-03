<#
.SYNOPSIS
    Creates a Docker Compose deployment bundle from a Kestrun service package.
.DESCRIPTION
    Expands a `.krpack` service package, validates its `Service.psd1` descriptor,
    and generates a self-contained Docker deployment bundle that includes:

    - `docker-compose.yml`
    - `Dockerfile`
    - `entrypoint.sh`
    - the input `.krpack` copied as `app.krpack`
    - a local copy of the current `Kestrun` PowerShell module
    - `.dockerignore`

    The generated container uses the Microsoft ASP.NET Core .NET 10 base image
    and installs PowerShell from the Microsoft Linux package repository.
.PARAMETER PackagePath
    Path to the `.krpack` service package.
.PARAMETER OutputPath
    Output directory for the generated deployment bundle.
    Defaults to `<service-name>-docker` in the current directory.
.PARAMETER ImageName
    Docker image name written to `docker-compose.yml`.
    Defaults to `kestrun-<service-name-normalized>:<version>`.
.PARAMETER ServiceName
    Docker Compose service name and container name.
    Defaults to the service descriptor name normalized to lowercase kebab-case.
.PARAMETER PublishedPort
    Host port mapped by Docker Compose.
    Defaults to `8080`.
.PARAMETER ContainerPort
    Container port exposed by the generated image and used by `ASPNETCORE_URLS`.
    Defaults to `8080`.
.PARAMETER KestrunModulePath
    Optional path to the `Kestrun` module root folder to stage into the deployment bundle.
    Defaults to the currently loaded module source folder.
.PARAMETER Force
    Overwrite an existing generated deployment bundle.
.PARAMETER WhatIf
    Shows what would happen if the cmdlet runs. The cmdlet is not executed.
.PARAMETER Confirm
    Prompts for confirmation before running the cmdlet.
.EXAMPLE
    New-KrDockerDeployment -PackagePath .\my-service.krpack
.EXAMPLE
    New-KrDockerDeployment -PackagePath .\my-service.krpack -PublishedPort 5000 -OutputPath .\deploy\docker
.EXAMPLE
    New-KrDockerDeployment -PackagePath .\my-service.krpack -ImageName 'my-registry/my-service:1.2.0' -Force
#>
function New-KrDockerDeployment {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$PackagePath,

        [Parameter()]
        [string]$OutputPath,

        [Parameter()]
        [string]$ImageName,

        [Parameter()]
        [string]$ServiceName,

        [Parameter()]
        [ValidateRange(1, 65535)]
        [int]$PublishedPort = 8080,

        [Parameter()]
        [ValidateRange(1, 65535)]
        [int]$ContainerPort = 8080,

        [Parameter()]
        [string]$KestrunModulePath,

        [Parameter()]
        [switch]$Force
    )

    $declaringModuleBase = if ($MyInvocation.MyCommand.Module) {
        $MyInvocation.MyCommand.Module.ModuleBase
    } else {
        $null
    }

    function Get-KrDefaultModuleRoot {
        <#
        .SYNOPSIS
            Resolves the default Kestrun module root path based on the current script location.
        .DESCRIPTION
            Resolves the module root from the current module base when available and
            falls back to nearby script locations when running from a source checkout.
            Validates that the resolved path contains `Kestrun.psd1` to ensure it is correct.
        .OUTPUTS
            String path to the Kestrun module root.
        #>
        $candidateRoots = [System.Collections.Generic.List[string]]::new()
        foreach ($candidateRoot in @(
                $declaringModuleBase
                $ExecutionContext.SessionState.Module.ModuleBase
                $PSScriptRoot
                (Split-Path -Parent $PSScriptRoot)
                (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
            )) {
            if ([string]::IsNullOrWhiteSpace($candidateRoot)) {
                continue
            }

            $resolvedCandidateRoot = [System.IO.Path]::GetFullPath($candidateRoot)
            if (-not $candidateRoots.Contains($resolvedCandidateRoot)) {
                $candidateRoots.Add($resolvedCandidateRoot)
            }
        }

        foreach ($candidateRoot in $candidateRoots) {
            if (Test-Path -LiteralPath (Join-Path -Path $candidateRoot -ChildPath 'Kestrun.psd1') -PathType Leaf) {
                return $candidateRoot
            }
        }

        throw "Unable to resolve the Kestrun module root from '$PSScriptRoot'."
    }

    function Get-KrNormalizedDockerName {
        <#
        .SYNOPSIS
            Normalizes a string to a valid Docker name.
        .DESCRIPTION
            Converts the input string to lowercase, replaces invalid characters with hyphens, and trims leading/trailing hyphens.
            If the result is empty, returns a fallback name.
        .PARAMETER Name
            The input string to normalize.
        .PARAMETER Fallback
            The fallback name to use if normalization results in an empty string.
        .OUTPUTS
            String containing the normalized Docker name.
        #>
        param(
            [Parameter(Mandatory)]
            [string]$Name,

            [string]$Fallback = 'kestrun-app'
        )

        $normalized = $Name.ToLowerInvariant()
        $normalized = [System.Text.RegularExpressions.Regex]::Replace($normalized, '[^a-z0-9]+', '-')
        $normalized = $normalized.Trim('-')

        if ([string]::IsNullOrWhiteSpace($normalized)) {
            return $Fallback
        }

        return $normalized
    }

    function Get-KrStableDockerSuffix {
        <#
        .SYNOPSIS
            Generates a stable suffix for Docker resource names based on an input string.
        .DESCRIPTION
            Computes a SHA256 hash of the input string and returns the first 12 characters as a hexadecimal suffix.
            This ensures that the same input will always produce the same suffix, which is useful for generating consistent Docker resource names based on variable input.
        .PARAMETER InputValue
            The input string used to generate the hash-based suffix.
        .OUTPUTS
            String containing the stable Docker suffix derived from the input value.
        #>
        param(
            [Parameter(Mandatory)]
            [string]$InputValue
        )

        $bytes = [System.Text.Encoding]::UTF8.GetBytes($InputValue)
        $hash = [System.Security.Cryptography.SHA256]::HashData($bytes)
        return ([System.Convert]::ToHexString($hash)).Substring(0, 12).ToLowerInvariant()
    }

    function Get-KrApplicationDataDefinition {
        <#
        .SYNOPSIS
            Generates application data volume definitions for Docker Compose based on service descriptor entries.
        .DESCRIPTION
            For each relative path specified in the service descriptor's
            `ApplicationDataFolders`, this function generates a corresponding Docker
            volume name and storage path. Each path must point to a subdirectory
            under the service root.
        .PARAMETER NormalizedServiceName
            The normalized name of the service, used as a prefix for volume names.
        .PARAMETER RelativePaths
            An array of relative paths from the service descriptor's
            `ApplicationDataFolders` entry. Each path is processed to generate a
            corresponding Docker volume definition.
        .OUTPUTS
            An array of custom objects containing `RelativePath`, `VolumeName`, and
            `StoragePath` properties for use in Docker Compose volume definitions.
        #>
        param(
            [Parameter(Mandatory)]
            [string]$NormalizedServiceName,

            [Parameter()]
            [string[]]$RelativePaths
        )

        $definitions = [System.Collections.Generic.List[object]]::new()
        foreach ($relativePath in @($RelativePaths)) {
            if ([string]::IsNullOrWhiteSpace($relativePath)) {
                continue
            }

            $normalizedRelativePath = $relativePath.Replace('\\', '/').Trim()
            $trimmedRelativePath = $normalizedRelativePath.Trim('/')
            $pathSegments = @($trimmedRelativePath -split '/')

            if ($pathSegments -contains '.' -or $pathSegments -contains '..') {
                throw (
                    "ApplicationDataFolders entry '{0}' must resolve to a subdirectory under the service root." -f
                    $relativePath
                )
            }

            if ([string]::IsNullOrWhiteSpace($trimmedRelativePath)) {
                continue
            }

            $dockerSegment = Get-KrNormalizedDockerName -Name ($trimmedRelativePath -replace '/', '-') -Fallback 'app-data'
            $hashSuffix = Get-KrStableDockerSuffix -InputValue $trimmedRelativePath.ToLowerInvariant()
            $volumeName = '{0}-appdata-{1}-{2}' -f $NormalizedServiceName, $dockerSegment, $hashSuffix
            $storagePath = '/opt/kestrun/application-data/{0}-{1}' -f $dockerSegment, $hashSuffix

            $definitions.Add([pscustomobject]([ordered]@{
                        RelativePath = $normalizedRelativePath
                        VolumeName = $volumeName
                        StoragePath = $storagePath
                    }))
        }

        return $definitions
    }

    function Get-KrDeploymentOutputPath {
        <#
        .SYNOPSIS
            Resolves the output path for the Docker deployment bundle.
        .DESCRIPTION
            If a path is provided, it returns the full path. Otherwise, it combines the current location with a default directory name.
        .PARAMETER ProvidedOutputPath
            The user-provided output path.
        .PARAMETER DefaultDirectoryName
            The default directory name to use if no path is provided.
        .OUTPUTS
            String containing the resolved output path.
        #>
        param(
            [string]$ProvidedOutputPath,
            [string]$DefaultDirectoryName
        )

        if ([string]::IsNullOrWhiteSpace($ProvidedOutputPath)) {
            return [System.IO.Path]::Combine((Get-Location).Path, $DefaultDirectoryName)
        }

        return [System.IO.Path]::GetFullPath($ProvidedOutputPath)
    }

    function Set-KrGeneratedFileContent {
        <#
        .SYNOPSIS
            Writes content to a file, ensuring the directory exists and handling overwrites based on the Force parameter.
        .DESCRIPTION
            Creates the target directory if it does not exist. If the file already
            exists and Force is not set, it throws an error. Writes the content
            using UTF-8 encoding without a BOM.
        .PARAMETER Path
            The file path where the content should be written.
        .PARAMETER Content
            The content to write to the file.
        .OUTPUTS
            Boolean indicating whether the file was written.
        #>
        [CmdletBinding(SupportsShouldProcess)]
        param(
            [Parameter(Mandatory)]
            [string]$Path,

            [Parameter(Mandatory)]
            [string]$Content
        )

        if (-not $PSCmdlet.ShouldProcess($Path, 'Write generated file content')) {
            return $false
        }

        $directory = [System.IO.Path]::GetDirectoryName($Path)
        if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path -LiteralPath $directory -PathType Container)) {
            $null = New-Item -ItemType Directory -Path $directory -Force
        }

        if ((Test-Path -LiteralPath $Path -PathType Leaf) -and -not $Force) {
            throw "Output file already exists: $Path. Use -Force to overwrite."
        }

        $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
        [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)

        return $true
    }

    function Copy-KrGeneratedDirectory {
        <#
        .SYNOPSIS
            Copies a directory and its contents, handling overwrites based on the Force parameter.
        .DESCRIPTION
            If the destination directory already exists and Force is not set, it throws an error. Otherwise, it removes the existing directory and copies the source directory to the destination.
        .PARAMETER SourcePath
            The path of the source directory to copy.
        .PARAMETER DestinationPath
            The path of the destination directory.
        .OUTPUTS
            Boolean indicating whether the directory was copied.
        #>
        [CmdletBinding(SupportsShouldProcess)]
        param(
            [Parameter(Mandatory)]
            [string]$SourcePath,

            [Parameter(Mandatory)]
            [string]$DestinationPath
        )

        if (-not $PSCmdlet.ShouldProcess($DestinationPath, 'Copy generated directory contents')) {
            return $false
        }

        if (Test-Path -LiteralPath $DestinationPath) {
            if (-not $Force) {
                throw "Output directory already exists: $DestinationPath. Use -Force to overwrite."
            }

            Remove-Item -LiteralPath $DestinationPath -Recurse -Force
        }

        Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Recurse -Force

        return $true
    }

    $temporaryExtractionRoot = $null

    try {
        $resolvedPackagePath = [System.IO.Path]::GetFullPath($PackagePath)
        if (-not (Test-Path -LiteralPath $resolvedPackagePath -PathType Leaf)) {
            throw "Package file not found: $resolvedPackagePath"
        }

        if (-not $resolvedPackagePath.EndsWith('.krpack', [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "PackagePath must point to a .krpack file: $resolvedPackagePath"
        }

        $resolvedModuleRoot = if ([string]::IsNullOrWhiteSpace($KestrunModulePath)) {
            Get-KrDefaultModuleRoot
        } else {
            [System.IO.Path]::GetFullPath($KestrunModulePath)
        }

        if (-not (Test-Path -LiteralPath $resolvedModuleRoot -PathType Container)) {
            throw "Kestrun module path not found: $resolvedModuleRoot"
        }

        if (-not (Test-Path -LiteralPath (Join-Path -Path $resolvedModuleRoot -ChildPath 'Kestrun.psd1') -PathType Leaf)) {
            throw "Kestrun module manifest not found under: $resolvedModuleRoot"
        }

        $temporaryExtractionRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-docker-{0}' -f [Guid]::NewGuid().ToString('N'))
        $null = New-Item -ItemType Directory -Path $temporaryExtractionRoot -Force
        Expand-Archive -LiteralPath $resolvedPackagePath -DestinationPath $temporaryExtractionRoot -Force

        $descriptor = Get-KrServiceDescriptor -Path $temporaryExtractionRoot
        $normalizedServiceName = if ([string]::IsNullOrWhiteSpace($ServiceName)) {
            Get-KrNormalizedDockerName -Name $descriptor.Name -Fallback 'kestrun-app'
        } else {
            Get-KrNormalizedDockerName -Name $ServiceName -Fallback 'kestrun-app'
        }

        $resolvedImageName = if ([string]::IsNullOrWhiteSpace($ImageName)) {
            'kestrun-{0}:{1}' -f $normalizedServiceName, $descriptor.Version
        } else {
            $ImageName
        }

        $resolvedOutputPath = Get-KrDeploymentOutputPath -ProvidedOutputPath $OutputPath -DefaultDirectoryName ('{0}-docker' -f $normalizedServiceName)
        $resolvedPowerShellPackageVersion = '7.6.0-1.deb'
        $composePath = Join-Path -Path $resolvedOutputPath -ChildPath 'docker-compose.yml'
        $dockerfilePath = Join-Path -Path $resolvedOutputPath -ChildPath 'Dockerfile'
        $entrypointPath = Join-Path -Path $resolvedOutputPath -ChildPath 'entrypoint.sh'
        $dockerignorePath = Join-Path -Path $resolvedOutputPath -ChildPath '.dockerignore'
        $packageDestinationPath = Join-Path -Path $resolvedOutputPath -ChildPath 'app.krpack'
        $moduleDestinationPath = Join-Path -Path $resolvedOutputPath -ChildPath 'Kestrun'
        $applicationDataDefinitions = @(Get-KrApplicationDataDefinition -NormalizedServiceName $normalizedServiceName -RelativePaths $descriptor.ApplicationDataFolders)

        $composeLines = [System.Collections.Generic.List[string]]::new()
        $composeLines.Add('services:')
        $composeLines.Add("  ${normalizedServiceName}:")
        $composeLines.Add("    image: $resolvedImageName")
        $composeLines.Add('    build:')
        $composeLines.Add('      context: .')
        $composeLines.Add('      dockerfile: Dockerfile')
        $composeLines.Add('    ports:')
        $composeLines.Add(('      - "{0}:{1}"' -f $PublishedPort, $ContainerPort))
        $composeLines.Add('    environment:')
        $composeLines.Add(('      PORT: "{0}"' -f $ContainerPort))
        $composeLines.Add(('      ASPNETCORE_URLS: "http://+:{0}"' -f $ContainerPort))
        if ($applicationDataDefinitions.Count -gt 0) {
            $composeLines.Add('    volumes:')
            foreach ($applicationDataDefinition in $applicationDataDefinitions) {
                $composeLines.Add("      - $($applicationDataDefinition.VolumeName):$($applicationDataDefinition.StoragePath)")
            }
        }

        $composeLines.Add('    restart: unless-stopped')

        if ($applicationDataDefinitions.Count -gt 0) {
            $composeLines.Add('volumes:')
            foreach ($applicationDataDefinition in $applicationDataDefinitions) {
                $composeLines.Add("  $($applicationDataDefinition.VolumeName):")
            }
        }

        $composeContent = $composeLines -join "`n"

        $dockerfileContent = @"
FROM mcr.microsoft.com/dotnet/aspnet:10.0

ARG POWERSHELL_PACKAGE_VERSION=$resolvedPowerShellPackageVersion

RUN apt-get update \
    && apt-get install -y --no-install-recommends wget ca-certificates \
    && . /etc/os-release \
    && wget -q "https://packages.microsoft.com/config/`${ID}/`${VERSION_ID}/packages-microsoft-prod.deb" \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends powershell=`${POWERSHELL_PACKAGE_VERSION} \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

ENV PORT=$ContainerPort
ENV ASPNETCORE_URLS=http://+:$ContainerPort
WORKDIR /opt/kestrun

COPY Kestrun/ /opt/kestrun/Kestrun/
COPY app.krpack /opt/kestrun/app/app.krpack
COPY entrypoint.sh /opt/kestrun/entrypoint.sh

RUN module_root="`$(pwsh -NoLogo -NoProfile -Command '(`$env:PSModulePath -split [System.IO.Path]::PathSeparator)[0]')" \
    && module_version="`$(pwsh -NoLogo -NoProfile -Command '(Import-PowerShellDataFile -LiteralPath "/opt/kestrun/Kestrun/Kestrun.psd1").ModuleVersion.ToString()')" \
    && mkdir -p "`$module_root/Kestrun/`$module_version" \
    && cp -R /opt/kestrun/Kestrun/. "`$module_root/Kestrun/`$module_version/" \
    && rm -rf /opt/kestrun/Kestrun \
    && printf '%s\n' 'if (Get-Module -ListAvailable Kestrun) {' '    Import-Module Kestrun' '}' > /opt/microsoft/powershell/7/profile.ps1 \
    && chmod +x /opt/kestrun/entrypoint.sh

EXPOSE $ContainerPort

ENTRYPOINT ["/opt/kestrun/entrypoint.sh"]
"@

        $entrypointLines = [System.Collections.Generic.List[string]]::new()
        @(
            '#!/bin/sh'
            'set -eu'
            ''
            'PACKAGE_PATH="/opt/kestrun/app/app.krpack"'
            'SERVICE_ROOT="/opt/kestrun/service"'
            'PERSISTENT_ROOT="/opt/kestrun/application-data"'
            'ENTRYPOINT_FILE="/opt/kestrun/app/entrypoint-path.txt"'
            ''
            'mkdir -p "$PERSISTENT_ROOT"'
            ''
            "pwsh -NoLogo -NoProfile -File - <<'POWERSHELL'"
            '$ErrorActionPreference = ''Stop'''
            '$packagePath = ''/opt/kestrun/app/app.krpack'''
            '$serviceRoot = ''/opt/kestrun/service'''
            '$entrypointFile = ''/opt/kestrun/app/entrypoint-path.txt'''
            '$serviceRootWithSeparator = ([System.IO.Path]::GetFullPath($serviceRoot)).TrimEnd('
            '    [System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) +'
            '    [System.IO.Path]::DirectorySeparatorChar'
            '$applicationDataDefinitions = @('
        ).ForEach({ $entrypointLines.Add($_) })

        foreach ($applicationDataDefinition in $applicationDataDefinitions) {
            $entrypointLines.Add("    [pscustomobject]@{ RelativePath = '$($applicationDataDefinition.RelativePath.Replace("'", "''"))'; StoragePath = '$($applicationDataDefinition.StoragePath.Replace("'", "''"))' }")
        }

        @(
            ')'
            'if (Test-Path -LiteralPath $serviceRoot) {'
            '    Remove-Item -LiteralPath $serviceRoot -Recurse -Force'
            '}'
            '$null = New-Item -ItemType Directory -Path $serviceRoot -Force'
            'Expand-Archive -LiteralPath $packagePath -DestinationPath $serviceRoot -Force'
            '$descriptorPath = [System.IO.Path]::Combine($serviceRoot, ''Service.psd1'')'
            '$descriptor = Import-PowerShellDataFile -LiteralPath $descriptorPath'
            'if (-not $descriptor.ContainsKey(''EntryPoint'') -or [string]::IsNullOrWhiteSpace([string]$descriptor[''EntryPoint''])) {'
            '    throw (''Descriptor {0} is missing required key EntryPoint.'' -f $descriptorPath)'
            '}'
            'foreach ($applicationDataDefinition in $applicationDataDefinitions) {'
            '    $relativePath = [string]$applicationDataDefinition.RelativePath'
            '    $storagePath = [string]$applicationDataDefinition.StoragePath'
            '    $servicePath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($serviceRoot, $relativePath))'
            '    if ($servicePath -eq $serviceRoot -or -not $servicePath.StartsWith($serviceRootWithSeparator, [System.StringComparison]::Ordinal)) {'
            '        throw (''ApplicationDataFolders entry ''''{0}'''' must resolve to a subdirectory under the service root.'' -f $relativePath)'
            '    }'
            '    $storageDirectory = [System.IO.Path]::GetDirectoryName($storagePath)'
            '    if (-not [string]::IsNullOrWhiteSpace($storageDirectory)) {'
            '        $null = New-Item -ItemType Directory -Path $storageDirectory -Force'
            '    }'
            '    if (-not (Test-Path -LiteralPath $storagePath -PathType Container)) {'
            '        $null = New-Item -ItemType Directory -Path $storagePath -Force'
            '    }'
            '    $storageChildren = @(Get-ChildItem -LiteralPath $storagePath -Force -ErrorAction SilentlyContinue)'
            '    if ((Test-Path -LiteralPath $servicePath -PathType Container) -and $storageChildren.Count -eq 0) {'
            '        foreach ($child in Get-ChildItem -LiteralPath $servicePath -Force -ErrorAction SilentlyContinue) {'
            '            Copy-Item -LiteralPath $child.FullName -Destination $storagePath -Recurse -Force'
            '        }'
            '    }'
            '    if (Test-Path -LiteralPath $servicePath) {'
            '        Remove-Item -LiteralPath $servicePath -Recurse -Force'
            '    }'
            '    $serviceDirectory = [System.IO.Path]::GetDirectoryName($servicePath)'
            '    if (-not [string]::IsNullOrWhiteSpace($serviceDirectory)) {'
            '        $null = New-Item -ItemType Directory -Path $serviceDirectory -Force'
            '    }'
            '    $null = New-Item -ItemType SymbolicLink -Path $servicePath -Target $storagePath'
            '}'
            '$entryPointPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($serviceRoot, [string]$descriptor[''EntryPoint'']))'
            '[System.IO.File]::WriteAllText($entrypointFile, $entryPointPath, [System.Text.UTF8Encoding]::new($false))'
            'POWERSHELL'
            ''
            'ENTRYPOINT_PATH=$(cat "$ENTRYPOINT_FILE")'
            ''
            ('export ASPNETCORE_URLS="${{ASPNETCORE_URLS:-http://+:{0}}}"' -f $ContainerPort)
            ('export PORT="${{PORT:-{0}}}"' -f $ContainerPort)
            ''
            'cd "$SERVICE_ROOT"'
            'exec pwsh -NoLogo -File "$ENTRYPOINT_PATH" "$@"'
        ).ForEach({ $entrypointLines.Add($_) })
        $entrypointContent = $entrypointLines -join "`n"

        $dockerignoreContent = @'
*
!Dockerfile
!docker-compose.yml
!entrypoint.sh
!app.krpack
!Kestrun/
!Kestrun/**
'@

        if (-not $PSCmdlet.ShouldProcess($resolvedOutputPath, 'Create Docker deployment bundle')) {
            return
        }

        if (-not (Test-Path -LiteralPath $resolvedOutputPath -PathType Container)) {
            $null = New-Item -ItemType Directory -Path $resolvedOutputPath -Force
        }

        Set-KrGeneratedFileContent -Path $composePath -Content $composeContent -Confirm:$false -WhatIf:$false | Out-Null
        Set-KrGeneratedFileContent -Path $dockerfilePath -Content $dockerfileContent -Confirm:$false -WhatIf:$false | Out-Null
        Set-KrGeneratedFileContent -Path $entrypointPath -Content $entrypointContent -Confirm:$false -WhatIf:$false | Out-Null
        Set-KrGeneratedFileContent -Path $dockerignorePath -Content $dockerignoreContent -Confirm:$false -WhatIf:$false | Out-Null

        if ((Test-Path -LiteralPath $packageDestinationPath -PathType Leaf) -and -not $Force) {
            throw "Output file already exists: $packageDestinationPath. Use -Force to overwrite."
        }

        Copy-Item -LiteralPath $resolvedPackagePath -Destination $packageDestinationPath -Force
        Copy-KrGeneratedDirectory -SourcePath $resolvedModuleRoot -DestinationPath $moduleDestinationPath -Confirm:$false -WhatIf:$false | Out-Null

        [pscustomobject]([ordered]@{
                PackagePath = $resolvedPackagePath
                DeploymentPath = $resolvedOutputPath
                ComposePath = $composePath
                DockerfilePath = $dockerfilePath
                EntrypointPath = $entrypointPath
                DockerignorePath = $dockerignorePath
                ServiceName = $normalizedServiceName
                ImageName = $resolvedImageName
                DescriptorName = $descriptor.Name
                Version = $descriptor.Version
                EntryPoint = $descriptor.EntryPoint
                PublishedPort = $PublishedPort
                ContainerPort = $ContainerPort
            })
    } finally {
        if (-not [string]::IsNullOrWhiteSpace($temporaryExtractionRoot) -and (Test-Path -LiteralPath $temporaryExtractionRoot -PathType Container)) {
            Remove-Item -LiteralPath $temporaryExtractionRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
