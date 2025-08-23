﻿#requires -Module InvokeBuild
<#
    .SYNOPSIS
    Build script for Kestrun

    .DESCRIPTION
    This script contains the build tasks for the Kestrun project.

    .PARAMETER Configuration
    The build configuration to use (Debug or Release).

    .PARAMETER Release
    The release stage (Stable, ReleaseCandidate, Beta, Alpha).

    .PARAMETER Frameworks
    The target frameworks to build for.

    .PARAMETER Version
    The version of the Kestrun project.

    .PARAMETER Iteration
    The iteration of the Kestrun project.

    .PARAMETER FileVersion
    The file version to use.

    .PARAMETER PesterVerbosity
    The verbosity level for Pester tests.

    .PARAMETER DotNetVerbosity
    The verbosity level for .NET commands.

    .PARAMETER RunPesterInProcess
    Whether to run Pester tests in the same process.

    .EXAMPLE
    .\Kestrun.build.ps1 -Configuration Release -Frameworks net9.0 -Version 1.0.0
    This example demonstrates how to build the Kestrun project for the Release configuration,
    targeting the net9.0 framework, and specifying the version as 1.0.0.

    .EXAMPLE
    .\Kestrun.build.ps1 -Configuration Debug -Frameworks net8.0 -Version 1.0.0
    This example demonstrates how to build the Kestrun project for the Debug configuration,
    targeting the net8.0 framework, and specifying the version as 1.0.0.

    .NOTES
    This script is intended to be run with Invoke-Build.

#>
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', '')]
[CmdletBinding( DefaultParameterSetName = 'FileVersion')]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [Parameter(Mandatory = $false, ParameterSetName = 'Version')]
    [ValidateSet('Stable', 'ReleaseCandidate', 'Beta', 'Alpha')]
    [string]$Release = 'Beta',
    [Parameter(Mandatory = $false)]
    [ValidateSet('net8.0', 'net9.0', 'net10.0')]
    [string[]]$Frameworks = @('net8.0', 'net9.0'),
    [Parameter(Mandatory = $true, ParameterSetName = 'Version')]
    [string]$Version,
    [Parameter(Mandatory = $false, ParameterSetName = 'Version')]
    [string]$Iteration = '',
    [Parameter(Mandatory = $false, ParameterSetName = 'FileVersion')]
    [string]$FileVersion = './version.json',
    [Parameter(Mandatory = $false)]
    [string]
    [ValidateSet('None', 'Normal' , 'Detailed', 'Minimal')]
    $PesterVerbosity = 'Detailed',
    [Parameter(Mandatory = $false)]
    [string]
    [ValidateSet('quiet', 'minimal' , 'normal', 'detailed', 'diagnostic')]
    $DotNetVerbosity = 'detailed',
    [Parameter(Mandatory = $false)]
    [switch]$RunPesterInProcess
)

if (($null -eq $PSCmdlet.MyInvocation) -or ([string]::IsNullOrEmpty($PSCmdlet.MyInvocation.PSCommandPath)) -or (-not $PSCmdlet.MyInvocation.PSCommandPath.EndsWith('Invoke-Build.ps1'))) {
    Write-Host 'This script is intended to be run with Invoke-Build. ' -ForegroundColor Yellow
    Write-Host 'Please use Invoke-Build to execute the tasks defined in this script or Invoke-Build Help for more information.' -ForegroundColor Yellow
    return
}

# Add Helper utility
. ./Utility/Helper.ps1

$SolutionPath = Join-Path -Path $PSScriptRoot -ChildPath "Kestrun.sln"

# Load the InvokeBuild module
Add-BuildTask Default Help

