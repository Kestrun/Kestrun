# Kestrun.Build.psm1
#requires -Version 7.4

$moduleRootPath = Split-Path -Parent -Path (
    Split-Path -Parent -Path (
        Split-Path -Parent -Path $MyInvocation.MyCommand.Path
    )
)

Get-ChildItem "$($moduleRootPath)/src/PowerShell/Kestrun/Private/Assembly/*.ps1" -Recurse | ForEach-Object { . ([System.IO.Path]::GetFullPath($_)) }

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
    $toolDir = if ($IsWindows) { Join-Path $env:USERPROFILE '.dotnet\tools' } else { "$HOME/.dotnet/tools" }
    $exe = if ($IsWindows) { 'reportgenerator.exe' } else { 'reportgenerator' }
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
        Write-Host 'Installing ReportGenerator (dotnet global tool)...' -ForegroundColor Cyan
        dotnet tool install -g dotnet-reportgenerator-globaltool | Out-Host
    }
    # ensure current session PATH includes toolDir
    $toolDir = Split-Path -Parent -Path $rg
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
    if (-not $runtimes) { throw 'Microsoft.AspNetCore.App runtime not found' }

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

            Write-Host "Downloading $Id $Version (attempt $attempt of $($Retries + 1))..." -NoNewline
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
            Write-Host 'Succeeded.' -ForegroundColor Green
            return (Resolve-Path $pkgRoot).Path
        } catch {
            if ($attempt -gt $Retries) { throw }

            # Exponential backoff with jitter
            $base = [Math]::Min($MaxDelaySeconds, $DelaySeconds * [Math]::Pow(2, $attempt - 1))
            $jitter = 1 + (Get-Random -Minimum -0.2 -Maximum 0.2) # ±20%
            $sleep = [int][Math]::Max(1, [Math]::Round($base * $jitter))
            Write-Host 'Failed.' -ForegroundColor Yellow
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
        # collect classes to remove (can't safely delete while iterating)
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
                # try to trim by base path if it's in the string
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

            # if we're grouping by root, enforce allowlist (drop 'lib', etc.)
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

<#
.SYNOPSIS
    Removes the comment-based help block from a PowerShell script file.
.DESCRIPTION
    This function reads the content of a PowerShell script file, removes the comment-based help block (enclosed in <(dash) ... (dash)>),
    collapses multiple blank lines into a single blank line, and trims leading/trailing blank lines.
.PARAMETER Path
    The path to the PowerShell script file.
.PARAMETER ShouldProcess
    Indicates whether to perform the operation. If specified, the function will prompt for confirmation before proceeding.
.PARAMETER ConfirmImpact
    The impact level of the operation for confirmation prompts. Default is 'Low'.
.EXAMPLE
    $modifiedContent = Remove-CommentHelpBlock -Path './MyScript.ps1'
    This will read the content of 'MyScript.ps1', remove the comment-based help block
.OUTPUTS
    The modified content of the script file as a string.
#>
function Remove-CommentHelpBlock {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
    param(
        [string]$Path
    )
    if ($PSCmdlet.ShouldProcess('CommentHelpBlock', 'Remove')) {
        $content = Get-Content $Path -Raw

        # Normalize newlines to LF
        $content = $content -replace "`r`n", "`n"
        $content = $content -replace "`r", "`n"

        # Strip the first <# ... #> block (comment-based help)
        $stripped = $content -replace '<#[\s\S]*?#>', ''

        # Collapse 3+ blank lines → one blank line
        $stripped = $stripped -replace '(\n) { 3, }', "`n`n"

        # Trim leading/trailing blank lines
        return $stripped.Trim()
    }
}

<#
.SYNOPSIS
    Synchronizes PowerShell DLLs from the C# project output to the PowerShell module lib folder.
.DESCRIPTION
    Copies compiled DLLs from the C# project output directory to the PowerShell module's lib folder,
    ensuring that the necessary assemblies are available for the PowerShell module to function correctly.
    It also checks for the presence of Microsoft.CodeAnalysis assemblies and downloads them if missing.
.PARAMETER dest
    The destination directory where the DLLs will be copied. Default is '.\src\PowerShell\Kestrun\lib'.
.PARAMETER Configuration
    The build configuration (Debug or Release). Default is 'Debug'.
.PARAMETER Frameworks
    An array of target frameworks to synchronize. Default is @('net8.0', 'net9.0').
.PARAMETER PowerShellHome
    The path to the PowerShell installation to scan for existing DLLs. Default is $PSHOME.
.PARAMETER ScanPowerShellHomeRecursively
    A switch indicating whether to scan the PowerShell home directory recursively for existing DLLs.
.EXAMPLE
    Sync-PowerShellDll -dest '.\src\PowerShell\Kestrun\lib' -Configuration 'Release' -Frameworks @('net8.0', 'net9.0')
    This will synchronize the DLLs from the Release build output for net8.0 and net9.0 to the specified lib folder.
.OUTPUTS
    None. The function performs file operations to copy DLLs to the specified destination.
#>
function Sync-PowerShellDll {
    param (
        [Parameter()]
        [string]$dest = '.\src\PowerShell\Kestrun\lib',

        [Parameter()]
        [ValidateSet('Debug', 'Release')]
        [string]$Configuration = 'Debug',

        [Parameter()]
        [string[]]$Frameworks = @('net8.0', 'net9.0'),

        [Parameter()]
        [string]$PowerShellHome = $PSHOME,

        [Parameter()]
        [switch]$ScanPowerShellHomeRecursively
    )
    if (  (Test-Path -Path $dest)) {
        Write-Host "🧹 Cleaning destination folder: $dest"
        Remove-Item -Path (Join-Path -Path $dest -ChildPath '*') -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }

    if (-not (Test-Path -Path $dest)) {
        Write-Host "📁 Creating destination folder: $dest"
        New-Item -Path $dest -ItemType Directory -Force | Out-Null
    }

    # Build a fast lookup of DLLs already provided by PowerShell
    $psDllNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    if (Test-Path -Path $PowerShellHome) {
        $scanPath = (Resolve-Path $PowerShellHome).Path
        $gciParams = @{
            Path = $scanPath
            Filter = '*.dll'
            File = $true
            ErrorAction = 'SilentlyContinue'
        }
        if ($ScanPowerShellHomeRecursively) { $gciParams.Recurse = $true }

        Get-ChildItem @gciParams | ForEach-Object { [void]$psDllNames.Add($_.Name) }

        Write-Host ('🔎 PowerShellHome scan: {0} DLL names indexed from {1}' -f $psDllNames.Count, $scanPath)
    } else {
        Write-Warning "PowerShellHome '$PowerShellHome' not found; skip-list will be empty."
    }

    # Get list of PowerShell core DLL names to skip copying
    $src = Join-Path -Path $PWD -ChildPath 'src' -AdditionalChildPath 'CSharp', 'Kestrun', 'bin', $Configuration
    Write-Host "📁 Preparing to copy files from $src to $dest"
    if (-not (Test-Path -Path $dest)) {
        New-Item -Path $dest -ItemType Directory -Force | Out-Null
    }
    if (-not (Test-Path -Path (Join-Path -Path $dest -ChildPath 'Microsoft.CodeAnalysis'))) {
        Write-Host '📦 Missing CodeAnalysis (downloading)...'
        & .\Utility\Download-CodeAnalysis.ps1 -OutputDir $dest
    }
    foreach ($framework in $Frameworks) {
        $destFramework = Join-Path -Path $dest -ChildPath $framework
        if (Test-Path -Path $destFramework) {
            Remove-Item -Path $destFramework -Recurse -Force | Out-Null
        }
        New-Item -Path $destFramework -ItemType Directory -Force | Out-Null
        $destFramework = Resolve-Path -Path $destFramework
        $srcFramework = Resolve-Path (Join-Path -Path $src -ChildPath $framework)
        Write-Host "📄 Copying dlls from $srcFramework to $destFramework"
        $cultureDirs = @(
            'cs', 'de', 'es', 'fr', 'it', 'ja', 'ko', 'pl',
            'pt-BR', 'ru', 'tr', 'zh-Hans', 'zh-Hant'
        )

        # Normalize src root once
        $sep = [System.IO.Path]::DirectorySeparatorChar
        $srcRoot = (Resolve-Path $srcFramework).Path.TrimEnd($sep)

        Get-ChildItem -Path $srcFramework -Recurse -File |
            Where-Object {
                $full = $_.FullName

                # Get path *relative* to $srcFramework and split into segments
                $rel = $full.Substring($srcRoot.Length).TrimStart($sep, '/')
                $parts = $rel -split '[\\/]'

                # True if any segment is one of the culture directory names
                $isCulturePath = ($parts | Where-Object { $_ -in $cultureDirs }).Count -gt 0

                -not (
                    ($_.Name -like 'Microsoft.CodeAnalysis*' -and $_.Name -notlike 'Microsoft.CodeAnalysis.Razor*') -or
                    $_.Name -eq 'Kestrun.Annotations.dll' -or
                    #$full -like "*$sep" + 'ref' + "$sep*" -or # any path segment "\ref\"
                    #$full -like "*$sep" + 'refs' + "$sep*" -or # any path segment "\refs\"
                    $parts -contains 'ref' -or # any path segment "\ref\"
                    $parts -contains 'refs' -or # any path segment "\refs\"
                    $isCulturePath                               # any culture dir anywhere in the path
                )
            } |
            ForEach-Object {
                # Skip anything under runtimes
                if ($_.DirectoryName.Contains("$sep" + 'runtimes' + "$sep")) { return }

                # Skip DLLs that PowerShell already ships
                if ($_.Extension -ieq '.dll' -and $psDllNames.Contains($_.Name)) {
                    if ($VerboseSkips) { Write-Host "⏭️  Skipping (in PSHOME): $($_.Name)" }
                    return
                }

                $targetPath = $_.FullName.Replace($srcFramework, $destFramework)
                $targetDir = Split-Path -Path $targetPath -Parent

                if (-not (Test-Path $targetDir)) {
                    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
                }

                Copy-Item -Path $_.FullName -Destination $targetPath -Force
            }
    }
    # Additionally, copy Kestrun.Annotations.dll and .pdb to PowerShell lib/assemblies
    $annotationSrc = Join-Path -Path $PWD -ChildPath 'src' -AdditionalChildPath 'CSharp', 'Kestrun.Annotations' , 'bin', $Configuration, 'net8.0'
    $annotationDest = Join-Path -Path $dest -ChildPath 'assemblies'
    Write-Host "📄 Copying Kestrun.Annotations.dll from $annotationSrc to $annotationDest"
    # Create destination directory if it doesn't exist
    if (-not (Test-Path -Path $annotationDest)) {
        New-Item -Path $annotationDest -ItemType Directory -Force | Out-Null
    }
    # Copy the DLL and PDB files
    Copy-Item -Path (Join-Path -Path $annotationSrc -ChildPath 'Kestrun.Annotations.dll') -Destination (Join-Path -Path $annotationDest -ChildPath 'Kestrun.Annotations.dll') -Force

    if ($Configuration -eq 'Debug' -and (Test-Path -Path (Join-Path -Path $annotationSrc -ChildPath 'Kestrun.Annotations.pdb'))) {
        Copy-Item -Path (Join-Path -Path $annotationSrc -ChildPath 'Kestrun.Annotations.pdb') -Destination (Join-Path -Path $annotationDest -ChildPath 'Kestrun.Annotations.pdb') -Force
    }
}

<#
.SYNOPSIS
    Helper function to run dotnet test with consistent parameters for Kestrun test projects.
.PARAMETER ProjectPath
    The path to the .csproj file of the test project to run.
.PARAMETER Framework
    The target framework to test against.
.PARAMETER Label
    A label to identify the test run in logs and results (e.g., 'Kestrun.Tests net8.0').
.EXAMPLE
    Invoke-KestrunDotNetTest -ProjectPath './tests/CSharp.Tests/Kestrun.Tests/Kestrun.Tests.csproj' -Framework 'net8.0' -Label 'Kestrun.Tests net8.0'
    This example demonstrates how to run the Kestrun core tests for the net8.0 framework with a specific label for logging and results.
.NOTES
    This function is used internally by the Test-xUnit build task to run tests with consistent logging and result handling. It sets up the results directory, log paths, and common parameters for dotnet test.
#>
function Invoke-KestrunDotNetTest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $ProjectPath,
        [Parameter(Mandatory = $true)]
        [string] $Framework,
        [Parameter(Mandatory = $true)]
        [string] $Label,
        [Parameter()]
        [ValidateSet('Debug', 'Release')]
        [string] $Configuration = 'Debug',
        [Parameter()]
        [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
        [string] $DotNetVerbosity = 'minimal',
        [Parameter()]
        [string] $ResultsRoot = (Join-Path -Path (Get-Location) -ChildPath 'TestResults' -AdditionalChildPath 'xunit'),
        [Parameter()]
        [string] $TestFilter,
        [Parameter()]
        [string] $RunLabelSuffix = '',
        [Parameter()]
        [switch] $NoBuild
    )

    $safeLabel = ($Label -replace '[^A-Za-z0-9._-]', '_')
    $safeFramework = ($Framework -replace '[^A-Za-z0-9._-]', '_')
    $targetResultsDir = Join-Path -Path $ResultsRoot -ChildPath $safeLabel -AdditionalChildPath $safeFramework

    if (-not (Test-Path -Path $targetResultsDir)) {
        New-Item -Path $targetResultsDir -ItemType Directory -Force | Out-Null
    }

    $safeRunLabelSuffix = if ([string]::IsNullOrWhiteSpace($RunLabelSuffix)) {
        ''
    } else {
        '-' + ($RunLabelSuffix -replace '[^A-Za-z0-9._-]', '_')
    }
    $resultStem = "$safeLabel-$safeFramework$safeRunLabelSuffix"
    $diagLogPath = Join-Path -Path $targetResultsDir -ChildPath "$resultStem.diag.log"
    $trxFileName = "$resultStem.trx"
    $trxPath = Join-Path -Path $targetResultsDir -ChildPath $trxFileName
    $failureManifestPath = Join-Path -Path $targetResultsDir -ChildPath "$resultStem.failed-tests.json"
    $hangTimeout = if ($env:KESTRUN_TEST_HANG_TIMEOUT) { $env:KESTRUN_TEST_HANG_TIMEOUT } else { '5m' }

    Write-Host "🧪 dotnet test target: $Label ($Framework)" -ForegroundColor Cyan
    Write-Host "📁 xUnit results directory: $targetResultsDir" -ForegroundColor DarkCyan
    Write-Host "📝 xUnit diag log: $diagLogPath" -ForegroundColor DarkCyan
    Write-Host "📄 xUnit failure manifest: $failureManifestPath" -ForegroundColor DarkCyan
    Write-Host "⏱️ xUnit hang timeout: $hangTimeout" -ForegroundColor DarkCyan
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        Write-Host "🔎 xUnit filter: $TestFilter" -ForegroundColor DarkYellow
    }

    $arguments = @(
        'test',
        $ProjectPath,
        '-c', $Configuration,
        '-f', $Framework,
        "-v:$DotNetVerbosity",
        '--results-directory', $targetResultsDir,
        '--logger', "trx;LogFileName=$trxFileName",
        '--logger', 'console;verbosity=detailed',
        '--diag', $diagLogPath,
        '--blame-hang',
        '--blame-hang-timeout', $hangTimeout
    )

    if ($NoBuild) {
        $arguments += '--no-build'
    }

    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        $arguments += @('--filter', $TestFilter)
    }

    & dotnet @arguments
    $exitCode = $LASTEXITCODE

    $failedSelectors = if (Test-Path -LiteralPath $trxPath) {
        @(Get-KestrunTrxFailedSelector -TrxPath $trxPath -ProjectPath $ProjectPath -Framework $Framework -Label $Label)
    } else {
        Write-Warning "TRX result file was not created for '$Label' ($Framework): $trxPath"
        @()
    }

    ConvertTo-Json -InputObject @($failedSelectors) -Depth 6 | Set-Content -LiteralPath $failureManifestPath

    return [pscustomobject]@{
        ExitCode            = $exitCode
        ProjectPath         = $ProjectPath
        Framework           = $Framework
        Label               = $Label
        TestFilter          = $TestFilter
        TrxPath             = $trxPath
        DiagLogPath         = $diagLogPath
        ResultsDirectory    = $targetResultsDir
        FailureManifestPath = $failureManifestPath
        FailedSelectors     = @($failedSelectors)
    }
}

