<#
.SYNOPSIS
    Build and stage Kestrun Tool ServiceHost payloads for supported PowerShell platforms.
.DESCRIPTION
    This script performs the following steps for each specified runtime identifier:
    1. Determines the Microsoft.PowerShell.SDK version from the ServiceHost project file.
    2. Retrieves the corresponding PowerShell release asset from GitHub.
    3. Caches the downloaded PowerShell SDK archive locally to avoid redundant downloads.
    4. Extracts only the 'Modules' content from the PowerShell SDK archive and stages it alongside the published ServiceHost binary.
.PARAMETER KestrunServiceHostProjectPath
    The file path to the Kestrun ServiceHost .csproj project file.
.PARAMETER Configuration
    The build configuration to use for publishing (e.g., 'Release').
.PARAMETER DotNetVerbosity
    The verbosity level to use for dotnet CLI output (e.g., 'minimal', 'normal', 'detailed').
.PARAMETER Version
    The version number to set for the published ServiceHost binary.
.PARAMETER InformationalVersion
    The informational version string to set for the published ServiceHost binary.
.PARAMETER RuntimeIdentifiers
    An array of runtime identifiers (RIDs) to target for publishing (e.g., 'win-x64', 'linux-x64', 'osx-x64').
.PARAMETER PublishRoot
    The root directory where the ServiceHost publish outputs will be staged.
.PARAMETER CacheRoot
    The directory to use for caching downloaded PowerShell SDK archives.
.PARAMETER ServiceHostRuntimesDirectory
    The directory where the final staged ServiceHost binaries and their corresponding PowerShell modules will be placed.
