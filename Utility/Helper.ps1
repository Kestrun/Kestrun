﻿# Kestrun.Build.psm1
#requires -Version 7.4

# ---- Public functions ------------------------------------------------------

<#
    .SYNOPSIS
        Retrieves the filesystem path to the ReportGenerator tool used for creating coverage reports.
    .DESCRIPTION
        Get-ReportGeneratorPath locates the ReportGenerator executable or script on the local machine.
        It centralizes the logic for discovering the tool so callers can reliably invoke ReportGenerator when producing code coverage reports.
        The function may search known installation directories, environment variables, or tool-restore locations and returns the first matching path found.
    .EXAMPLE
        Get-ReportGeneratorPath
        # Returns a string with the full path to ReportGenerator, for example:
        # "C:\tools\ReportGenerator\ReportGenerator.exe"
    .EXAMPLE
        $rgPath = Get-ReportGeneratorPath
        & $rgPath -reports:'.\coverage.xml' -targetdir:'.\coverage-report'
        # Uses the returned path to run ReportGenerator and produce a coverage report.
    .OUTPUTS
        System.String
        A single string containing the absolute path to the ReportGenerator executable or script. If the tool cannot be located the function may return $null or throw an error depending on implementation.
    .NOTES
        Callers should validate that the returned path exists and is executable before attempting to run it.
        This function is intended to keep tool discovery logic in one place so other scripts can remain simpler and more robust.
#>
function Get-ReportGeneratorPath {
    $toolDir = if ($IsWindows) { Join-Path $env:USERPROFILE ".dotnet\tools" } else { "$HOME/.dotnet/tools" }
    $exe = if ($IsWindows) { "reportgenerator.exe" } else { "reportgenerator" }
    Join-Path $toolDir $exe
}

<#
    .SYNOPSIS
        Installs (or ensures availability of) ReportGenerator for converting code-coverage files into human-readable reports.
    .DESCRIPTION
        Install-ReportGenerator makes ReportGenerator available on the machine or CI agent so coverage outputs
        (for example OpenCover, Cobertura, or other supported formats) can be transformed into HTML and other report formats.
        The function is intended to be idempotent: it detects an existing suitable installation and will avoid unnecessary reinstallation unless a forced update is requested.
        It writes progress and informational messages to the host and returns non-terminating or terminating errors if installation cannot be completed.
    .OUTPUTS
        None. Progress and status messages are written to the host. On success, the ReportGenerator executable/tool is available on PATH or at a documented location.
    .EXAMPLE
        # Ensure ReportGenerator is installed using default method
        PS> Install-ReportGenerator
    .EXAMPLE
        # Typical usage in a CI job before generating coverage reports
        PS> Install-ReportGenerator
        PS> Invoke-SomeCoverageTool
        PS> ReportGenerator -reports:coverage.xml -targetdir:coverage-report
    .NOTES
        - Intended for use in local development and CI/CD pipelines.
        - The caller should have appropriate privileges to install software or write to the chosen location.
        - Implementation may choose the best available installation mechanism for the current platform (local download, dotnet tool, package manager, etc.).
#>
function Install-ReportGenerator {
    $rg = Get-ReportGeneratorPath
    if (-not (Test-Path $rg)) {
        Write-Host "Installing ReportGenerator (dotnet global tool)..." -ForegroundColor Cyan
        dotnet tool install -g dotnet-reportgenerator-globaltool | Out-Host
    }
    # ensure current session PATH includes toolDir
    $toolDir = Split-Path $rg
    $sep = [IO.Path]::PathSeparator
    if (-not ($env:PATH -split $sep | Where-Object { $_ -eq $toolDir })) {
        $env:PATH = "$toolDir$sep$env:PATH"
    }
    return $rg
}


<#
.SYNOPSIS
    Returns the filesystem path to the ASP.NET "Shared" directory for a specified .NET / ASP.NET framework version.
.DESCRIPTION
    Get-AspNetSharedDir accepts a framework version string and resolves the location of the ASP.NET "Shared" directory that corresponds to that framework.
    The function normalizes common version formats (for example "v4.0", "4.0", "v2.0.50727") and
    returns a full path string that can be used to locate shared ASP.NET assemblies or configuration items.
.PARAMETER framework
    The framework version to resolve. Accepts typical .NET/ASP.NET version representations such as:
    - "v4.0"
    - "4.0"
    - "v2.0.50727"
    This parameter is required.