<#
.SYNOPSIS
    Returns the latest UTC write time across one or more files or directories.
.DESCRIPTION
    Scans the provided paths and returns the newest LastWriteTimeUtc found.
    Files under bin/ and obj/ are ignored so generated artifacts do not make
    source freshness checks report false positives.
.PARAMETER Paths
    Files or directories to scan.
.PARAMETER Include
    Optional file patterns used when a path is a directory.
#>
function Get-KestrunLatestWriteTimeUtc {
    [CmdletBinding()]
    [OutputType([datetime])]
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Paths,
        [Parameter()]
        [string[]] $Include = @('*')
    )

    $latest = [datetime]::MinValue
    foreach ($path in $Paths) {
        if (-not (Test-Path -Path $path)) {
            continue
        }

        $item = Get-Item -LiteralPath $path
        $files = if ($item.PSIsContainer) {
            Get-ChildItem -LiteralPath $item.FullName -Recurse -File -Include $Include -ErrorAction SilentlyContinue
        } else {
            @($item)
        }

        foreach ($file in $files) {
            if ($file.FullName -match '[\\/](bin|obj)[\\/]') {
                continue
            }

            if ($file.LastWriteTimeUtc -gt $latest) {
                $latest = $file.LastWriteTimeUtc
            }
        }
    }

    return $latest
}

