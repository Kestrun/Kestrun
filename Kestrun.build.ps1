#requires -Module InvokeBuild
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

.PARAMETER AnnotationFramework
    The target framework for the Kestrun.Annotations project.

.PARAMETER Version
    The version of the Kestrun project.

.PARAMETER Iteration
    The iteration of the Kestrun project.

.PARAMETER FileVersion
    The file version to use.

.PARAMETER PesterVerbosity
    The verbosity level for Pester tests.

.PARAMETER DotNetVerbosity
    The verbosity level for .NET commands. Valid values are 'quiet', 'minimal', 'normal', 'detailed', and 'diagnostic'.

.PARAMETER SignModule
    Indicates whether to sign the module during the build process.

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
    [Parameter(Mandatory = $false)]
    [ValidateSet('net8.0', 'net9.0', 'net10.0')]
    [string] $AnnotationFramework = 'net8.0',
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
    [switch]$SignModule
)

if (($null -eq $PSCmdlet.MyInvocation) -or ([string]::IsNullOrEmpty($PSCmdlet.MyInvocation.PSCommandPath)) -or (-not $PSCmdlet.MyInvocation.PSCommandPath.EndsWith('Invoke-Build.ps1'))) {
    Write-Host '⚠️ This script is intended to be run with Invoke-Build. ' -ForegroundColor Yellow
    Write-Host 'ℹ️ Please use Invoke-Build to execute the tasks defined in this script or Invoke-Build Help for more information.' -ForegroundColor Yellow
    return
}

# Add Helper utility module
Import-Module -Name './Utility/Modules/Helper.psm1'

# Quiet env handling with optional verbose debug
$krDebug = -not [string]::IsNullOrWhiteSpace($env:KR_DEBUG_UPSTASH) -and ($env:KR_DEBUG_UPSTASH -in @('1', 'true', 'True'))
$isDebug = ($env:ACTIONS_STEP_DEBUG -eq 'true' -or $krDebug)

if ($isDebug) {
    # Verbose diagnostics (only when debug is enabled)
    Write-Host '🔍 [BUILD DEBUG] Checking UPSTASH_REDIS_URL in build script...' -ForegroundColor Cyan
    $upstashValue = [System.Environment]::GetEnvironmentVariable('UPSTASH_REDIS_URL')
    if ($upstashValue) {
        if ([string]::IsNullOrWhiteSpace($upstashValue)) {
            Write-Host "⚠️ UPSTASH_REDIS_URL is set but empty/whitespace (length: $($upstashValue.Length))" -ForegroundColor Yellow
            Write-Host "⚠️ Value: '$upstashValue'" -ForegroundColor Yellow
        } else {
            Write-Host "✅ UPSTASH_REDIS_URL is set in build script (length: $($upstashValue.Length))" -ForegroundColor Green
            Write-Host "✅ UPSTASH_REDIS_URL starts with: $($upstashValue.Substring(0, [Math]::Min(20, $upstashValue.Length)))..." -ForegroundColor Green
        }
    } else {
        Write-Host '❌ UPSTASH_REDIS_URL is NOT set in build script' -ForegroundColor Red
        if (Test-Path '.env.json') {
            Write-Host '🔄 Attempting to load .env.json...' -ForegroundColor Yellow
            try {
                . ./Utility/Import-EnvFile.ps1 -Path '.env.json' -Overwrite
                $upstashAfterLoad = [System.Environment]::GetEnvironmentVariable('UPSTASH_REDIS_URL')
                if ($upstashAfterLoad -and -not [string]::IsNullOrWhiteSpace($upstashAfterLoad)) {
                    Write-Host "✅ UPSTASH_REDIS_URL loaded from .env.json (length: $($upstashAfterLoad.Length))" -ForegroundColor Green
                    Write-Host "✅ UPSTASH_REDIS_URL starts with: $($upstashAfterLoad.Substring(0, [Math]::Min(20, $upstashAfterLoad.Length)))..." -ForegroundColor Green
                } else {
                    Write-Host '❌ UPSTASH_REDIS_URL not found or empty in .env.json' -ForegroundColor Red
                }
            } catch {
                Write-Host "❌ Failed to load .env.json: $($_.Exception.Message)" -ForegroundColor Red
            }
        } else {
            Write-Host '❌ .env.json file not found' -ForegroundColor Red
        }
    }
    Write-Host '🔍 All environment variables containing UPSTASH in build script:' -ForegroundColor Cyan
    Get-ChildItem env: | Where-Object Name -Like '*UPSTASH*' | ForEach-Object {
        $value = $_.Value
        if ([string]::IsNullOrWhiteSpace($value)) {
            Write-Host "  $($_.Name) = [EMPTY/WHITESPACE] (length: $($value.Length))" -ForegroundColor Red
        } else {
            Write-Host "  $($_.Name) = $($value.Substring(0, [Math]::Min(20, $value.Length)))... (length: $($value.Length))" -ForegroundColor Yellow
        }
    }
} else {
    # Silent hydration: best-effort import without noisy logs
    $upstashValue = [System.Environment]::GetEnvironmentVariable('UPSTASH_REDIS_URL')
    if ([string]::IsNullOrWhiteSpace($upstashValue) -and (Test-Path '.env.json')) {
        try { . ./Utility/Import-EnvFile.ps1 -Path '.env.json' -Overwrite }
        catch { Write-Information "⚠️ Failed to silently load .env.json: $($_.Exception.Message)" -InformationAction SilentlyContinue }
    }
}