Add-BuildTask Help {
    Write-Host 'Tasks in the Build Script:' -ForegroundColor DarkMagenta
    Write-Host
    Write-Host 'Primary Tasks:' -ForegroundColor Green
    Write-Host '- Default: Lists all available tasks.'
    Write-Host '- Help: Displays this help message.'
    Write-Host '- Clean: Cleans the solution.'
    Write-Host '- Restore: Restores NuGet packages.'
    Write-Host '- Build: Builds the solution.'
    Write-Host '- Test: Runs tests and Pester tests.'
    Write-Host '- All: Runs Clean, Build, and Test tasks in sequence.'
    Write-Host '-----------------------------------------------------'
    Write-Host 'Additional Tasks:' -ForegroundColor Green
    Write-Host '- Nuget-CodeAnalysis: Updates CodeAnalysis packages.'
    Write-Host '- Clean-CodeAnalysis: Cleans the CodeAnalysis packages.'
    Write-Host '- Kestrun.Tests: Runs Kestrun DLL tests.'
    Write-Host '- Test-Pester: Runs Pester tests.'
    Write-Host '- Kestrun.Tests: Runs Kestrun DLL tests.'
    Write-Host '- Package: Packages the solution.'
    Write-Host '- Manifest: Updates the Kestrun.psd1 manifest.'
    Write-Host '- Generate-LargeFile: Generates a large test file.'
    Write-Host '- Clean-LargeFile: Cleans the generated large test files.'
    Write-Host '- ThirdPartyNotices: Generates third-party notices.'
    Write-Host '- BuildHelp: Generates PowerShell help documentation.'
    Write-Host '- CleanHelp: Cleans the PowerShell help documentation.'
    Write-Host '- Install-Module: Installs the Kestrun module.'
    Write-Host '- Remove-Module: Removes the Kestrun module.'
    Write-Host '- Update-Module: Updates the Kestrun module.'
    Write-Host '- Format: Formats the codebase.'
    Write-Host '- Coverage: Generates code coverage reports.'
    Write-Host '- Report-Coverage: Generates code coverage report webpage.'
    Write-Host '- Clean-Coverage: Cleans the code coverage reports.'
}

Add-BuildTask 'Clean' 'Clean-CodeAnalysis', 'CleanHelp', {
    Write-Host 'Cleaning solution...'
    foreach ($framework in $Frameworks) {
        dotnet clean "$SolutionPath" -c $Configuration -f $framework -v:$DotNetVerbosity
    }
}
Add-BuildTask 'Restore' {
    Write-Host 'Restore Packages'
    dotnet restore "$SolutionPath" -v:$DotNetVerbosity
}, 'Nuget-CodeAnalysis'

Add-BuildTask 'BuildNoPwsh' {
    Write-Host 'Building solution...'

    if ($PSCmdlet.ParameterSetName -eq 'FileVersion') {
        $Version = Get-Version -FileVersion $FileVersion
    } elseif ($PSCmdlet.ParameterSetName -eq 'Version') {
        if (-not (Test-Path -Path $FileVersion)) {
            [ordered]@{
                Version = $Version
                Release = $Release
                Iteration = $Iteration
            } | ConvertTo-Json | Set-Content -Path $FileVersion
        }
        $Version = Get-Version -FileVersion $FileVersion
    } else {
        throw "Invalid parameter set. Use either 'FileVersion' or 'Version'."
    }

    foreach ($framework in $Frameworks) {
        dotnet build "$SolutionPath" -c $Configuration -f $framework -v:$DotNetVerbosity -p:Version=$Version -p:InformationalVersion=$InformationalVersion
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed for framework $framework"
        }
    }
}

Add-BuildTask 'Build' 'BuildNoPwsh', 'SyncPowerShellDll', {}

Add-BuildTask 'SyncPowerShellDll' {
    $dest = ".\src\PowerShell\Kestrun\lib"
    $src = ".\src\CSharp\Kestrun\bin\$Configuration"
    Write-Host "Preparing to copy files from $src to $dest"
    foreach ($framework in $Frameworks) {
        $destFramework = Join-Path -Path $dest -ChildPath $framework
        $srcFramework = Join-Path -Path $src -ChildPath $framework
        Write-Host "Copy dll from $srcFramework to $destFramework"
        if (Test-Path -Path $destFramework) {
            Remove-Item -Path $destFramework -Recurse -Force | Out-Null
        }
        New-Item -Path $destFramework -ItemType Directory -Force | Out-Null
        Copy-Item -Path "$srcFramework\*" -Destination $destFramework -Recurse -Force
    }
}