<#
.SYNOPSIS
    Builds the Kestrun managed projects required by test and packaging workflows.
.DESCRIPTION
    Builds Kestrun.Annotations for its annotation framework and Kestrun for each
    requested target framework. This is the reusable helper behind the build task
    and the incremental Pester preparation path.
.PARAMETER KestrunProjectPath
    Path to Kestrun.csproj.
.PARAMETER KestrunAnnotationsProjectPath
    Path to Kestrun.Annotations.csproj.
.PARAMETER Frameworks
    Target frameworks to build for Kestrun.
.PARAMETER AnnotationFramework
    Target framework to build for Kestrun.Annotations.
.PARAMETER Configuration
    Build configuration.
.PARAMETER DotNetVerbosity
    dotnet CLI verbosity.
.PARAMETER Version
    Package version to stamp into assemblies.
.PARAMETER InformationalVersion
    Informational version to stamp into assemblies.
#>
function Invoke-KestrunBuildNoPwsh {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $KestrunProjectPath,
        [Parameter(Mandatory = $true)]
        [string] $KestrunAnnotationsProjectPath,
        [Parameter(Mandatory = $true)]
        [string[]] $Frameworks,
        [Parameter(Mandatory = $true)]
        [string] $AnnotationFramework,
        [Parameter()]
        [ValidateSet('Debug', 'Release')]
        [string] $Configuration = 'Debug',
        [Parameter()]
        [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
        [string] $DotNetVerbosity = 'minimal',
        [Parameter(Mandatory = $true)]
        [string] $Version,
        [Parameter(Mandatory = $true)]
        [string] $InformationalVersion
    )

    if (Get-Module -Name Kestrun) {
        throw 'Kestrun module is currently loaded in this PowerShell session. Please close all sessions using the Kestrun module before building.'
    }

    Write-Host '🔨 Building solution...'

    Write-Host "Building Kestrun.Annotations for single framework: $AnnotationFramework" -ForegroundColor DarkCyan
    dotnet build "$KestrunAnnotationsProjectPath" -c $Configuration -f $AnnotationFramework -v:$DotNetVerbosity -p:Version=$Version -p:InformationalVersion=$InformationalVersion
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for Kestrun.Annotations project for framework $AnnotationFramework"
    }

    Write-Host "Building Kestrun for multiple frameworks: $($Frameworks -join ', ')" -ForegroundColor DarkCyan
    foreach ($framework in $Frameworks) {
        Write-Host "  - Target Framework: $framework" -ForegroundColor DarkCyan
        dotnet build "$KestrunProjectPath" -c $Configuration -f $framework -v:$DotNetVerbosity -p:Version=$Version -p:InformationalVersion=$InformationalVersion

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed for Kestrun project for framework $framework"
        }
    }
}

