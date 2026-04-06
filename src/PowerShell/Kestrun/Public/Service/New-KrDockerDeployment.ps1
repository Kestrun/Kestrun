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

    function Get-KrDefaultModuleRoot {
        $moduleRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
        if (-not (Test-Path -LiteralPath (Join-Path -Path $moduleRoot -ChildPath 'Kestrun.psd1') -PathType Leaf)) {
            throw "Unable to resolve the Kestrun module root from '$PSScriptRoot'."
        }

        return $moduleRoot
    }

    function Get-KrNormalizedDockerName {
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

    function Get-KrDeploymentOutputPath {
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
        param(
            [Parameter(Mandatory)]
            [string]$Path,

            [Parameter(Mandatory)]
            [string]$Content
        )

        $directory = [System.IO.Path]::GetDirectoryName($Path)
        if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path -LiteralPath $directory -PathType Container)) {
            $null = New-Item -ItemType Directory -Path $directory -Force
        }

        if ((Test-Path -LiteralPath $Path -PathType Leaf) -and -not $Force) {
            throw "Output file already exists: $Path. Use -Force to overwrite."
        }

        $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
        [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
    }

    function Copy-KrGeneratedDirectory {
        param(
            [Parameter(Mandatory)]
            [string]$SourcePath,

            [Parameter(Mandatory)]
            [string]$DestinationPath
        )

        if (Test-Path -LiteralPath $DestinationPath) {
            if (-not $Force) {
                throw "Output directory already exists: $DestinationPath. Use -Force to overwrite."
            }

            Remove-Item -LiteralPath $DestinationPath -Recurse -Force
        }

        Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Recurse -Force
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

        $temporaryExtractionRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("kestrun-docker-{0}" -f [Guid]::NewGuid().ToString('N'))
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
        $composePath = Join-Path -Path $resolvedOutputPath -ChildPath 'docker-compose.yml'
        $dockerfilePath = Join-Path -Path $resolvedOutputPath -ChildPath 'Dockerfile'
        $entrypointPath = Join-Path -Path $resolvedOutputPath -ChildPath 'entrypoint.sh'
        $dockerignorePath = Join-Path -Path $resolvedOutputPath -ChildPath '.dockerignore'
        $packageDestinationPath = Join-Path -Path $resolvedOutputPath -ChildPath 'app.krpack'
        $moduleDestinationPath = Join-Path -Path $resolvedOutputPath -ChildPath 'Kestrun'

        $composeContent = @"
services:
  ${normalizedServiceName}:
    container_name: $normalizedServiceName
    image: $resolvedImageName
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "$PublishedPort`:$ContainerPort"
    environment:
      PORT: "$ContainerPort"
      ASPNETCORE_URLS: "http://+:$ContainerPort"
    restart: unless-stopped
"@

        $dockerfileContent = @"
FROM mcr.microsoft.com/dotnet/aspnet:10.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends wget ca-certificates \
    && . /etc/os-release \
    && wget -q "https://packages.microsoft.com/config/`${ID}/`${VERSION_ID}/packages-microsoft-prod.deb" \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends powershell \
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

        $entrypointLines = @(
            '#!/bin/sh'
            'set -eu'
            ''
            'PACKAGE_PATH="/opt/kestrun/app/app.krpack"'
            'SERVICE_ROOT="/opt/kestrun/service"'
            ''
            'rm -rf "$SERVICE_ROOT"'
            'mkdir -p "$SERVICE_ROOT"'
            ''
            'pwsh -NoLogo -NoProfile -Command ''$ErrorActionPreference = "Stop"; Expand-Archive -LiteralPath "/opt/kestrun/app/app.krpack" -DestinationPath "/opt/kestrun/service" -Force'''
            ''
            'ENTRYPOINT_PATH=$(pwsh -NoLogo -NoProfile -Command ''$ErrorActionPreference = "Stop"; $descriptorPath = [System.IO.Path]::Combine("/opt/kestrun/service", "Service.psd1"); $descriptor = Import-PowerShellDataFile -LiteralPath $descriptorPath; if (-not $descriptor.ContainsKey("EntryPoint") -or [string]::IsNullOrWhiteSpace([string]$descriptor["EntryPoint"])) { throw ("Descriptor {0} is missing required key EntryPoint." -f $descriptorPath) }; [System.IO.Path]::GetFullPath([System.IO.Path]::Combine("/opt/kestrun/service", [string]$descriptor["EntryPoint"]))'')'
            ''
            ('export ASPNETCORE_URLS="${{ASPNETCORE_URLS:-http://+:{0}}}"' -f $ContainerPort)
            ('export PORT="${{PORT:-{0}}}"' -f $ContainerPort)
            ''
            'cd "$SERVICE_ROOT"'
            'exec pwsh -NoLogo -File "$ENTRYPOINT_PATH" "$@"'
        )
        $entrypointContent = $entrypointLines -join "`n"

        $dockerignoreContent = @"
*
!Dockerfile
!docker-compose.yml
!entrypoint.sh
!app.krpack
!Kestrun/
!Kestrun/**
"@

        if ($PSCmdlet.ShouldProcess($resolvedOutputPath, 'Create Docker deployment bundle')) {
            if (-not (Test-Path -LiteralPath $resolvedOutputPath -PathType Container)) {
                $null = New-Item -ItemType Directory -Path $resolvedOutputPath -Force
            }

            Set-KrGeneratedFileContent -Path $composePath -Content $composeContent
            Set-KrGeneratedFileContent -Path $dockerfilePath -Content $dockerfileContent
            Set-KrGeneratedFileContent -Path $entrypointPath -Content $entrypointContent
            Set-KrGeneratedFileContent -Path $dockerignorePath -Content $dockerignoreContent

            if ((Test-Path -LiteralPath $packageDestinationPath -PathType Leaf) -and -not $Force) {
                throw "Output file already exists: $packageDestinationPath. Use -Force to overwrite."
            }

            Copy-Item -LiteralPath $resolvedPackagePath -Destination $packageDestinationPath -Force
            Copy-KrGeneratedDirectory -SourcePath $resolvedModuleRoot -DestinationPath $moduleDestinationPath
        }

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