.EXAMPLE
    .\Build-KestrunTool.ps1 -KestrunServiceHostProjectPath '..\src\ServiceHost\ServiceHost.csproj' -Configuration 'Release' -DotNetVerbosity 'minimal' `
        -Version '1.0.0' -InformationalVersion '1.0.0+build.123' -RuntimeIdentifiers @('win-x64', 'linux-x64',
    'osx-x64') -PublishRoot '..\publish' -CacheRoot '..\cache' -ServiceHostRuntimesDirectory '..\runtimes'
    This command will build and stage the Kestrun ServiceHost for Windows, Linux, and macOS x64 platforms, using the specified version and informational version, while caching PowerShell SDK downloads
    in the '..\cache' directory and staging publish outputs in the '..\publish' directory.
    The final binaries and their corresponding PowerShell modules will be placed in the '..\runtimes' directory under subdirectories for each runtime identifier.
.NOTES
    - This script requires an active internet connection to retrieve PowerShell release information and assets from GitHub.
    The script also assumes that the specified project file contains a valid PackageReference to Microsoft.PowerShell
    .SDK with a version attribute. The script uses .NET's System.IO.Compression for handling ZIP files and System.Formats.Tar for handling TAR.GZ files,
    so it requires .NET 6 or later to run successfully. The script also provides console output to indicate the progress of the build and staging process,
    including any downloads or caching actions taken for the PowerShell SDK archives. If any step fails (e.g., unable to determine SDK version,
    download failures, publish failures), the script will throw an exception with a descriptive error message.
#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$KestrunServiceHostProjectPath,
    [Parameter(Mandatory = $true)]
    [string]$Configuration,
    [Parameter(Mandatory = $true)]
    [string]$DotNetVerbosity,
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$InformationalVersion,
    [Parameter(Mandatory = $true)]
    [string[]]$RuntimeIdentifiers,
    [Parameter(Mandatory = $true)]
    [string]$PublishRoot,
    [Parameter(Mandatory = $true)]
    [string]$CacheRoot,
    [Parameter(Mandatory = $true)]
    [string]$ServiceHostRuntimesDirectory
)

$ErrorActionPreference = 'Stop'
$script:PowerShellReleaseAssetCache = @{}

<#
.SYNOPSIS
    Retrieves the Microsoft.PowerShell.SDK version from the specified .csproj project file.
.DESCRIPTION
    This function loads the .csproj file as XML, searches for a PackageReference to Microsoft.PowerShell.SDK, and extracts the Version attribute from that reference. The version is returned as a string
.PARAMETER ProjectPath
    The file path to the .csproj project file to analyze.
.OUTPUTS
    A string representing the version of Microsoft.PowerShell.SDK referenced in the project file.
.EXAMPLE
    Get-PowerShellSdkVersionForServiceHost -ProjectPath '..\src\ServiceHost\ServiceHost.csproj'
    This command will parse the ServiceHost.csproj file and return the version of Microsoft.PowerShell.SDK that is referenced as a PackageReference in the project file.
.NOTES
    - The project file must contain a PackageReference to Microsoft.PowerShell.SDK with a valid Version attribute for this function to work correctly. If the version cannot be determined, an exception will be thrown.
#>
function Get-PowerShellSdkVersionForServiceHost {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    $project = [xml](Get-Content -Path $ProjectPath -Raw)
    $sdkReferences = $project.SelectNodes("//PackageReference[@Include='Microsoft.PowerShell.SDK']")

    $sdkVersion = $null
    foreach ($sdkReference in $sdkReferences) {
        $candidateVersion = $sdkReference.GetAttribute('Version')
        if ([string]::IsNullOrWhiteSpace($candidateVersion)) {
            $candidateVersion = $sdkReference.SelectSingleNode('Version')?.InnerText
        }

        if (-not [string]::IsNullOrWhiteSpace($candidateVersion)) {
            $sdkVersion = $candidateVersion
            break
        }
    }

    if ([string]::IsNullOrWhiteSpace($sdkVersion)) {
        throw "Unable to determine Microsoft.PowerShell.SDK version from $ProjectPath"
    }

    return [string]$sdkVersion
}

<#
.SYNOPSIS
    Builds GitHub request headers for API and download calls.
.DESCRIPTION
    Uses `GITHUB_TOKEN` (preferred) or `GH_TOKEN` when available to authenticate
    requests and avoid low anonymous API rate limits in CI.
.PARAMETER IncludeApiAccept
    Adds the GitHub API JSON `Accept` header when specified.
#>
function Get-GitHubRequestHeader {
    param(
        [Parameter(Mandatory = $false)]
        [switch]$IncludeApiAccept
    )

    $headers = @{
        'User-Agent' = 'kestrun-build'
    }

    $token = @($env:GITHUB_TOKEN, $env:GH_TOKEN) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1

    if (-not [string]::IsNullOrWhiteSpace($token)) {
        $headers['Authorization'] = "Bearer $token"
    }

    if ($IncludeApiAccept) {
        $headers['Accept'] = 'application/vnd.github+json'
    }

    return $headers
}

<#
.SYNOPSIS
    Detects whether an error looks like a GitHub API rate-limit failure.
.PARAMETER ErrorRecord
    The caught PowerShell error record.
#>
function Test-IsGitHubRateLimitError {
    param(
        [Parameter(Mandatory = $true)]
        [System.Management.Automation.ErrorRecord]$ErrorRecord
    )

    $combinedErrorText = @(
        [string]$ErrorRecord.Exception.Message,
        [string]$ErrorRecord.ErrorDetails.Message,
        [string]$ErrorRecord
    ) -join "`n"

    return $combinedErrorText -match '(?i)api rate limit exceeded|rate limit exceeded'
}

<#
.SYNOPSIS
    Retrieves the appropriate PowerShell release asset from GitHub for a given version and runtime identifier.
.DESCRIPTION
    This function queries the GitHub API for the PowerShell release corresponding to the specified version,
    then searches through the release assets to find the one that matches the expected naming convention for the given runtime identifier.
    The results are cached in-memory to optimize subsequent lookups for the same version.
.PARAMETER Version
    The version of PowerShell SDK to retrieve (e.g., '7.3.0').
.PARAMETER RuntimeIdentifier
    The runtime identifier (RID) for which to find the PowerShell SDK asset (e.g., 'win-x64', 'linux-x64', 'osx-x64').