<#
.SYNOPSIS
    Tests whether managed build outputs are current for the requested frameworks.
.DESCRIPTION
    Compares the newest relevant source file timestamp with the expected output
    assemblies for Kestrun.Annotations and Kestrun.
.PARAMETER RepoRoot
    Repository root used to resolve shared build input files.
.PARAMETER KestrunProjectPath
    Path to Kestrun.csproj.
.PARAMETER KestrunAnnotationsProjectPath
    Path to Kestrun.Annotations.csproj.
.PARAMETER Frameworks
    Target frameworks expected for Kestrun outputs.
.PARAMETER AnnotationFramework
    Target framework expected for Kestrun.Annotations output.
.PARAMETER Configuration
    Build configuration.
#>
function Test-KestrunBuildOutputsCurrent {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot,
        [Parameter(Mandatory = $true)]
        [string] $KestrunProjectPath,
        [Parameter(Mandatory = $true)]
        [string] $KestrunAnnotationsProjectPath,
        [Parameter(Mandatory = $true)]
        [string[]] $Frameworks,
        [Parameter(Mandatory = $true)]
        [string] $AnnotationFramework,
        [Parameter()]
        [ValidateSet('Debug', 'Release')]
        [string] $Configuration = 'Debug'
    )

    $sharedInputs = @(
        (Join-Path -Path $RepoRoot -ChildPath 'Directory.Build.props'),
        (Join-Path -Path $RepoRoot -ChildPath 'Directory.Build.targets'),
        (Join-Path -Path $RepoRoot -ChildPath 'global.json'),
        $KestrunAnnotationsProjectPath,
        $KestrunProjectPath
    )
    $sourceInclude = @('*.cs', '*.csproj', '*.props', '*.targets', '*.json', '*.resx')
    $annotationInputs = $sharedInputs + (Join-Path -Path $RepoRoot -ChildPath 'src/CSharp/Kestrun.Annotations')
    $annotationOutput = Join-Path -Path $RepoRoot -ChildPath "src/CSharp/Kestrun.Annotations/bin/$Configuration/$AnnotationFramework/Kestrun.Annotations.dll"

    if (-not (Test-Path -Path $annotationOutput)) {
        return $false
    }

    $annotationInputTime = Get-KestrunLatestWriteTimeUtc -Paths $annotationInputs -Include $sourceInclude
    if ((Get-Item -LiteralPath $annotationOutput).LastWriteTimeUtc -lt $annotationInputTime) {
        return $false
    }

    $projectInputs = $sharedInputs + (Join-Path -Path $RepoRoot -ChildPath 'src/CSharp/Kestrun')
    $projectInputTime = Get-KestrunLatestWriteTimeUtc -Paths $projectInputs -Include $sourceInclude

    foreach ($framework in $Frameworks) {
        $projectOutput = Join-Path -Path $RepoRoot -ChildPath "src/CSharp/Kestrun/bin/$Configuration/$framework/Kestrun.dll"
        if (-not (Test-Path -Path $projectOutput)) {
            return $false
        }

        if ((Get-Item -LiteralPath $projectOutput).LastWriteTimeUtc -lt $projectInputTime) {
            return $false
        }
    }

    return $true
}