$SolutionPath = Join-Path -Path $PSScriptRoot -ChildPath 'Kestrun.sln'
$KestrunProjectPath = Join-Path -Path $PSScriptRoot -ChildPath 'src/CSharp/Kestrun/Kestrun.csproj'
$KestrunAnnotationsProjectPath = Join-Path -Path $PSScriptRoot -ChildPath 'src/CSharp/Kestrun.Annotations/Kestrun.Annotations.csproj'
$ExamplesSolutionFilter = Join-Path -Path $PSScriptRoot -ChildPath 'Examples.slnf'

Write-Host '---------------------------------------------------' -ForegroundColor DarkCyan
if (-not $Version) {
    if ($PSCmdlet.ParameterSetName -eq 'FileVersion') {
        $VersionDetails = Get-Version -FileVersion $FileVersion -Details
    } elseif ($PSCmdlet.ParameterSetName -eq 'Version') {
        if (-not (Test-Path -Path $FileVersion)) {
            [ordered]@{
                Version = $Version
                Release = $Release
                Iteration = $Iteration
            } | ConvertTo-Json | Set-Content -Path $FileVersion
        }
        $VersionDetails = Get-Version -FileVersion $FileVersion -Details
    } else {
        throw "Invalid parameter set. Use either 'FileVersion' or 'Version'."
    }
    $Version = $VersionDetails.FullVersion
}
# Display the Kestrun build version
if ($VersionDetails.Prerelease) {
    Write-Host "Kestrun Build: v$Version (Pre-release)" -ForegroundColor DarkCyan
} else {
    if ($Version -eq '0.0.0') {
        Write-Host 'Kestrun Build: [Development Version]' -ForegroundColor DarkCyan
    } else {
        Write-Host "Kestrun Build: v$Version" -ForegroundColor DarkCyan
    }
}

# Display the current UTC time in a readable format
$utcTime = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
Write-Host "Start Time: $utcTime" -ForegroundColor DarkCyan

Write-Host '---------------------------------------------------' -ForegroundColor DarkCyan

# Load the InvokeBuild module
Add-BuildTask Default Help