.OUTPUTS
    An object representing the GitHub release asset that matches the specified version and runtime identifier.
.EXAMPLE
    Get-PowerShellReleaseAsset -Version '7.3.0' -RuntimeIdentifier 'win-x64'
    This command will query the GitHub API for the PowerShell release tagged 'v7.3.0' and return the asset that corresponds to the Windows x64 runtime,
    which is typically a .zip file named like 'PowerShell-7.3.0-win-x64.zip'.
.NOTES
    - This function uses the GitHub API to retrieve release information, so it requires an active internet connection and may be subject to GitHub's API rate limits. The results are cached in-memory
    to improve performance for repeated requests for the same version, but this cache is not persisted across script executions.
    If no matching asset is found for the specified version and runtime identifier, an exception will be thrown indicating the failure to locate the appropriate release asset.
#>
function Get-PowerShellReleaseAsset {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier
    )

    $archiveExtension = if ($RuntimeIdentifier -like 'win-*') { '.zip' } else { '.tar.gz' }
    $expectedName = "PowerShell-$Version-$RuntimeIdentifier$archiveExtension"

    if (-not $script:PowerShellReleaseAssetCache.ContainsKey($Version)) {
        $releaseApiUrl = "https://api.github.com/repos/PowerShell/PowerShell/releases/tags/v$Version"
        $headers = Get-GitHubRequestHeader -IncludeApiAccept

        try {
            $release = Invoke-RestMethod -Uri $releaseApiUrl -Headers $headers -ErrorAction Stop
            $script:PowerShellReleaseAssetCache[$Version] = @($release.assets)
        } catch {
            if (Test-IsGitHubRateLimitError -ErrorRecord $_) {
                Write-Warning "GitHub API rate limit reached while fetching PowerShell release metadata for v$Version. Falling back to direct release URL for $expectedName."
                return [pscustomobject]@{
                    name = $expectedName
                    browser_download_url = "https://github.com/PowerShell/PowerShell/releases/download/v$Version/$expectedName"
                }
            }

            throw
        }
    }

    $assets = $script:PowerShellReleaseAssetCache[$Version]

    $asset = $assets | Where-Object { $_.name -ieq $expectedName } | Select-Object -First 1
    if (-not $asset) {
        $asset = $assets |
            Where-Object { $_.name -ilike "*-$RuntimeIdentifier$archiveExtension" } |
            Select-Object -First 1
    }

    if (-not $asset) {
        throw "Unable to find PowerShell release asset for runtime '$RuntimeIdentifier' and version '$Version'."
    }

    return $asset
}

<#
.SYNOPSIS
    Tests for the presence of a PowerShell SDK release asset in the local cache, and downloads it if not already cached.
.DESCRIPTION
    This function checks if the PowerShell SDK archive for the specified version and runtime identifier is already present in the local cache directory. If it is not found, the function downloads the asset from the
    provided asset information and saves it to the cache directory. The path to the cached archive is returned at the end.
.PARAMETER Version
    The version of PowerShell SDK to check for and potentially download (e.g., '7.3.0').
.PARAMETER RuntimeIdentifier
    The runtime identifier (RID) for which to check the PowerShell SDK asset (e.g., 'win-x64', 'linux-x64', 'osx-x64').
.PARAMETER CacheRoot
    The root directory where downloaded PowerShell SDK archives should be cached.
.PARAMETER Asset
    The GitHub release asset object that contains information about the PowerShell SDK archive to be downloaded if it is not already cached.
.OUTPUTS
    A string representing the file path to the PowerShell SDK archive in the local cache, whether it was newly downloaded or already present.
.EXAMPLE
    Test-PowerShellReleaseArchiveInCache -Version '7.3.0' -RuntimeIdentifier 'win-x64' -CacheRoot '..\cache' -Asset $asset
    This command will check if the PowerShell SDK archive for version 7.3.0 and Windows x64 runtime is already present in the '..\cache' directory.
    If it is not found, it will download the archive from the URL specified in the $asset object and save it to the cache directory.
    The path to the cached archive will be returned at the end.