.OUTPUTS
    System.String
    A full path to the ASP.NET Shared directory for the specified framework.
    If the directory cannot be found, the function will either return $null or throw an error depending on implementation and error-handling preferences.
.EXAMPLE
    # Resolve the shared directory for .NET 4.0 and print it
    Get-AspNetSharedDir -framework "v4.0"
.EXAMPLE
    # Use the returned path to reference a file in the Shared folder
    $shared = Get-AspNetSharedDir "4.0"
    Join-Path $shared "SomeSharedFile.dll"
.EXAMPLE
    # Handle a missing result gracefully
    $path = Get-AspNetSharedDir "v9.9"
    if ($null -eq $path) {
        Write-Warning "Requested framework shared directory not found."
    }
#>
function Get-AspNetSharedDir([string]$framework) {
    $major = ($framework -replace '^net(\d+)\..+$', '$1')
    $runtimes = & dotnet --list-runtimes | Select-String 'Microsoft.AspNetCore.App'
    if (-not $runtimes) { throw "Microsoft.AspNetCore.App runtime not found" }

    $aspnetMatches = @()
    foreach ($r in $runtimes) {
        $parts = ($r.ToString() -split '\s+')
        $ver = $parts[1]
        $base = ($r.ToString() -replace '.*\[(.*)\].*', '$1')
        if ($ver -like "$major.*") {
            $aspnetMatches += [pscustomobject]@{ Version = [version]$ver; Dir = (Join-Path $base $ver) }
        }
    }

    if (-not $aspnetMatches) { throw "No Microsoft.AspNetCore.App runtime found for net$major.x" }
    ($aspnetMatches | Sort-Object Version -Descending | Select-Object -First 1).Dir
}

<#
    .SYNOPSIS
        Retrieves the version information from the version file.
    .DESCRIPTION
        This function reads the version information from a JSON file and returns the version string.
        It also retrieves the release and iteration information if available.
    .PARAMETER FileVersion
        The path to the version file.
    .PARAMETER VersionOnly
        If specified, only the version string is returned.
    .PARAMETER Details
        If specified, the full version details including release and iteration information are returned.
    .EXAMPLE
        Get-Version -FileVersion './version.json'
        This will return the version string from the specified JSON file.
    .EXAMPLE
        Get-Version -FileVersion './version.json' -VersionOnly
        This will return only the version string from the specified JSON file.
    .OUTPUTS
        [string]
        Returns the version string, including release and iteration information if available.
#>
function Get-Version {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FileVersion,
        [switch]$VersionOnly,
        [switch]$Details
    )
    if (-not (Test-Path -Path $FileVersion)) {
        throw "File version file not found: $FileVersion"
    }
    $versionData = Get-Content -Path $FileVersion | ConvertFrom-Json -AsHashtable
    $Version = $versionData.Version
    if ($VersionOnly) {
        return $Version
    }
    $Release = $versionData.Release
    $ReleaseIteration = ([string]::IsNullOrEmpty($versionData.Iteration))? $Release : "$Release.$($versionData.Iteration)"
    if ($Release -ne 'Stable') {
        $Version = "$Version-$ReleaseIteration"
        $versionData.Prerelease = $true
    }
    if ($Details) {
        $commit = (git rev-parse --short HEAD).Trim()
        $versionData.InformationalVersion = "$($Version)+$commit"
        $versionData.FullVersion = $Version
        return $versionData
    }
    return $Version
}

<#
  .SYNOPSIS
    Chooses the best TFM (Target Framework Moniker) folder from a library folder.
    This is useful for multi-targeted libraries that may have different versions of the same assembly for different frameworks.
  .DESCRIPTION
    Returns the path to the best TFM folder, or null if none is found.
    This is useful for multi-targeted libraries that may have different versions of the same assembly for different frameworks.
#>
function Get-BestTfmFolder([string]$LibFolder) {
    if (-not (Test-Path $LibFolder)) { return $null }
    $tfms = Get-ChildItem -Path $LibFolder -Directory | Select-Object -ExpandProperty Name
    $preference = @(
        'net9.0', 'net9.0-windows',
        'net8.0', 'net8.0-windows',
        'net7.0', 'net7.0-windows',
        'net6.0', 'net6.0-windows',
        'netstandard2.1', 'netstandard2.0',
        'net472', 'net471', 'net48', 'net47'
    )
    foreach ($p in $preference) { if ($tfms -contains $p) { return Join-Path $LibFolder $p } }
    if ($tfms.Count -gt 0) { return Join-Path $LibFolder $tfms[0] }
    return $null
}