Add-BuildTask Help {
    Write-Host '📘 Tasks in the Build Script:' -ForegroundColor DarkMagenta
    Write-Host
    Write-Host '🟩 Primary Tasks:' -ForegroundColor Green
    Write-Host '- Default: Lists all available tasks.'
    Write-Host '- Help: Displays this help message.'
    Write-Host '- Clean: Cleans the solution.'
    Write-Host '- Restore: Restores NuGet packages.'
    Write-Host '- Build: Builds the solution.'
    Write-Host '- Test: Runs tests and Pester tests.'
    Write-Host '- Package: Packages the solution.'
    Write-Host '- All: Runs Clean, Build, and Test tasks in sequence.'
    Write-Host '-----------------------------------------------------'
    Write-Host '🧩 Additional Tasks:' -ForegroundColor Green
    Write-Host '- Nuget-CodeAnalysis: Updates CodeAnalysis packages.'
    Write-Host '- Clean-CodeAnalysis: Cleans the CodeAnalysis packages.'
    Write-Host '- Test-xUnit: Runs Kestrun DLL tests.'
    Write-Host '- Test-Pester: Runs Pester tests.'
    Write-Host '- Manifest: Updates the Kestrun.psd1 manifest.'
    Write-Host '- New-LargeFile: Generates a large test file.'
    Write-Host '- Clean-LargeFile: Cleans the generated large test files.'
    Write-Host '- ThirdPartyNotices: Generates third-party notices.'
    Write-Host '- Build-Help: Generates PowerShell help documentation.'
    Write-Host '- Clean-Help: Cleans the PowerShell help documentation.'
    Write-Host '- Install-Module: Installs the Kestrun module.'
    Write-Host '- Remove-Module: Removes the Kestrun module.'
    Write-Host '- Update-Module: Updates the Kestrun module.'
    Write-Host '- Format: Formats the codebase.'
    Write-Host '- Coverage: Generates code coverage reports.'
    Write-Host '- Report-Coverage: Generates code coverage report webpage.'
    Write-Host '- Clean-Coverage: Cleans the code coverage reports.'
    Write-Host '- Normalize-LineEndings: Normalizes line endings to LF in .ps1, .psm1, and .cs files.'
    Write-Host '- Test-Tutorials: Runs tests on tutorial documentation.'
    Write-Host '- Deep-Clean: Cleans all build artifacts.'
    Write-Host '- Clean-Package: Cleans the package output directories.'
    Write-Host '-----------------------------------------------------'
}

Add-BuildTask 'Clean' 'Clean-CodeAnalysis', 'Clean-Help', 'Clean-Dotnet', 'Clean-PowerShellLib', {
    Write-Host '✅ Clean completed.'
}

Add-BuildTask 'Clean-PowerShellLib' {
    Write-Host '🧹 Cleaning PowerShell library...'
    if (Test-Path -Path './src/PowerShell/Kestrun/lib') {
        Remove-Item -Recurse -Force './src/PowerShell/Kestrun/lib' -ErrorAction SilentlyContinue
    }
    Write-Host '✅ PowerShell library Clean completed.'
}

Add-BuildTask 'Clean-Dotnet' {
    Write-Host '🧹 Cleaning solutions...'
    dotnet clean "$SolutionPath" -c $Configuration -v:$DotNetVerbosity
    Write-Host '✅ Solutions Clean completed.'
}

Add-BuildTask 'CleanObj' {
    Write-Host '🧹 Cleaning obj folders...'
    if (Test-Path -Path '.\src\CSharp\Kestrun.Annotations\obj') {
        Remove-Item -Recurse -Force '.\src\CSharp\Kestrun.Annotations\obj' -ErrorAction SilentlyContinue
    }
    if (Test-Path -Path '.\src\CSharp\Kestrun\obj') {
        Remove-Item -Recurse -Force '.\src\CSharp\Kestrun\obj' -ErrorAction SilentlyContinue
    }
    Write-Host '✅ Obj clean completed.'
}
Add-BuildTask 'CleanBin' {
    Write-Host '🧹 Cleaning bin folders...'
    if (Test-Path -Path '.\src\CSharp\Kestrun.Annotations\bin') {
        Remove-Item -Recurse -Force '.\src\CSharp\Kestrun.Annotations\bin' -ErrorAction SilentlyContinue
    }
    if (Test-Path -Path '.\src\CSharp\Kestrun\bin') {
        Remove-Item -Recurse -Force '.\src\CSharp\Kestrun\bin' -ErrorAction SilentlyContinue
    }
    Write-Host '✅ Bin clean completed.'
}