.NOTES
    - This function assumes that the $Asset parameter is a valid GitHub release asset object with 'name' and 'browser_download_url' properties. The function will create the cache directory if it
    does not already exist. The downloaded file will be saved with the same name as specified in the asset's 'name' property.
    If the download fails, an exception will be thrown. The function also provides console output to indicate whether it is downloading a new archive or reusing an existing cached archive.
#>
function Test-PowerShellReleaseArchiveInCache {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier,
        [Parameter(Mandatory = $true)]
        [string]$CacheRoot,
        [Parameter(Mandatory = $true)]
        [object]$Asset
    )

    if (-not (Test-Path -Path $CacheRoot)) {
        New-Item -Path $CacheRoot -ItemType Directory -Force | Out-Null
    }

    $archiveName = [string]$Asset.name
    $downloadUrl = [string]$Asset.browser_download_url
    $archivePath = Join-Path -Path $CacheRoot -ChildPath $archiveName

    if (-not (Test-Path -Path $archivePath)) {
        Write-Host "   ⬇️ Downloading PowerShell SDK archive for $RuntimeIdentifier ($Version)..." -ForegroundColor DarkCyan
        $downloadHeaders = Get-GitHubRequestHeader
        Invoke-WebRequest -Uri $downloadUrl -Headers $downloadHeaders -OutFile $archivePath -UseBasicParsing -ErrorAction Stop
    } else {
        Write-Host "   ♻️ Reusing cached PowerShell SDK archive for $RuntimeIdentifier ($Version)." -ForegroundColor DarkCyan
    }

    return $archivePath
}

<#
.SYNOPSIS
    Extracts the relative path of a module from a given archive entry path.
.DESCRIPTION
    This function takes an archive entry path and attempts to extract the relative path of a module within the archive. If the entry path does not correspond to a module, $null is returned.
.PARAMETER EntryPath
    The full path of the archive entry.
.OUTPUTS
    A string representing the relative path of the module within the archive, or $null if the entry is not a module.
.EXAMPLE
    Get-ModulesRelativePathFromArchiveEntry -EntryPath 'PowerShell-7.3.0-win-x64/Modules/Microsoft.PowerShell.Management/Microsoft.PowerShell.Management.psd1'
    This command will return 'Microsoft.PowerShell.Management/Microsoft.PowerShell.Management.psd1' as the relative path of the module within the archive entry path.
.NOTES
    - The function normalizes the entry path to use forward slashes and uses a regular expression to match paths that contain a 'Modules' directory, extracting the portion of the path that follows '
    Modules/'. If the entry path does not match this pattern, the function returns $null, indicating that it is not a module entry.
#>
function Get-ModulesRelativePathFromArchiveEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EntryPath
    )

    $normalized = $EntryPath.Replace('\\', '/')
    $modulesPrefixMatch = [regex]::Match($normalized, '(?i)(^|.*/)Modules/(?<relative>.*)$')
    if (-not $modulesPrefixMatch.Success) {
        return $null
    }

    return $modulesPrefixMatch.Groups['relative'].Value
}

<#
.SYNOPSIS
    Expands only the PowerShell modules from a given archive to a specified destination path.
.DESCRIPTION
    This function takes an archive path and a destination path, and extracts only the PowerShell modules from the archive to the destination path. It supports both ZIP and TAR.GZ archives.
.PARAMETER ArchivePath
    The full path to the archive file.
.PARAMETER DestinationModulesPath
    The path where the extracted modules should be placed.
.PARAMETER ArchiveName
    The name of the archive file.
.OUTPUTS
    This function does not return any output. It performs the extraction as a side effect. If the archive does not contain any modules, an exception is thrown.