<#
.SYNOPSIS
    Tests whether the synced PowerShell module libraries are current.
.DESCRIPTION
    Verifies that the synced lib folder contains the expected Kestrun binaries,
    annotations assembly, and CodeAnalysis directory, and that they are not older
    than the corresponding managed build outputs.
.PARAMETER RepoRoot
    Repository root used to resolve source output paths.
.PARAMETER PowerShellLibRoot
    Root folder of the synced PowerShell module libraries.
.PARAMETER Frameworks
    Target frameworks expected under the PowerShell lib folder.
.PARAMETER AnnotationFramework
    Target framework expected for the annotations assembly source path.
.PARAMETER Configuration
    Build configuration.
#>
function Test-KestrunPowerShellLibCurrent {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot,
        [Parameter(Mandatory = $true)]
        [string] $PowerShellLibRoot,
        [Parameter(Mandatory = $true)]
        [string[]] $Frameworks,
        [Parameter(Mandatory = $true)]
        [string] $AnnotationFramework,
        [Parameter()]
        [ValidateSet('Debug', 'Release')]
        [string] $Configuration = 'Debug'
    )

    if (-not (Test-Path -Path $PowerShellLibRoot)) {
        return $false
    }

    if (-not (Test-Path -Path (Join-Path -Path $PowerShellLibRoot -ChildPath 'Microsoft.CodeAnalysis'))) {
        return $false
    }

    $annotationSource = Join-Path -Path $RepoRoot -ChildPath "src/CSharp/Kestrun.Annotations/bin/$Configuration/$AnnotationFramework/Kestrun.Annotations.dll"
    $annotationDest = Join-Path -Path $PowerShellLibRoot -ChildPath 'assemblies/Kestrun.Annotations.dll'

    if ((-not (Test-Path -Path $annotationSource)) -or (-not (Test-Path -Path $annotationDest))) {
        return $false
    }

    if ((Get-Item -LiteralPath $annotationDest).LastWriteTimeUtc -lt (Get-Item -LiteralPath $annotationSource).LastWriteTimeUtc) {
        return $false
    }

    foreach ($framework in $Frameworks) {
        $sourceDll = Join-Path -Path $RepoRoot -ChildPath "src/CSharp/Kestrun/bin/$Configuration/$framework/Kestrun.dll"
        $destDll = Join-Path -Path $PowerShellLibRoot -ChildPath "$framework/Kestrun.dll"

        if ((-not (Test-Path -Path $sourceDll)) -or (-not (Test-Path -Path $destDll))) {
            return $false
        }

        if ((Get-Item -LiteralPath $destDll).LastWriteTimeUtc -lt (Get-Item -LiteralPath $sourceDll).LastWriteTimeUtc) {
            return $false
        }
    }

    return $true
}