Add-BuildTask 'Clean-Package' {
    Write-Host '🧼 Clearing previous package artifacts...'
    $out = Join-Path -Path $PWD -ChildPath 'artifacts'
    if (Test-Path -Path $out) {
        Remove-Item -Path $out -Recurse -Force -ErrorAction Stop
    }
    Write-Host '✅ Package clean completed.'
}

Add-BuildTask 'Clean-CodeAnalysis' {
    Write-Host '🧼 Cleaning CodeAnalysis packages...'
    if (Test-Path -Path './src/PowerShell/Kestrun/lib/Microsoft.CodeAnalysis/') {
        Remove-Item -Path './src/PowerShell/Kestrun/lib/Microsoft.CodeAnalysis/' -Force -Recurse -ErrorAction SilentlyContinue
    }
    Write-Host '✅ CodeAnalysis clean completed.'
}

Add-BuildTask 'Deep-Clean' 'Clean', 'CleanObj', 'CleanBin' , 'Clean-Package', {
    Write-Host '🧼 Deep cleaning completed.'
}

Add-BuildTask 'Restore' {
    Write-Host '📦 Restoring packages...'
    dotnet restore "$SolutionPath" -v:$DotNetVerbosity
}, 'Nuget-CodeAnalysis'

Add-BuildTask 'BuildNoPwsh' {
    if (Get-Module -Name Kestrun) {
        throw 'Kestrun module is currently loaded in this PowerShell session. Please close all sessions using the Kestrun module before building.'
    }
    Write-Host '🔨 Building solution...'

    Write-Host "Building Kestrun.Annotations for single framework: $AnnotationFramework" -ForegroundColor DarkCyan
    dotnet build "$KestrunAnnotationsProjectPath" -c $Configuration -f $AnnotationFramework -v:$DotNetVerbosity -p:Version=$Version -p:InformationalVersion=$VersionDetails.InformationalVersion
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for Kestrun.Annotations project for framework $AnnotationFramework"
    }
    # Build Kestrun project for each specified framework
    $enableNet10 = ($Frameworks -contains 'net10.0')
    if ($Frameworks.Count -eq 1) {
        $framework = $Frameworks[0]
        Write-Host "Building Kestrun for single framework: $framework)" -ForegroundColor DarkCyan
        dotnet build "$KestrunProjectPath" -c $Configuration -f $framework -v:$DotNetVerbosity -p:Version=$Version -p:InformationalVersion=$VersionDetails.InformationalVersion -p:EnableNet10=$enableNet10
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed for Kestrun project for framework $framework"
        }
    } else {
        Write-Host "Building Kestrun for multiple frameworks: $($Frameworks -join ', ')" -ForegroundColor DarkCyan
        dotnet build "$KestrunProjectPath" -c $Configuration -v:$DotNetVerbosity -p:Version=$Version -p:InformationalVersion=$VersionDetails.InformationalVersion -p:EnableNet10=$enableNet10
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed for Kestrun project for framework $framework"
        }
    }
}

Add-BuildTask 'BuildExamples' {
    if (Get-Module -Name Kestrun) {
        throw 'Kestrun module is currently loaded in this PowerShell session. Please close all sessions using the Kestrun module before building.'
    }
    Write-Host '🔨 Building solution...'
    if ($Frameworks.Count -eq 1) {

        Write-Host "Building for single framework: $($Frameworks[0])" -ForegroundColor DarkCyan
        dotnet build "$ExamplesSolutionFilter" -c $Configuration -f $framework -v:$DotNetVerbosity -p:Version=$Version -p:InformationalVersion=$VersionDetails.InformationalVersion
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed for framework $framework"
        }
    } else {
        Write-Host "Building Kestrun.Annotations for multiple frameworks: $($Frameworks -join ', ')" -ForegroundColor DarkCyan
        dotnet build "$ExamplesSolutionFilter" -c $Configuration -v:$DotNetVerbosity -p:Version=$Version -p:InformationalVersion=$VersionDetails.InformationalVersion
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed for Kestrun.Annotations project for framework $framework"
        }
    }
}