.EXAMPLE
    Expand-PowerShellModulesOnlyFromArchive -ArchivePath '..\cache\PowerShell-7.3.0-win-x64.zip' -DestinationModulesPath '..\runtimes\win-x64\Modules' -ArchiveName 'PowerShell-7.3.0-win-x64.zip'
    This command will extract only the PowerShell modules from the specified ZIP archive and place them in the '..\runtimes\win-x64\Modules' directory.
    If the archive does not contain any modules, an exception will be thrown indicating that the 'Modules' content could not be located in the archive.
    The function supports both ZIP and TAR.GZ formats, so it will handle the extraction accordingly based on the file extension of the archive.
.NOTES
    - The function uses .NET's System.IO.Compression for ZIP files and System.Formats.Tar for TAR.GZ files, so it requires .NET 6 or later.
    The function also assumes that the archive structure contains a 'Modules' directory from which it will extract content. If
    no 'Modules' content is found in the archive, an exception is thrown to indicate this issue. The function also normalizes paths to ensure compatibility across different operating systems.
#>
function Expand-PowerShellModulesOnlyFromArchive {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArchivePath,
        [Parameter(Mandatory = $true)]
        [string]$DestinationModulesPath,
        [Parameter(Mandatory = $true)]
        [string]$ArchiveName
    )

    $foundModulesContent = $false

    if ($ArchivePath.EndsWith('.zip', [StringComparison]::OrdinalIgnoreCase)) {
        Add-Type -AssemblyName 'System.IO.Compression'
        Add-Type -AssemblyName 'System.IO.Compression.FileSystem'

        $zipArchive = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
        try {
            foreach ($entry in $zipArchive.Entries) {
                $relativePath = Get-ModulesRelativePathFromArchiveEntry -EntryPath $entry.FullName
                if ($null -eq $relativePath) {
                    continue
                }

                $foundModulesContent = $true
                if ([string]::IsNullOrEmpty($relativePath)) {
                    continue
                }

                $destinationPath = Join-Path -Path $DestinationModulesPath -ChildPath ($relativePath -replace '/', [IO.Path]::DirectorySeparatorChar)
                if ($entry.FullName.EndsWith('/')) {
                    New-Item -Path $destinationPath -ItemType Directory -Force | Out-Null
                    continue
                }

                $destinationDirectory = Split-Path -Path $destinationPath -Parent
                if (-not [string]::IsNullOrEmpty($destinationDirectory)) {
                    New-Item -Path $destinationDirectory -ItemType Directory -Force | Out-Null
                }

                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destinationPath, $true)
            }
        } finally {
            $zipArchive.Dispose()
        }
    } else {
        Add-Type -AssemblyName 'System.Formats.Tar'
        Add-Type -AssemblyName 'System.IO.Compression'

        $fileStream = [System.IO.File]::OpenRead($ArchivePath)
        $gzipStream = [System.IO.Compression.GZipStream]::new($fileStream, [System.IO.Compression.CompressionMode]::Decompress)
        $tarReader = [System.Formats.Tar.TarReader]::new($gzipStream)

        try {
            while ($null -ne ($entry = $tarReader.GetNextEntry())) {
                $relativePath = Get-ModulesRelativePathFromArchiveEntry -EntryPath $entry.Name
                if ($null -eq $relativePath) {
                    continue
                }

                $foundModulesContent = $true
                if ([string]::IsNullOrEmpty($relativePath)) {
                    continue
                }

                $destinationPath = Join-Path -Path $DestinationModulesPath -ChildPath ($relativePath -replace '/', [IO.Path]::DirectorySeparatorChar)
                if ($entry.EntryType -eq [System.Formats.Tar.TarEntryType]::Directory) {
                    New-Item -Path $destinationPath -ItemType Directory -Force | Out-Null
                    continue
                }

                if ($null -eq $entry.DataStream) {
                    continue
                }

                $destinationDirectory = Split-Path -Path $destinationPath -Parent
                if (-not [string]::IsNullOrEmpty($destinationDirectory)) {
                    New-Item -Path $destinationDirectory -ItemType Directory -Force | Out-Null
                }

                $targetFile = [System.IO.File]::Create($destinationPath)
                try {
                    $entry.DataStream.CopyTo($targetFile)
                } finally {
                    $targetFile.Dispose()
                }
            }
        } finally {
            $tarReader.Dispose()
            $gzipStream.Dispose()
            $fileStream.Dispose()
        }
    }

    if (-not $foundModulesContent) {
        throw "Unable to locate 'Modules' content in PowerShell archive '$ArchiveName'."
    }
}