<#
.SYNOPSIS
    Writes a UTC timestamped line to both the host and a log file.
.DESCRIPTION
    Appends a single line prefixed with the current UTC timestamp to the
    specified log file and mirrors the same line to the console.
.PARAMETER Path
    Log file path to append to.
.PARAMETER Message
    Message text to write.
.PARAMETER ForegroundColor
    Console color used when writing to the host.
#>
function Write-KestrunTimestampedLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,
        [Parameter(Mandatory = $true)]
        [string] $Message,
        [Parameter()]
        [System.ConsoleColor] $ForegroundColor = [System.ConsoleColor]::Gray
    )

    $parentPath = Split-Path -Parent -Path $Path
    if ($parentPath -and -not (Test-Path -LiteralPath $parentPath)) {
        New-Item -Path $parentPath -ItemType Directory -Force | Out-Null
    }

    $line = '[{0}] {1}' -f [DateTimeOffset]::UtcNow.ToString('o'), $Message
    Add-Content -LiteralPath $Path -Value $line
    Write-Host $line -ForegroundColor $ForegroundColor
    return $line
}

<#
.SYNOPSIS
    Resolves Pester test files for one or more file or directory paths.
.DESCRIPTION
    Expands directory inputs to all *.Tests.ps1 files beneath them and returns
    a deterministic, unique list of resolved file paths.
.PARAMETER TestPath
    One or more file or directory paths containing Pester tests.
.PARAMETER ExcludePath
    Optional file or directory paths to remove from the discovered test set.
.PARAMETER ShardCount
    Total number of deterministic shards to split the resulting test set into.
.PARAMETER ShardIndex
    One-based shard number to return from the discovered test set.
#>
function Get-KestrunPesterTestFile {
    [CmdletBinding()]
    [OutputType([object[]])]
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $TestPath,
        [Parameter()]
        [string[]] $ExcludePath = @(),
        [Parameter()]
        [ValidateRange(1, 256)]
        [int] $ShardCount = 1,
        [Parameter()]
        [ValidateRange(1, 256)]
        [int] $ShardIndex = 1
    )

    if ($ShardIndex -gt $ShardCount) {
        throw "ShardIndex ($ShardIndex) cannot be greater than ShardCount ($ShardCount)."
    }

    $testFiles = [System.Collections.Generic.List[string]]::new()

    foreach ($path in $TestPath) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Pester test path not found: $path"
        }

        $item = Get-Item -LiteralPath $path
        if ($item.PSIsContainer) {
            $resolvedFiles = Get-ChildItem -LiteralPath $item.FullName -Recurse -File -Filter '*.Tests.ps1' |
                Sort-Object -Property FullName
            foreach ($file in $resolvedFiles) {
                $testFiles.Add($file.FullName)
            }
        } else {
            $testFiles.Add($item.FullName)
        }
    }

    $resolvedFiles = @($testFiles | Sort-Object -Unique)
    if ($ExcludePath.Count -gt 0) {
        $comparison = if ($IsWindows) {
            [System.StringComparison]::OrdinalIgnoreCase
        } else {
            [System.StringComparison]::Ordinal
        }

        $excludedItems = foreach ($path in $ExcludePath) {
            if (-not (Test-Path -LiteralPath $path)) {
                throw "Pester exclude path not found: $path"
            }

            $item = Get-Item -LiteralPath $path
            $fullPath = [System.IO.Path]::GetFullPath($item.FullName)
            if ($item.PSIsContainer) {
                '{0}{1}' -f $fullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar), [System.IO.Path]::DirectorySeparatorChar
            } else {
                $fullPath
            }
        }

        $resolvedFiles = @(
            $resolvedFiles |
                Where-Object {
                    $candidate = [System.IO.Path]::GetFullPath($_)
                    $isExcluded = $false
                    foreach ($excludedItem in $excludedItems) {
                        if ($excludedItem.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
                            if ($candidate.StartsWith($excludedItem, $comparison)) {
                                $isExcluded = $true
                                break
                            }
                        } elseif ([string]::Equals($candidate, $excludedItem, $comparison)) {
                            $isExcluded = $true
                            break
                        }
                    }

                    -not $isExcluded
                }
        )
    }

    if ($ShardCount -eq 1) {
        return $resolvedFiles
    }

    $shardedFiles = [System.Collections.Generic.List[string]]::new()
    for ($index = $ShardIndex - 1; $index -lt $resolvedFiles.Count; $index += $ShardCount) {
        $shardedFiles.Add($resolvedFiles[$index])
    }

    return @($shardedFiles)
}