Add-BuildTask 'Build' 'BuildNoPwsh', 'SyncPowerShellDll', { Write-Host '🚀 Build completed.' }

Add-BuildTask 'SyncPowerShellDll' {
    Write-Host '🔄 Syncing PowerShell DLLs to src/PowerShell/Kestrun/lib...'
    Sync-PowerShellDll -Configuration $Configuration -Frameworks $Frameworks -dest '.\src\PowerShell\Kestrun\lib'
    Write-Host '🚀 PowerShell DLL synchronization completed.'
}

Add-BuildTask 'Nuget-CodeAnalysis' {
    Write-Host '♻️ Updating CodeAnalysis packages...'
    & .\Utility\Download-CodeAnalysis.ps1
}

# XUnit tests
Add-BuildTask 'Test-xUnit' {
    Write-Host '🧪 Running Kestrun DLL tests...'
    $failures = @()
    foreach ($framework in $Frameworks) {
        Write-Host "▶️ Running tests for $framework"
        dotnet test "$SolutionPath" -c $Configuration -f $framework -v:$DotNetVerbosity
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Tests failed for $framework" -ForegroundColor Red
            $failures += $framework
        }
    }
    if ($failures.Count -gt 0) {
        throw "Test-xUnit failed for frameworks: $($failures -join ', ')"
    }
}

# Formatting source code
Add-BuildTask 'Format' {
    Write-Host '✨ Formatting code...'
    dotnet format "$SolutionPath" -v:$DotNetVerbosity
    & .\Utility\Normalize-Files.ps1 `
        -Root (Join-Path -Path $PSScriptRoot -ChildPath 'src') `
        -ReformatFunctionHelp -FunctionHelpPlacement BeforeFunction -NoFooter -UseGitForCreated
}

# Pester tests
Add-BuildTask 'Test-Pester' {
    if ($isDebug) {
        Write-Host '🔍 [TEST-PESTER DEBUG] Checking UPSTASH_REDIS_URL before running Pester tests...' -ForegroundColor Cyan
        $upstashValue = [System.Environment]::GetEnvironmentVariable('UPSTASH_REDIS_URL')
        if ($upstashValue) {
            if ([string]::IsNullOrWhiteSpace($upstashValue)) {
                Write-Host "⚠️ UPSTASH_REDIS_URL is set but empty/whitespace for Pester tests (length: $($upstashValue.Length))" -ForegroundColor Yellow
                Write-Host "⚠️ Value: '$upstashValue'" -ForegroundColor Yellow
            } else {
                Write-Host "✅ UPSTASH_REDIS_URL is available for Pester tests (length: $($upstashValue.Length))" -ForegroundColor Green
                Write-Host "✅ UPSTASH_REDIS_URL starts with: $($upstashValue.Substring(0, [Math]::Min(20, $upstashValue.Length)))..." -ForegroundColor Green
            }
        } else {
            Write-Host '❌ UPSTASH_REDIS_URL is NOT available for Pester tests' -ForegroundColor Red
        }
    }
    $res = & .\Utility\Test-Pester.ps1 -ReRunFailed -Verbosity $PesterVerbosity
    if ($res -ne 0) { Write-Error "Test-Pester failed with exit code $res" }
    return $res
}

Add-BuildTask 'Test' 'Test-xUnit', 'Test-Pester'

Add-BuildTask 'Test-Tutorials' {
    Write-Host '🧪 Running Kestrun Tutorial tests...'
    & .\Utility\Test-TutorialDocs.ps1
}