Add-BuildTask 'Nuget-CodeAnalysis' {
    Write-Host 'Update CodeAnalysis...'
    & .\Utility\Download-CodeAnalysis.ps1
}

Add-BuildTask 'Clean-CodeAnalysis' {
    Write-Host 'Cleaning CodeAnalysis...'
    Remove-Item -Path './src/PowerShell/Kestrun/lib/Microsoft.CodeAnalysis/' -Force -Recurse -ErrorAction SilentlyContinue
}

Add-BuildTask 'Kestrun.Tests' {
    Write-Host 'Running Kestrun DLL tests...'
    $failures = @()
    foreach ($framework in $Frameworks) {
        Write-Host "Running tests for $framework"
        dotnet test "$SolutionPath" -c $Configuration -f $framework -v:$DotNetVerbosity
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Tests failed for $framework" -ForegroundColor Red
            $failures += $framework
        }
    }
    if ($failures.Count -gt 0) {
        throw "Kestrun.Tests failed for frameworks: $($failures -join ', ')"
    }
}

Add-BuildTask 'Format' {
    Write-Host 'Formatting code...'
    dotnet format "$SolutionPath" -v:$DotNetVerbosity
}

Add-BuildTask 'Test-Pester' {

    Import-Module Pester -Force
    $cfg = [PesterConfiguration]::Default
    $cfg.Run.Path = @("$($PWD.Path)/tests/PowerShell.Tests")
    $cfg.Output.Verbosity = $PesterVerbosity
    $cfg.TestResult.Enabled = $true
    $cfg.Run.Exit = $true

    $excludeTag = @()
    if ($IsLinux) { $excludeTag += 'Exclude_Linux' }
    if ($IsMacOS) { $excludeTag += 'Exclude_MacOs' }
    if ($IsWindows) { $excludeTag += 'Exclude_Windows' }
    $cfg.Filter.ExcludeTag = $excludeTag

    if ($RunPesterInProcess) {
        Invoke-Pester -Configuration $cfg
        # Check exit code
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Some tests failed"
            exit $LASTEXITCODE
        } else {
            Write-Host "✅ All tests passed"
        }
    } else {
        $json = $cfg | ConvertTo-Json -Depth 10
        $child = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "run-pester-$([guid]::NewGuid()).ps1"
        @'
param([string]$ConfigJson)
Import-Module Pester -Force
$hash = $ConfigJson | ConvertFrom-Json -AsHashtable
$cfg = New-PesterConfiguration -Hashtable $hash
Invoke-Pester -Configuration $cfg
'@ | Set-Content -Path $child -Encoding UTF8

        try {
            pwsh -NoProfile -File $child -ConfigJson $json
        } finally {
            Remove-Item $child -ErrorAction SilentlyContinue
        }
    }
}

Add-BuildTask 'Test' 'Kestrun.Tests', 'Test-Pester'

Add-BuildTask 'Package' 'Build', {
    Write-Host 'Packaging the solution...'
    foreach ($framework in $Frameworks) {
        dotnet pack "$SolutionPath" -c $Configuration -f $framework -v:$DotNetVerbosity -p:Version=$Version -p:InformationalVersion=$InformationalVersion --no-build
    }
}


Add-BuildTask 'Build_Powershell_Help' {
    Write-Host 'Generate Powershell Help...'
    pwsh -NoProfile -File .\Utility\Generate-Help.ps1
}