<#
.SYNOPSIS
    Creates a Pester configuration for a Kestrun test run.
.DESCRIPTION
    Builds a PesterConfiguration with pass-through results, NUnit XML output,
    and the standard OS-specific excluded tags used by the repository.
.PARAMETER TestPath
    The file or directory paths to execute.
.PARAMETER Verbosity
    Pester output verbosity.
.PARAMETER ResultsPath
    NUnit XML output path for the run.
.PARAMETER TestSuiteName
    Root test-suite name written to the XML report.
#>
function New-KestrunPesterConfig {
    [CmdletBinding()]
    [outputtype([PesterConfiguration])]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $TestPath,
        [Parameter(Mandatory = $true)]
        [ValidateSet('None', 'Normal', 'Detailed', 'Diagnostic', 'Quiet')]
        [string] $Verbosity,
        [Parameter(Mandatory = $true)]
        [string] $ResultsPath,
        [Parameter()]
        [string] $TestSuiteName = 'Kestrun Pester'
    )

    $resultsParent = Split-Path -Parent -Path $ResultsPath
    if ($resultsParent -and -not (Test-Path -LiteralPath $resultsParent)) {
        New-Item -Path $resultsParent -ItemType Directory -Force | Out-Null
    }

    $effectiveVerbosity = if ($Verbosity -eq 'Quiet') { 'None' } else { $Verbosity }

    $cfg = [PesterConfiguration]::Default
    $cfg.Run.Path = @($TestPath)
    $cfg.Output.Verbosity = $effectiveVerbosity
    $cfg.TestResult.Enabled = $true
    $cfg.TestResult.OutputFormat = 'NUnitXml'
    $cfg.TestResult.OutputPath = $ResultsPath
    $cfg.TestResult.TestSuiteName = $TestSuiteName
    $cfg.Run.Exit = $false
    $cfg.Run.PassThru = $true

    $excludeTag = @()
    if ($IsLinux) { $excludeTag += 'Exclude_Linux' }
    if ($IsMacOS) { $excludeTag += 'Exclude_MacOs' }
    if ($IsWindows) { $excludeTag += 'Exclude_Windows' }
    $cfg.Filter.ExcludeTag = $excludeTag

    return $cfg
}

<#
.SYNOPSIS
    Extracts failed test selectors from a Pester run result.
.DESCRIPTION
    Returns unique failing tests with their file, line, and full name so the
    rerun pass can target the exact failing examples.
.PARAMETER PesterRun
    Pester result object returned by Invoke-Pester.
#>
function Get-KestrunPesterFailedSelector {
    [CmdletBinding()]
    [OutputType([object[]])]
    param(
        [Parameter(Mandatory = $true)]
        $PesterRun
    )

    $failedSelectors = foreach ($test in @($PesterRun.Tests | Where-Object { $_.Result -eq 'Failed' })) {
        $file = $null
        try {
            $file = $test.ScriptBlock.File
        } catch {
            Write-Warning "Failed to get ScriptBlock.File for test '$($test.Name)': $_"
            if ($test.Block -and $test.Block.BlockContainer) {
                $file = $test.Block.BlockContainer.Item.ResolvedTarget
            }
        }

        [pscustomobject]@{
            File         = $file
            Line         = $test.StartLine
            FullName     = $test.FullName
            ExpandedPath = $test.ExpandedPath
            Name         = $test.Name
            Result       = $test.Result
        }
    }

    return @(
        $failedSelectors |
            Group-Object -Property { '{0}|{1}|{2}' -f $_.File, $_.Line, $_.FullName } |
            ForEach-Object { $_.Group[0] }
    )
}

<#
.SYNOPSIS
    Extracts failed xUnit/VSTest selectors from a TRX result file.
.DESCRIPTION
    Reads the generated TRX report, maps failed results back to their test
    definitions, and returns stable selector objects that can be fed into a
    deferred `dotnet test --filter` rerun.
.PARAMETER TrxPath
    Path to the TRX result file.
.PARAMETER ProjectPath
    Test project path that produced the TRX file.
.PARAMETER Framework
    Target framework associated with the TRX file.
.PARAMETER Label
    Friendly label used in build output for the test target.