<#
    .SYNOPSIS
        Downloads and extracts a NuGet package.
    .DESCRIPTION
        Downloads a NuGet package and extracts it to a specified folder.
        This function is designed to work cross-platform without relying on nuget.exe.
    .PARAMETER Id
        The ID of the NuGet package to download.
    .PARAMETER Version
        The version of the NuGet package to download.
    .PARAMETER WorkRoot
        The root directory where the package will be downloaded.
    .PARAMETER Force
        Whether to force re-download the package if it already exists.
    .PARAMETER Retries
        The number of times to retry downloading the package if it fails.
    .PARAMETER DelaySeconds
        The number of seconds to wait before retrying the download.
    .PARAMETER MaxDelaySeconds
        The maximum number of seconds to wait before retrying the download.
    .PARAMETER TimeoutSec
        The number of seconds to wait before timing out the download.
    .EXAMPLE
        Get-PackageFolder -Id 'MyPackage' -Version '1.0.0' -WorkRoot 'C:\Packages' -Force
        This will download and extract the specified NuGet package to the specified work root.
    .EXAMPLE
        Get-PackageFolder -Id 'MyPackage' -Version '1.0.0' -WorkRoot 'C:\Packages' -Force -Retries 5
        This will download and extract the specified NuGet package to the specified work root, with 5 retries on failure.

    .OUTPUTS
        The path to the extracted package folder.
#>
function Get-PackageFolder {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$WorkRoot,
        [switch]$Force,

        # --- Retry knobs ---
        [Parameter()]
        [int]$Retries = 3,            # number of retries after the first attempt (total attempts = Retries + 1)
        [int]$DelaySeconds = 2,       # initial backoff delay
        [int]$MaxDelaySeconds = 30,   # backoff cap
        [int]$TimeoutSec = 120        # web request timeout per attempt
    )

    $idLower = $Id.ToLowerInvariant()
    $pkgRoot = Join-Path $WorkRoot "$Id.$Version"

    if (-not $Force -and (Test-Path $pkgRoot)) {
        return (Resolve-Path $pkgRoot).Path
    }

    $attempt = 0
    while ($attempt -le $Retries) {
        $attempt++

        try {
            # fresh folder every attempt
            if (Test-Path $pkgRoot) {
                Remove-Item -Recurse -Force $pkgRoot -ErrorAction SilentlyContinue
            }
            New-Item -ItemType Directory -Path $pkgRoot -Force | Out-Null

            $nupkgName = "$idLower.$Version.nupkg"
            $nupkgUrl = "https://api.nuget.org/v3-flatcontainer/$idLower/$Version/$nupkgName"
            $nupkgPath = Join-Path $pkgRoot $nupkgName

            Write-Host "Downloading $Id $Version (attempt $attempt of $($Retries + 1))..."
            Invoke-WebRequest `
                -Uri $nupkgUrl `
                -OutFile $nupkgPath `
                -MaximumRedirection 5 `
                -TimeoutSec $TimeoutSec `
                -Headers @{ 'User-Agent' = "Get-PackageFolder/1.0 (+PowerShell $($PSVersionTable.PSVersion))" } `
                -ErrorAction Stop

            try {
                Expand-Archive -Path $nupkgPath -DestinationPath $pkgRoot -Force -ErrorAction Stop
            } finally {
                if (Test-Path $nupkgPath) { Remove-Item $nupkgPath -Force -ErrorAction SilentlyContinue }
            }

            return (Resolve-Path $pkgRoot).Path
        } catch {
            if ($attempt -gt $Retries) { throw }

            # Exponential backoff with jitter
            $base = [Math]::Min($MaxDelaySeconds, $DelaySeconds * [Math]::Pow(2, $attempt - 1))
            $jitter = 1 + (Get-Random -Minimum -0.2 -Maximum 0.2) # ±20%
            $sleep = [int][Math]::Max(1, [Math]::Round($base * $jitter))

            Write-Warning ("Get-PackageFolder failed (attempt {0}/{1}): {2}`nRetrying in {3}s..." -f $attempt, ($Retries + 1), $_.Exception.Message, $sleep)

            # Clean up any partial extraction before the next try
            if (Test-Path $pkgRoot) {
                Remove-Item -Recurse -Force $pkgRoot -ErrorAction SilentlyContinue
            }

            Start-Sleep -Seconds $sleep
        }
    }
}