Add-BuildTask 'Build_CSharp_Help' {
    Write-Host 'Generate C# Help...'
    # Check if xmldocmd is in PATH
    if (-not (Get-Command xmldocmd -ErrorAction SilentlyContinue)) {
        Write-Host '📦 Installing xmldocmd...'
        dotnet tool install -g xmldocmd
    } else {
        Write-Host '✅ xmldocmd already installed'
    }
    & .\Utility\Prepare-DocRefs.ps1
    & .\Utility\Prepare-JustTheDocs.ps1 -ApiRoot 'docs/cs/api' -TopParent 'C# API'
}

# Build Help will call Build_Powershell_Help and Build_CSharp_Help
Add-BuildTask 'BuildHelp' {
    Write-Host 'Generate Help...'
}, 'Build_Powershell_Help', 'Build_CSharp_Help'

# Clean Help will call Clean_Powershell_Help and Clean_CSharp_Help
Add-BuildTask 'CleanHelp' {
    Write-Host 'Cleaning Help...'
}, 'Clean_Powershell_Help', 'Clean_CSharp_Help'

# Clean PowerShell Help
Add-BuildTask 'Clean_Powershell_Help' {
    Write-Host 'Cleaning Powershell Help...'
    & .\Utility\Generate-Help.ps1 -Clean
}

# Clean CSharp Help
Add-BuildTask 'Clean_CSharp_Help' {
    Write-Host 'Cleaning C# Help...'
    & .\Utility\Prepare-DocRefs.ps1 -Clean
}

# Code Coverage
Add-BuildTask 'Coverage' {
    Write-Host 'Creating coverage report...'
    & .\Utility\Generate-Coverage.ps1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Coverage generation failed" -ForegroundColor Red
        throw "Coverage generation failed"
    }
}

# Report coverage
Add-BuildTask 'Report-Coverage' {
    Write-Host 'Creating coverage report webpage...'
    & .\Utility\Generate-Coverage.ps1 -ReportGenerator
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Coverage Report generation failed" -ForegroundColor Red
        throw "Coverage Report generation failed"
    }
}

# Clean coverage reports
Add-BuildTask 'Clean-Coverage' {
    Write-Host 'Cleaning coverage report...'
    & .\Utility\Generate-Coverage.ps1 -Clean
}

# Update the module manifest
Add-BuildTask 'Manifest' {
    Write-Host 'Updating Kestrun.psd1 manifest...'
    pwsh -NoProfile -File .\Utility\Update-Manifest.ps1
}

Add-BuildTask 'Generate-LargeFile' 'Clean-LargeFile', {
    Write-Host 'Generating large file...'
    if (-not (Test-Path -Path '.\examples\files\LargeFiles')) {
        New-Item -ItemType Directory -Path '.\examples\files\LargeFiles' -Force | Out-Null
    }
    (10, 100, 1000, 3000) | ForEach-Object {
        $sizeMB = $_
        & .\Utility\Generate-LargeFile.ps1 -Path ".\examples\files\LargeFiles\file-$sizeMB-MB.bin" -Mode 'Binary' -SizeMB $sizeMB
        & .\Utility\Generate-LargeFile.ps1 -Path ".\examples\files\LargeFiles\file-$sizeMB-MB.txt" -Mode 'Text' -SizeMB $sizeMB
    }
}
Add-BuildTask 'Clean-LargeFile' {
    Write-Host 'Cleaning large files...'
    Remove-Item -Path '.\examples\files\LargeFiles\*' -Force
}

Add-BuildTask 'ThirdPartyNotices' {
    & .\Utility\Generate-ThirdPartyNotices.ps1 -Project '.\src\CSharp\Kestrun\Kestrun.csproj' -Path '.\THIRD-PARTY-NOTICES.md' -Version (Get-Version -FileVersion $FileVersion)
}

Add-BuildTask All 'Clean', 'Restore', 'Build', 'Test'

Add-BuildTask Install-Module {
    Write-Host 'Installing Kestrun module...'
    & .\Utility\Install-Kestrun.ps1 -FileVersion $FileVersion
}

Add-BuildTask Remove-Module {
    Write-Host 'Removing Kestrun module...'
    & .\Utility\Install-Kestrun.ps1 -FileVersion $FileVersion -Remove
}