#>
function Get-KestrunTrxFailedSelector {
    [CmdletBinding()]
    [OutputType([object[]])]
    param(
        [Parameter(Mandatory = $true)]
        [string] $TrxPath,
        [Parameter()]
        [string] $ProjectPath,
        [Parameter()]
        [string] $Framework,
        [Parameter()]
        [string] $Label
    )

    if (-not (Test-Path -LiteralPath $TrxPath)) {
        throw "TRX result file not found: $TrxPath"
    }

    [xml] $trx = Get-Content -LiteralPath $TrxPath -Raw
    $namespaceManager = [System.Xml.XmlNamespaceManager]::new($trx.NameTable)
    $namespaceManager.AddNamespace('trx', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')

    $testsById = @{}
    foreach ($unitTest in @($trx.SelectNodes('//trx:TestDefinitions/trx:UnitTest', $namespaceManager))) {
        $testId = $unitTest.GetAttribute('id')
        if ([string]::IsNullOrWhiteSpace($testId)) {
            continue
        }

        $testMethod = $unitTest.GetElementsByTagName('TestMethod') | Select-Object -First 1
        $className = $null
        $methodName = $null
        if ($null -ne $testMethod) {
            $className = $testMethod.GetAttribute('className')
            $methodName = $testMethod.GetAttribute('name')
        }
        $fullyQualifiedName = if (-not [string]::IsNullOrWhiteSpace($className) -and -not [string]::IsNullOrWhiteSpace($methodName)) {
            "$className.$methodName"
        } else {
            $null
        }

        $testsById[$testId] = [pscustomobject]@{
            TestId             = $testId
            DisplayName        = $unitTest.GetAttribute('name')
            ClassName          = $className
            MethodName         = $methodName
            FullyQualifiedName = $fullyQualifiedName
        }
    }

    $failedSelectors = foreach ($result in @($trx.SelectNodes('//trx:Results/trx:UnitTestResult[@outcome="Failed"]', $namespaceManager))) {
        $testId = $result.GetAttribute('testId')
        $test = if ($testsById.ContainsKey($testId)) { $testsById[$testId] } else { $null }
        $className = if ($null -ne $test) { $test.ClassName } else { $null }
        $methodName = if ($null -ne $test) { $test.MethodName } else { $null }
        $fullyQualifiedName = if ($null -ne $test) { $test.FullyQualifiedName } else { $null }
        $displayName = if ($test -and -not [string]::IsNullOrWhiteSpace($test.DisplayName)) {
            $test.DisplayName
        } else {
            $result.GetAttribute('testName')
        }

        [pscustomobject]@{
            ProjectPath         = $ProjectPath
            Framework           = $Framework
            Label               = $Label
            TrxPath             = $TrxPath
            TestId              = $testId
            DisplayName         = $displayName
            ClassName           = $className
            MethodName          = $methodName
            FullyQualifiedName  = $fullyQualifiedName
            Outcome             = $result.GetAttribute('outcome')
            ErrorMessage        = $result.SelectSingleNode('trx:Output/trx:ErrorInfo/trx:Message', $namespaceManager)?.InnerText
        }
    }

    return @(
        $failedSelectors |
            Group-Object -Property {
                $selectorKey = if ([string]::IsNullOrWhiteSpace($_.FullyQualifiedName)) {
                    $_.DisplayName
                } else {
                    $_.FullyQualifiedName
                }
                '{0}|{1}|{2}' -f $_.ProjectPath, $_.Framework, $selectorKey
            } |
            ForEach-Object { $_.Group[0] }
    )
}

<#
.SYNOPSIS
    Creates a rerun Pester configuration for a failed-test subset.
.DESCRIPTION
    Reuses the base verbosity and platform exclusions while restricting the run
    to the failing files and FullName selectors collected from the prior pass.
.PARAMETER Failed
    Failing test selector objects from Get-KestrunPesterFailedSelector.
.PARAMETER BaseConfig
    Base configuration whose verbosity and excluded tags are reused.
.PARAMETER ResultsPath
    NUnit XML output path for the rerun.
.PARAMETER TestSuiteName
    Root test-suite name written to the rerun XML report.
#>
function New-KestrunPesterRerunConfig {
    [CmdletBinding()]
    [outputtype([PesterConfiguration])]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IEnumerable] $Failed,
        [Parameter(Mandatory = $true)]
        $BaseConfig,
        [Parameter(Mandatory = $true)]
        [string] $ResultsPath,
        [Parameter()]
        [string] $TestSuiteName = 'Kestrun Pester Rerun'
    )

    $cfg = [PesterConfiguration]::Default
    $cfg.Run.Path = @(
        $Failed |
            Where-Object { $_.File } |
            Select-Object -ExpandProperty File -Unique
    )
    $cfg.Output.Verbosity = $BaseConfig.Output.Verbosity
    $cfg.TestResult.Enabled = $true
    $cfg.TestResult.OutputFormat = 'NUnitXml'
    $cfg.TestResult.OutputPath = $ResultsPath
    $cfg.TestResult.TestSuiteName = $TestSuiteName
    $cfg.Run.Exit = $false
    $cfg.Run.PassThru = $true
    $cfg.Filter.ExcludeTag = $BaseConfig.Filter.ExcludeTag
    $cfg.Filter.FullName = @(
        $Failed |
            Where-Object { $_.FullName } |
            Select-Object -ExpandProperty FullName -Unique
    )

    return $cfg
}

<#
.SYNOPSIS
    Invokes Pester and returns both the result object and an exit code.
.PARAMETER Config
    Pester configuration to execute.
.OUTPUTS
    Hashtable containing the Pester run result and exit code.
#>
function Invoke-KestrunPesterWithConfig {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        $Config
    )

    $run = Invoke-Pester -Configuration $Config -ErrorAction Stop
    if ($null -eq $run) {
        throw 'Invoke-Pester did not return a run result.'
    }
    $code = if ($run.FailedCount -gt 0) { 1 } else { 0 }
    return @{
        Run      = $run
        ExitCode = $code
    }
}

Export-ModuleMember -Function *