<#
    .SYNOPSIS
        Gets the path to the shared framework for a given family and major version.
    .DESCRIPTION
        Returns the path to the shared framework, or throws an error if not found.
    .PARAMETER family
        The family name of the shared framework (e.g. Microsoft.AspNetCore.App).
    .PARAMETER major
        The major version of the shared framework (e.g. 8 or 9).
    .OUTPUTS
        The path to the shared framework, or throws an error if not found.
#>
function Get-SharedFrameworkPath {
    param(
        [string]$family,
        [int]$major
    )

    $escapedFamily = [Regex]::Escape($family)

    $rts = (& dotnet --list-runtimes) -split "`n" |
        Where-Object { $_ -match "^$escapedFamily\s+$major\.\d+\.\d+\s+\[(.+)\]" } |
        ForEach-Object {
            [PSCustomObject]@{
                Version = [Version]($_ -replace "^$escapedFamily\s+([0-9\.]+)\s+\[.+\]$", '$1')
                Root = ($_ -replace '^.*\[(.+)\]$', '$1')
            }
        } | Sort-Object Version -Descending

    if (-not $rts) { throw "No $family $major.x runtime found." }

    return Join-Path $($rts[0].Root) $($rts[0].Version)
}

<#
    .SYNOPSIS
        Sets the package name for a Cobertura coverage report.
    .DESCRIPTION
        Updates the Cobertura XML file to use a consistent assembly name and optional base path for class names.
    .PARAMETER CoberturaPath
        The path to the Cobertura XML file.
    .PARAMETER AssemblyName
        The logical assembly name to use (e.g. 'Kestrun.PowerShell').
    .PARAMETER BasePath
        The base path to use for resolving class names (optional).
#>
function Set-CoberturaPackageName {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    param(
        [Parameter(Mandatory)] [string]$CoberturaPath,
        [Parameter(Mandatory)] [string]$AssemblyName   # e.g., 'Kestrun.PowerShell'
    )

    if (-not (Test-Path $CoberturaPath)) { throw "Cobertura not found: $CoberturaPath" }

    [xml]$doc = Get-Content -LiteralPath $CoberturaPath
    $packages = @($doc.coverage.packages.package)
    if (-not $packages) { return }  # nothing to tweak

    foreach ($pkg in $packages) {
        # Unify all PowerShell files under a single logical assembly
        $pkg.name = $AssemblyName

        # Do NOT change $pkg.classes.class.filename or .name
        # Leaving filenames intact keeps history stable across runs/OSes.
    }

    $doc.Save($CoberturaPath)
}

<#
    .SYNOPSIS
        Sets the package name for a Cobertura coverage report.
    .DESCRIPTION
        Updates the Cobertura XML file to use a consistent assembly name and optional base path for class names.
    .PARAMETER CoberturaPath
        The path to the Cobertura XML file.
    .PARAMETER AssemblyName
        The logical assembly name to use (e.g. 'Kestrun.PowerShell').
    .PARAMETER BasePath
        The base path to use for resolving class names (optional).
    .PARAMETER GroupByFirstFolder
        Whether to group classes by their first folder (Public/Private).
    .PARAMETER FolderRenameMap
        A hashtable mapping old folder names to new folder names.
    .PARAMETER AllowedFirstFolders
        A list of allowed first folders for grouping.
    .PARAMETER ExcludePathContains
        A list of path segments to exclude from grouping.