Add-BuildTask 'Package' 'Clean-Package', 'Build', {
    Write-Host '🚀 Starting release build...'
    $script:Configuration = 'Release'

    # Retrieve the short commit SHA from Git
    #$commit = (git rev-parse --short HEAD).Trim()
    # $InformationalVersion = "$($Version)+$commit"

    $out = Join-Path -Path $PWD -ChildPath 'artifacts'

    if ( (Test-Path -Path $out)) {
        Write-Host "🗑️ Cleaning existing artifacts at $out ..."
        Remove-Item -Path $out -Recurse -Force
    }
    New-Item -Path $out -ItemType Directory -Force | Out-Null
    $kestrunReleasePath = Join-Path -Path $out -ChildPath 'modules' -AdditionalChildPath 'Kestrun'

    & .\Utility\Create-Distribution.ps1 -SignModule:$SignModule -ArtifactsPath $out
    if ($LASTEXITCODE -ne 0) {
        Write-Host '❌ Failed to pack Kestrun' -ForegroundColor Red
        throw 'Failed to pack Kestrun'
    }
    dotnet pack src/CSharp/Kestrun/Kestrun.csproj -c Release -o (Join-Path -Path $out -ChildPath 'nuget') `
        -p:Version=$Version -p:InformationalVersion=$VersionDetails.InformationalVersion `
        -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
    if ($LASTEXITCODE -ne 0) {
        Write-Host '❌ Failed to pack Kestrun' -ForegroundColor Red
        throw 'Failed to pack Kestrun'
    }
    $powershellGallery = New-Item -ItemType Directory -Force -Path (Join-Path -Path $out -ChildPath 'PowershellGallery')
    $zip = Join-Path -Path $powershellGallery -ChildPath("Kestrun-PSModule-$($Version).zip")
    Compress-Archive -Path "$kestrunReleasePath/$($VersionDetails.Version)/*" -DestinationPath $zip -Force
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to create $zip" -ForegroundColor Red
        throw "Failed to create $zip"
    }
    Write-Host "Created $zip"
    Write-Host '🚀 Release build completed.'
}

Add-BuildTask 'Build_Powershell_Help' {
    Write-Host '📖 Generating PowerShell Help...'
    pwsh -NoProfile -File .\Utility\Build-Help.ps1
}

Add-BuildTask 'Build_CSharp_Help' {
    Write-Host '📘 Generating C# Help...'
    # Check if xmldocmd is in PATH
    if (-not (Get-Command xmldocmd -ErrorAction SilentlyContinue)) {
        Write-Host '📦 Installing xmldocmd...'
        dotnet tool install -g xmldocmd
    } else {
        Write-Host '✅ xmldocmd already installed'
    }
    & .\Utility\Build-DocRefs.ps1
    & .\Utility\Update-JustTheDocs.ps1 -ApiRoot 'docs/cs/api' -TopParent 'C# API'
}

# Build Help will call Build_Powershell_Help and Build_CSharp_Help
Add-BuildTask 'Build-Help' {
    Write-Host '📚 Generating all Help...'
}, 'Build_Powershell_Help', 'Build_CSharp_Help', {
    $tutorialZipPath = './docs/pwsh/tutorial/examples.zip'
    Write-Host "📦 Creating tutorial examples zip ($tutorialZipPath)..."
    if ( (Test-Path -Path $tutorialZipPath)) {
        Write-Verbose "🗑️ Removing existing tutorial examples zip ($tutorialZipPath)..."
        Remove-Item -Path $tutorialZipPath -Force | Out-Null
    }
    Compress-Archive -Path './docs/_includes/examples/pwsh/' -DestinationPath $tutorialZipPath
}

# Clean Help will call Clean_Powershell_Help and Clean_CSharp_Help
Add-BuildTask 'Clean-Help' {
    Write-Host '🧼 Cleaning all Help artifacts...'
}, 'Clean_Powershell_Help', 'Clean_CSharp_Help'

# Clean PowerShell Help
Add-BuildTask 'Clean_Powershell_Help' {
    Write-Host '🧼 Cleaning PowerShell Help...'
    & .\Utility\Build-Help.ps1 -Clean
}

