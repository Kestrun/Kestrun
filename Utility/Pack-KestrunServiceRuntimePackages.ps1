<#
.SYNOPSIS
    Packs staged Kestrun service runtime payloads into per-RID NuGet packages.
.DESCRIPTION
    Converts staged payloads under src/CSharp/Kestrun.Tool/kestrun-service/<rid>/ into
    Kestrun.Service.<rid>.<version>.nupkg files. Each package contains:
    - host/kestrun-service-host(.exe)
    - modules/**
    - runtime-manifest.json
    - Kestrun.Service.<rid>.nuspec
.PARAMETER ServiceHostRuntimesDirectory
    Root directory containing staged service runtime payloads by RID.
.PARAMETER OutputDirectory
    Target directory for generated .nupkg files.
.PARAMETER Version
    Package version to assign to each runtime package.
.PARAMETER RuntimeIdentifiers
    Runtime identifiers to package.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ServiceHostRuntimesDirectory,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string[]]$RuntimeIdentifiers
)

$ErrorActionPreference = 'Stop'

function New-KestrunRuntimeNuspec {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageId,
        [Parameter(Mandatory = $true)]
        [string]$PackageVersion,
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier
    )

    return @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>$PackageId</id>
    <version>$PackageVersion</version>
    <authors>Kestrun Team</authors>
    <owners>Kestrun</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <license type="expression">MIT</license>
    <projectUrl>https://github.com/kestrun/Kestrun</projectUrl>
    <repository type="git" url="https://github.com/kestrun/Kestrun" />
    <description>Kestrun dedicated service runtime payload for $RuntimeIdentifier.</description>
    <tags>kestrun service runtime powershell</tags>
  </metadata>
</package>
"@
}

function New-KestrunRuntimeManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier,
        [Parameter(Mandatory = $true)]
        [string]$ServiceHostFileName
    )

    return [ordered]@{
        rid = $RuntimeIdentifier
        entryPoint = "host/$ServiceHostFileName"
        modulesPath = 'modules'
    } | ConvertTo-Json -Depth 4
}

if (-not (Test-Path -Path $ServiceHostRuntimesDirectory)) {
    throw "Missing staged service runtime directory: $ServiceHostRuntimesDirectory"
}

if (-not (Test-Path -Path $OutputDirectory)) {
    New-Item -Path $OutputDirectory -ItemType Directory -Force | Out-Null
}

$stagingRoot = Join-Path -Path $OutputDirectory -ChildPath '_runtime-package-staging'
if (Test-Path -Path $stagingRoot) {
    Remove-Item -Path $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
}

try {
    foreach ($runtimeIdentifier in $RuntimeIdentifiers) {
        $packageId = "Kestrun.Service.$runtimeIdentifier"
        $runtimeRoot = Join-Path -Path $ServiceHostRuntimesDirectory -ChildPath $runtimeIdentifier
        $serviceHostFileName = if ($runtimeIdentifier -like 'win-*') { 'kestrun-service-host.exe' } else { 'kestrun-service-host' }
        $serviceHostPath = Join-Path -Path $runtimeRoot -ChildPath $serviceHostFileName
        $modulesPath = Join-Path -Path $runtimeRoot -ChildPath 'Modules'

        if (-not (Test-Path -Path $serviceHostPath)) {
            throw "Missing staged service host for '$runtimeIdentifier': $serviceHostPath"
        }

        if (-not (Test-Path -Path $modulesPath)) {
            throw "Missing staged PowerShell modules for '$runtimeIdentifier': $modulesPath"
        }

        $packageStagingDirectory = Join-Path -Path $stagingRoot -ChildPath $runtimeIdentifier
        if (Test-Path -Path $packageStagingDirectory) {
            Remove-Item -Path $packageStagingDirectory -Recurse -Force -ErrorAction SilentlyContinue
        }

        $hostStagingDirectory = Join-Path -Path $packageStagingDirectory -ChildPath 'host'
        $modulesStagingDirectory = Join-Path -Path $packageStagingDirectory -ChildPath 'modules'
        New-Item -Path $hostStagingDirectory -ItemType Directory -Force | Out-Null
        New-Item -Path $modulesStagingDirectory -ItemType Directory -Force | Out-Null

        Copy-Item -Path $serviceHostPath -Destination (Join-Path -Path $hostStagingDirectory -ChildPath $serviceHostFileName) -Force
        Copy-Item -Path (Join-Path -Path $modulesPath -ChildPath '*') -Destination $modulesStagingDirectory -Recurse -Force

        $manifestPath = Join-Path -Path $packageStagingDirectory -ChildPath 'runtime-manifest.json'
        New-KestrunRuntimeManifest -RuntimeIdentifier $runtimeIdentifier -ServiceHostFileName $serviceHostFileName |
            Set-Content -Path $manifestPath -Encoding UTF8

        $nuspecPath = Join-Path -Path $packageStagingDirectory -ChildPath "$packageId.nuspec"
        New-KestrunRuntimeNuspec -PackageId $packageId -PackageVersion $Version -RuntimeIdentifier $runtimeIdentifier |
            Set-Content -Path $nuspecPath -Encoding UTF8

        $packagePath = Join-Path -Path $OutputDirectory -ChildPath "$packageId.$Version.nupkg"
        if (Test-Path -Path $packagePath) {
            Remove-Item -Path $packagePath -Force -ErrorAction SilentlyContinue
        }

        Compress-Archive -Path (Join-Path -Path $packageStagingDirectory -ChildPath '*') -DestinationPath $packagePath -CompressionLevel Optimal
        Write-Host "    ✅ Packed runtime package: $packagePath" -ForegroundColor Green
    }
}
finally {
    if (Test-Path -Path $stagingRoot) {
        Remove-Item -Path $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