#>
function Set-CoberturaGrouping {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string]$CoberturaPath,
        [string]$AssemblyRoot = 'Kestrun.PowerShell',
        [string]$BasePath = 'src/PowerShell/Kestrun',
        [switch]$GroupByFirstFolder,
        [hashtable]$FolderRenameMap = @{},
        [string[]]$AllowedFirstFolders = @('Public', 'Private'),
        [string[]]$ExcludePathContains = @('/lib/', '/Modules/PSDiagnostics/', 'PSDiagnostics.psm1')  # case-insensitive match
    )

    if (-not (Test-Path $CoberturaPath)) { throw "Cobertura not found: $CoberturaPath" }

    <#
    .SYNOPSIS
        Normalizes a file path by replacing backslashes with forward slashes and trimming whitespace.
    .DESCRIPTION
        This function is used to ensure consistent file path formatting across different operating systems.
    .PARAMETER p
        The file path to normalize.
    .OUTPUTS
        Returns the normalized file path as a string.
    #>
    function Normalize([string]$p) { if ($null -eq $p) { return '' }; return ($p -replace '\\', '/').Trim() }

    $baseNorm = Normalize $BasePath
    if ($baseNorm -and $baseNorm[-1] -ne '/') { $baseNorm += '/' }

    [xml]$doc = Get-Content -LiteralPath $CoberturaPath
    $packages = @($doc.coverage.packages.package)
    if (-not $packages) { return }

    $removed = 0
    foreach ($pkg in $packages) {
        # collect classes to remove (can’t safely delete while iterating)
        $toRemove = New-Object System.Collections.Generic.List[System.Xml.XmlElement]
        foreach ($cls in @($pkg.classes.class)) {
            $fileNorm = Normalize $cls.filename
            if ([string]::IsNullOrWhiteSpace($fileNorm)) { continue }

            # quick path-based excludes
            $excludeHit = $false
            foreach ($needle in $ExcludePathContains) {
                if ($needle -and ($fileNorm -like "*$needle*")) { $excludeHit = $true; break }
            }
            if ($excludeHit) { $toRemove.Add($cls); continue }

            # cut to region starting at Public/Private if present
            $rel = $fileNorm
            $m = [Regex]::Match($rel, '(?i)(?:^|/)(Public|Private)(/|$)')
            if ($m.Success) {
                $rel = $rel.Substring($m.Index).TrimStart('/')
            } else {
                # try to trim by base path if it’s in the string
                if ($baseNorm -and $rel.IndexOf($baseNorm, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    $rel = $rel.Substring($rel.IndexOf($baseNorm, [StringComparison]::OrdinalIgnoreCase) + $baseNorm.Length)
                } else {
                    # no anchor → treat as relative-ish
                    $rel = $rel.TrimStart('/')
                }
            }

            # split segments
            $parts = @($rel -split '/+' | Where-Object { $_ -ne '' })
            if ($parts.Count -eq 0) { $toRemove.Add($cls); continue }

            # root folder (Public/Private/…)
            $first = $parts[0]
            if ($FolderRenameMap.ContainsKey($first)) { $first = [string]$FolderRenameMap[$first] }

            # if we’re grouping by root, enforce allowlist (drop 'lib', etc.)
            if ($GroupByFirstFolder -and $AllowedFirstFolders.Count -gt 0) {
                if (-not ($AllowedFirstFolders | ForEach-Object { $_.ToLower() } | Where-Object { $_ -eq $first.ToLower() })) {
                    $toRemove.Add($cls); continue
                }
            }

            # build remainder
            $rest = @()
            if ($parts.Count -gt 1) { $rest = @($parts[1..($parts.Count - 1)]) }
            if ($rest.Count -gt 0) { $rest[-1] = [IO.Path]::GetFileNameWithoutExtension($rest[-1]) }
            elseif ($parts.Count -eq 1) {
                $nameNoExt = [IO.Path]::GetFileNameWithoutExtension($first)
                if ($nameNoExt -and $nameNoExt -ne $first) { $rest = @($nameNoExt) }
            }

            # package and class name shaping
            $pkg.name = ($GroupByFirstFolder ? "$AssemblyRoot.$first" : $AssemblyRoot)

            $dotted = @()
            if ($GroupByFirstFolder) { $dotted += $first }
            if ($rest.Count -gt 0) { $dotted += $rest }
            $cls.name = ($dotted.Count -gt 0) ? ($dotted -join '.') : $first
        }

        foreach ($dead in $toRemove) { [void]$dead.ParentNode.RemoveChild($dead); $removed++ }

        # prune empty packages
        if (-not @($pkg.classes.class)) {
            [void]$pkg.ParentNode.RemoveChild($pkg)
        }
    }

    if ($removed -gt 0) { Write-Host "🧹 Excluded $removed non-project classes from coverage (lib/diagnostics/etc.)" }
    $doc.Save($CoberturaPath)
}