# Clean CSharp Help
Add-BuildTask 'Clean_CSharp_Help' {
    Write-Host '🧼 Cleaning C# Help...'
    & .\Utility\Build-DocRefs.ps1 -Clean
}

# Code Coverage
Add-BuildTask 'Coverage' {
    Write-Host '📊 Creating coverage report...'
    & .\Utility\Build-Coverage.ps1
    if ($LASTEXITCODE -ne 0) {
        Write-Host '❌ Coverage generation failed' -ForegroundColor Red
        throw 'Coverage generation failed'
    }
}

# Report coverage
Add-BuildTask 'Report-Coverage' {
    Write-Host '🌐 Creating coverage report webpage...'
    & .\Utility\Build-Coverage.ps1 -ReportGenerator
    if ($LASTEXITCODE -ne 0) {
        Write-Host '❌ Coverage Report generation failed' -ForegroundColor Red
        throw 'Coverage Report generation failed'
    }
}

# Clean coverage reports
Add-BuildTask 'Clean-Coverage' {
    Write-Host '🗑️ Cleaning coverage reports...'
    & .\Utility\Build-Coverage.ps1 -Clean
}

# Update the module manifest
Add-BuildTask 'Manifest' {
    Write-Host '📝 Updating Kestrun.psd1 manifest...'
    pwsh -NoProfile -File .\Utility\Update-Manifest.ps1
}

Add-BuildTask 'New-LargeFile' 'Clean-LargeFile', {
    Write-Host '📄 Generating large files...'
    if (-not (Test-Path -Path '.\examples\files\LargeFiles')) {
        New-Item -ItemType Directory -Path '.\examples\files\LargeFiles' -Force | Out-Null
    }
    (10, 100, 1000, 3000) | ForEach-Object {
        $sizeMB = $_
        & .\Utility\New-LargeFile.ps1 -Path ".\examples\files\LargeFiles\file-$sizeMB-MB.bin" -Mode 'Binary' -SizeMB $sizeMB
        & .\Utility\New-LargeFile.ps1 -Path ".\examples\files\LargeFiles\file-$sizeMB-MB.txt" -Mode 'Text' -SizeMB $sizeMB
    }
}
Add-BuildTask 'Clean-LargeFile' {
    Write-Host '🗑️ Cleaning generated large files...'
    Remove-Item -Path '.\examples\files\LargeFiles\*' -Force
}

Add-BuildTask 'ThirdPartyNotices' {
    Write-Host '📄 Updating third-party notices...'
    & .\Utility\Update-ThirdPartyNotices.ps1 -Project '.\src\CSharp\Kestrun\Kestrun.csproj' -Path '.\THIRD-PARTY-NOTICES.md' -Version (Get-Version -FileVersion $FileVersion)
}

Add-BuildTask All 'Clean', 'Restore', 'Build', 'Test'

Add-BuildTask Install-Module {
    Write-Host '📥 Installing Kestrun module...'
    & .\Utility\Install-Kestrun.ps1 -FileVersion $FileVersion
}

Add-BuildTask Remove-Module {
    Write-Host '🗑️ Removing Kestrun module...'
    & .\Utility\Install-Kestrun.ps1 -FileVersion $FileVersion -Remove
}

Add-BuildTask Update-Module {
    Write-Host '🔄 Updating Kestrun module...'
}, Remove-Module, Install-Module, {
    Write-Host '🔄 Kestrun module updated.'
}

Add-BuildTask 'Normalize-LineEndings' {
    Write-Host '🔄 Normalizing line endings to LF in .ps1, .psm1, and .cs files...'

    Get-ChildItem -Recurse -Include *.ps1, *.psm1, *.cs |
        Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
        ForEach-Object {
            $text = Get-Content -Raw -Path $_.FullName
            # Replace CRLF with LF
            $text = $text -replace "`r`n", "`n"
            # Write back with UTF-8 (no BOM, cross-platform friendly)
            [System.IO.File]::WriteAllText($_.FullName, $text, (New-Object System.Text.UTF8Encoding($false)))
            Write-Host "Normalized line endings in $($_.FullName)"
        }
}