<#
.SYNOPSIS
    Tests for the presence of a PowerShell SDK release asset in the local cache, downloads it if not already cached, and stages the PowerShell modules for a specific runtime.
.DESCRIPTION
    This function checks if the PowerShell SDK archive for the specified version and runtime identifier is already present in the local cache directory. If it is not found, the function downloads the asset from Git
Hub and saves it to the cache directory. Then, it extracts only the 'Modules' content from the PowerShell SDK archive and stages it in the appropriate location
under the destination root for the specified runtime identifier. If the archive does not contain any modules, an exception is thrown.
.PARAMETER Version
    The version of PowerShell SDK to check for and potentially download (e.g., '7.3.0').
.PARAMETER RuntimeIdentifier
    The runtime identifier (RID) for which to check the PowerShell SDK asset and stage modules (e.g., 'win-x64', 'linux-x64', 'osx-x64').
.PARAMETER DestinationRoot
    The root directory where the staged modules should be placed, typically the runtimes directory for the ServiceHost payloads.
.PARAMETER CacheRoot
    The directory to use for caching downloaded PowerShell SDK archives.
.OUTPUTS
    This function does not return any output. It performs the caching and staging as side effects. If the archive does not contain any modules, an exception is thrown.
.EXAMPLE
    Test-PowerShellModulesPayloadForRuntime -Version '7.3.0' -RuntimeIdentifier 'win-x64' -DestinationRoot '..\runtimes' -CacheRoot '..\cache'
    This command will check if the PowerShell SDK archive for version 7.3.0 and Windows x64 runtime is already present in the '..\cache' directory.
    If it is not found, it will download the archive from GitHub and save it to the cache directory. Then, it will extract only the 'Modules'
    content from the PowerShell SDK archive and stage it in the '..\runtimes\win-x64\Modules' directory.
    If the archive does not contain any modules, an exception will be thrown indicating that the 'Modules' content could not be located in the archive.
    The function supports both ZIP and TAR.GZ formats, so it will handle the extraction accordingly based on the file extension of the archive.
    The function also provides console output to indicate the staging location of the PowerShell modules for the specified runtime identifier.
.NOTES
    - This function assumes that the specified version and runtime identifier correspond to a valid PowerShell SDK release asset on GitHub.
    The function also assumes that the archive structure contains a 'Modules' directory
    from which it will extract content. If no 'Modules' content is found in the archive, an exception is thrown to indicate this issue.
    The function also normalizes paths to ensure compatibility across different operating systems.
    The function provides console output to indicate the staging location of the PowerShell modules for the specified runtime identifier.
#>
function Test-PowerShellModulesPayloadForRuntime {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [Parameter(Mandatory = $true)]
        [string]$RuntimeIdentifier,
        [Parameter(Mandatory = $true)]
        [string]$DestinationRoot,
        [Parameter(Mandatory = $true)]
        [string]$CacheRoot
    )

    $asset = Get-PowerShellReleaseAsset -Version $Version -RuntimeIdentifier $RuntimeIdentifier
    $archiveName = [string]$asset.name

    $archivePath = Test-PowerShellReleaseArchiveInCache -Version $Version -RuntimeIdentifier $RuntimeIdentifier -CacheRoot $CacheRoot -Asset $asset

    $runtimeDestinationRoot = Join-Path -Path $DestinationRoot -ChildPath $RuntimeIdentifier
    $modulesDestination = Join-Path -Path $runtimeDestinationRoot -ChildPath 'Modules'
    if (Test-Path -Path $modulesDestination) {
        Remove-Item -Path $modulesDestination -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -Path $modulesDestination -ItemType Directory -Force | Out-Null

    Expand-PowerShellModulesOnlyFromArchive -ArchivePath $archivePath -DestinationModulesPath $modulesDestination -ArchiveName $archiveName

    if (-not (Test-Path -Path (Join-Path -Path $modulesDestination -ChildPath 'Microsoft.PowerShell.Management'))) {
        throw "Unable to locate a valid Modules directory in archive '$archiveName'."
    }

    Write-Host "    ✅ Staged PowerShell modules to: $modulesDestination"
}

Write-Host '🔨 Publishing ServiceHost payloads for PowerShell-supported platforms...'

$powerShellSdkVersion = Get-PowerShellSdkVersionForServiceHost -ProjectPath $KestrunServiceHostProjectPath

if (Test-Path -Path $ServiceHostRuntimesDirectory) {
    Remove-Item -Path $ServiceHostRuntimesDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -Path $ServiceHostRuntimesDirectory -ItemType Directory -Force | Out-Null

foreach ($runtimeIdentifier in $RuntimeIdentifiers) {
    Write-Host "  - Publishing for runtime: $runtimeIdentifier" -ForegroundColor DarkCyan

    $serviceHostPublishPath = Join-Path -Path $PublishRoot -ChildPath "$runtimeIdentifier-service-host"
    if (Test-Path -Path $serviceHostPublishPath) {
        Remove-Item -Path $serviceHostPublishPath -Recurse -Force -ErrorAction SilentlyContinue
    }

    dotnet publish "$KestrunServiceHostProjectPath" -c $Configuration -r $runtimeIdentifier `
        --self-contained true /p:DebugSymbols=false /p:DebugType=None /p:Version=$Version /p:InformationalVersion=$InformationalVersion `
        -o "$serviceHostPublishPath" -v:$DotNetVerbosity
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for ServiceHost runtime '$runtimeIdentifier'."
    }

    $serviceHostPublishedBinaryCandidates = if ($runtimeIdentifier -like 'win-*') {
        @('Kestrun.ServiceHost.exe')
    } else {
        @('Kestrun.ServiceHost')
    }

    $serviceHostPublishedBinary = $null
    foreach ($candidateName in $serviceHostPublishedBinaryCandidates) {
        $candidatePath = Join-Path -Path $serviceHostPublishPath -ChildPath $candidateName
        if (Test-Path -Path $candidatePath) {
            $serviceHostPublishedBinary = $candidatePath
            break
        }
    }

    if (-not $serviceHostPublishedBinary) {
        throw "ServiceHost publish output not found. Checked: $($serviceHostPublishedBinaryCandidates -join ', ') in $serviceHostPublishPath"
    }

    $serviceHostDestinationBinaryName = if ($runtimeIdentifier -like 'win-*') { 'kestrun-service-host.exe' } else { 'kestrun-service-host' }
    $serviceHostDestinationRuntimeDirectory = Join-Path -Path $ServiceHostRuntimesDirectory -ChildPath $runtimeIdentifier
    if (-not (Test-Path -Path $serviceHostDestinationRuntimeDirectory)) {
        New-Item -Path $serviceHostDestinationRuntimeDirectory -ItemType Directory -Force | Out-Null
    }

    $serviceHostDestinationBinary = Join-Path -Path $serviceHostDestinationRuntimeDirectory -ChildPath $serviceHostDestinationBinaryName
    Copy-Item -Path $serviceHostPublishedBinary -Destination $serviceHostDestinationBinary -Force
    Write-Host "    ✅ Copied to: $serviceHostDestinationBinary"

    Test-PowerShellModulesPayloadForRuntime -Version $powerShellSdkVersion -RuntimeIdentifier $runtimeIdentifier -DestinationRoot $ServiceHostRuntimesDirectory -CacheRoot $CacheRoot
}

Write-Host '✅ ServiceHost payload staging completed for all configured runtimes.'
