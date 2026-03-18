[CmdletBinding(DefaultParameterSetName = 'Default')]
param(
    [Parameter()] [string]$Framework = 'net10.0',
    [Parameter()] [string]$Configuration = 'Release',
    [Parameter()] [string]$TestProject,
    [Parameter()]
    [string[]]$TestProjects = @(
        '.\tests\CSharp.Tests\Kestrun.Tests\KestrunTests.csproj',
        '.\tests\CSharp.Tests\Kestrun.Tool.Tests\Kestrun.Tool.Tests.csproj',
        '.\tests\CSharp.Tests\Kestrun.ServiceHost.Tests\Kestrun.ServiceHost.Tests.csproj'
    ),
    [Parameter()] [string]$CoverageDir = '.\coverage',
    [Parameter()] [string]$PesterPath = '.\tests\PowerShell.Tests\Kestrun.Tests',

    [Parameter(Mandatory = $true, ParameterSetName = 'Clean')]
    [switch]$Clean,

    [Parameter(Mandatory = $true, ParameterSetName = 'Report')]
    [switch]$ReportGenerator,

    [Parameter(ParameterSetName = 'Report')]
    [string]$ReportDir = './coverage/report', # where HTML lands

    [Parameter(ParameterSetName = 'Report')]
    [string]$ReportTypes = 'Html;TextSummary;Cobertura;Badges',

    [Parameter(ParameterSetName = 'Report')]
    [string]$AssemblyFilters = '+Kestrun*;+Kestrun.PowerShell*;-*.Tests;-testhost*;-xunit*',

    [Parameter(ParameterSetName = 'Report')]
    [string]$ClassFilters = '-*.Modules.PSDiagnostics.*',

    [Parameter(ParameterSetName = 'Report')]
    [string]$FileFilters = '-**/Generated/**;-**/*.g.cs',

    [Parameter(ParameterSetName = 'Report')]
    [switch]$OpenWhenDone,

    [Parameter(ParameterSetName = 'Report')]
    [string]$HistoryDir,

    [Parameter(ParameterSetName = 'Report')]
    [switch]$SkipPowershell,

    [Parameter(ParameterSetName = 'Report')]
    [switch]$ResetHistory
)

# Add Helper utility module
Import-Module -Name './Utility/Modules/Helper.psm1'

# Clean coverage reports
if ($Clean) {
    if (Test-Path $CoverageDir) {
        Write-Host '🧹 Cleaning coverage report...'
        Remove-Item -Path $CoverageDir -Recurse -Force
    } else {
        Write-Host '🧹 No coverage report found to clean.'
    }
    return
}

# Generate coverage reports
function Get-TestProjectTargetFrameworks {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    [xml]$projectXml = Get-Content -LiteralPath $ProjectPath -Raw
    $frameworks = [System.Collections.Generic.List[string]]::new()

    foreach ($propertyGroup in @($projectXml.Project.PropertyGroup)) {
        if ($null -eq $propertyGroup) {
            continue
        }

        if (-not [string]::IsNullOrWhiteSpace([string]$propertyGroup.TargetFramework)) {
            $frameworks.Add([string]$propertyGroup.TargetFramework)
        }

        if (-not [string]::IsNullOrWhiteSpace([string]$propertyGroup.TargetFrameworks)) {
            foreach ($tfm in ([string]$propertyGroup.TargetFrameworks -split ';')) {
                if (-not [string]::IsNullOrWhiteSpace($tfm)) {
                    $frameworks.Add($tfm)
                }
            }
        }
    }

    $uniqueFrameworks = $frameworks | Select-Object -Unique
    if (-not $uniqueFrameworks) {
        throw "Unable to resolve TargetFramework/TargetFrameworks from project: $ProjectPath"
    }

    return $uniqueFrameworks
}

function Resolve-TestProjectFramework {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,
        [Parameter(Mandatory = $true)]
        [string]$PreferredFramework
    )

    $projectFrameworks = Get-TestProjectTargetFrameworks -ProjectPath $ProjectPath
    if ($projectFrameworks -contains $PreferredFramework) {
        return $PreferredFramework
    }

    if ($projectFrameworks -contains 'net10.0') {
        return 'net10.0'
    }

    return $projectFrameworks[0]
}

$requestedTestProjects = @()
if ($PSBoundParameters.ContainsKey('TestProjects') -and $null -ne $TestProjects -and $TestProjects.Count -gt 0) {
    $requestedTestProjects = $TestProjects
} elseif ($PSBoundParameters.ContainsKey('TestProject') -and -not [string]::IsNullOrWhiteSpace($TestProject)) {
    $requestedTestProjects = @($TestProject)
} else {
    $requestedTestProjects = $TestProjects
}

if (-not $requestedTestProjects -or $requestedTestProjects.Count -eq 0) {
    throw 'No C# test projects were provided for coverage generation.'
}

$resolvedTestRuns = @()
foreach ($requestedTestProject in $requestedTestProjects) {
    $resolvedProjectPathInfo = Resolve-Path -LiteralPath $requestedTestProject -ErrorAction Stop
    $resolvedProjectPath = if ($resolvedProjectPathInfo -is [System.Management.Automation.PathInfo]) {
        $resolvedProjectPathInfo.Path
    } else {
        [string]$resolvedProjectPathInfo
    }

    $frameworkForProject = Resolve-TestProjectFramework -ProjectPath $resolvedProjectPath -PreferredFramework $Framework
    $resolvedTestRuns += [pscustomobject]@{
        ProjectPath = $resolvedProjectPath
        ProjectName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedProjectPath)
        Framework = $frameworkForProject
    }
}

# Prepare coverage folders
if (-not (Test-Path -Path $CoverageDir)) {
    New-Item -ItemType Directory -Force -Path $CoverageDir | Out-Null
}
$CoverageDir = Resolve-Path -Path $CoverageDir
if ($CoverageDir -is [System.Management.Automation.PathInfo]) { $CoverageDir = $CoverageDir.Path }

$frameworksToBuild = $resolvedTestRuns | Select-Object -ExpandProperty Framework -Unique
Write-Host "🚀 Building project(s) for frameworks: $($frameworksToBuild -join ', ')"
Invoke-Build Build -Configuration $Configuration -Frameworks $frameworksToBuild

$coverageFiles = @()
foreach ($testRun in $resolvedTestRuns) {
    Write-Host "🔎 Resolving ASP.NET runtime path for $($testRun.Framework) ($($testRun.ProjectName))..."
    $aspnet = Get-AspNetSharedDir $testRun.Framework
    Write-Host "📦 Using ASP.NET runtime: $aspnet"

    $binDir = Join-Path (Split-Path $testRun.ProjectPath -Parent) "bin\$Configuration\$($testRun.Framework)"
    New-Item -ItemType Directory -Force -Path $binDir | Out-Null

    Write-Host '📂 Copying ASP.NET runtime assemblies...'
    Copy-Item -Path (Join-Path $aspnet '*') -Destination $binDir -Recurse -Force

    $resultsDir = Join-Path $CoverageDir "raw\$($testRun.ProjectName)\$($testRun.Framework)"
    New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

    Write-Host "🧹 Cleaning previous build output for $($testRun.ProjectName)..."
    dotnet clean "$($testRun.ProjectPath)" --configuration $Configuration --framework $($testRun.Framework) | Out-Host

    Write-Host "🧪 Running tests with XPlat DataCollector for $($testRun.ProjectName) ($($testRun.Framework))..."
    dotnet test "$($testRun.ProjectPath)" `
        --configuration $Configuration `
        --framework $($testRun.Framework) `
        --collect:"XPlat Code Coverage" `
        --logger 'trx;LogFileName=test-results.trx' `
        --results-directory "$resultsDir" | Out-Host

    Write-Host "🗂️ Scanning for Cobertura files in $resultsDir..."
    $foundCoverage = Get-ChildItem -Path $resultsDir -Recurse -Filter 'coverage.cobertura.xml' -File |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if (-not $foundCoverage) {
        throw "❌ No 'coverage.cobertura.xml' found under $resultsDir"
    }

    $projectCoverageFile = Join-Path $CoverageDir ("csharp.{0}.{1}.cobertura.xml" -f $testRun.ProjectName, $testRun.Framework)
    Copy-Item -LiteralPath $foundCoverage.FullName -Destination $projectCoverageFile -Force

    if ((Get-Item $projectCoverageFile).Length -lt 400) {
        throw "⚠️ Coverage file looks empty: $projectCoverageFile"
    }

    Write-Host "📊 Coverage (Cobertura) saved: $projectCoverageFile"
    $coverageFiles += $projectCoverageFile
}

$coverageFile = Join-Path $CoverageDir "csharp.$Framework.cobertura.xml"
if ($coverageFiles.Count -eq 1) {
    Copy-Item -LiteralPath $coverageFiles[0] -Destination $coverageFile -Force
} else {
    $rg = Install-ReportGenerator
    if (-not $rg) { throw '❌ ReportGenerator not found.' }

    $mergeOutputDirectory = Join-Path $CoverageDir '_merge'
    if (Test-Path $mergeOutputDirectory) {
        Remove-Item -LiteralPath $mergeOutputDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $mergeOutputDirectory | Out-Null
    $mergeReportsArg = '"' + ($coverageFiles -join ';') + '"'
    $mergeFileFiltersArg = '"-**/obj/**;-**/*.g.cs"'
    Write-Host '🧬 Merging Cobertura coverage files:'
    $coverageFiles | ForEach-Object { Write-Host "  • $_" }

    & $rg `
        -reports:$mergeReportsArg `
        -targetdir:$mergeOutputDirectory `
        -reporttypes:Cobertura `
        -filefilters:$mergeFileFiltersArg

    $mergedCoverageFile = Join-Path $mergeOutputDirectory 'Cobertura.xml'
    if (-not (Test-Path $mergedCoverageFile)) {
        throw "❌ Failed to generate merged Cobertura report at $mergedCoverageFile"
    }

    Copy-Item -LiteralPath $mergedCoverageFile -Destination $coverageFile -Force
}

if ((Get-Item $coverageFile).Length -lt 400) {
    throw "⚠️ Coverage file looks empty: $coverageFile"
} else {
    Write-Host "📊 Combined coverage (Cobertura) saved: $coverageFile"
}

# ReportGenerator
if ($ReportGenerator) {
    if ($ResetHistory -and (Test-Path $HistoryDir)) {
        Write-Host "🧹 Resetting history once at $HistoryDir"
        Remove-Item -Recurse -Force -LiteralPath $HistoryDir
        New-Item -ItemType Directory -Force -Path $HistoryDir | Out-Null
    }
    # PowerShell coverage
    if (-not $SkipPowershell) {
        $pesterCoverageDir = Join-Path -Path $CoverageDir -ChildPath 'pester'
        $pesterCoverageFile = Join-Path -Path $pesterCoverageDir -ChildPath 'coverage.cobertura.xml'
        New-Item -Force -ItemType Directory -Path $pesterCoverageDir | Out-Null
        $cfg = New-PesterConfiguration
        # Resolve the glob to actual files (absolute paths), so there’s no ambiguity
        $toCover = @(
            Get-ChildItem -Path 'src/PowerShell/Kestrun' -Recurse -Include *.ps1, *.psm1 -File -ErrorAction SilentlyContinue
        ) | ForEach-Object { $_.FullName }

        Write-Host "🧮 Pester will try to analyze $($toCover.Count) files:"
        $toCover | ForEach-Object { Write-Host "  • $_" }

        $cfg.CodeCoverage.Path = $toCover

        $cfg.Run.Path = @($PesterPath)
        $cfg.Output.Verbosity = 'Detailed'
        $cfg.TestResult.Enabled = $true
        $cfg.Run.Exit = $true
        # include both ps1 and psm1 sources
        $cfg.CodeCoverage.Enabled = $true
        $cfg.CodeCoverage.OutputFormat = 'Cobertura'
        $cfg.CodeCoverage.OutputPath = $pesterCoverageFile

        Invoke-Pester -Configuration $cfg
        if (-not (Test-Path $pesterCoverageFile)) {
            throw '⚠️ Pester coverage output not found.'
        } else {
            Set-CoberturaPackageName -CoberturaPath $pesterCoverageFile -AssemblyName 'Kestrun.PowerShell'
            Write-Host "📊 Pester Coverage (Cobertura) saved: $pesterCoverageFile"
            Set-CoberturaGrouping `
                -CoberturaPath $pesterCoverageFile `
                -AssemblyRoot 'Kestrun.PowerShell' `
                -BasePath 'src/PowerShell/Kestrun' `
                -GroupByFirstFolder `
                -FolderRenameMap @{ 'ClaimPolicy' = 'Claim' } `
                -AllowedFirstFolders @('Public', 'Private')    # keep only these roots
        }
    }

    $rg = Install-ReportGenerator
    if (-not $rg) { throw '❌ ReportGenerator not found.' }

    if (-not (Test-Path $ReportDir)) { New-Item -ItemType Directory -Force -Path $ReportDir | Out-Null }
    $ReportDir = Resolve-Path -Path $ReportDir

    if (-not $HistoryDir -and $env:HISTORY_DIR) { $HistoryDir = $env:HISTORY_DIR }
    if (-not $HistoryDir) { $HistoryDir = Join-Path $CoverageDir 'history' }
    New-Item -ItemType Directory -Force -Path $HistoryDir | Out-Null
    $HistoryDir = Resolve-Path -Path $HistoryDir

    # --- build the report list safely ---
    $reportFiles = @($coverageFile)
    $includePwsh = (-not $SkipPowershell) -and (Get-Variable pesterCoverageFile -Scope 0 -ErrorAction SilentlyContinue) -and (Test-Path $pesterCoverageFile)
    if ($includePwsh) { $reportFiles += $pesterCoverageFile }

    # Debug: show exactly what we feed RG
    Write-Host '🧾 Report inputs:'; $reportFiles | ForEach-Object { Write-Host "  • $_" }

    $reportsArg = '"' + ($reportFiles -join ';') + '"'
    $title = 'Kestrun — ' + ($(if ($includePwsh) { 'Combined' } else { 'C#' })) + ' Coverage'

    # --- filters: avoid excluding Pester ---
    if (-not $PSBoundParameters.ContainsKey('AssemblyFilters')) {
        # safer default: exclude tests & infra, but don't force +Kestrun*
        $AssemblyFilters = '-*.Tests;-testhost*;-xunit*'
    }

    # Build a friendly tag
    $repo = $env:GITHUB_REPOSITORY; $sha = $env:GITHUB_SHA
    if ([string]::IsNullOrWhiteSpace($repo)) {
        try { $repo = (git config --get remote.origin.url) -replace '^.*[:/]', '' -replace '\.git$', '' } catch {
            Write-Warning "Could not determine repository name: $_"
        }
    }
    if ([string]::IsNullOrWhiteSpace($sha)) {
        try {
            $sha = (git rev-parse --short HEAD)
        } catch {
            Write-Warning "Could not determine commit SHA: $_"
        }
    }
    $tag = if ($repo -and $sha) { "$repo@$sha" } else { "$(Get-Date -Format s)Z" }

    Write-Host "📈 Generating coverage report → $ReportDir (history: $HistoryDir)"
    & $rg `
        -reports:$reportsArg `
        -targetdir:$ReportDir `
        -historydir:$HistoryDir `
        -title:$title `
        -reporttypes:$ReportTypes `
        -tag:$tag `
        -assemblyfilters:$AssemblyFilters `
        -filefilters:$FileFilters `
        -classfilters:$ClassFilters

    # Open report in browser

    if ($OpenWhenDone) {
        $index = Join-Path $ReportDir 'index.html'
        if (Test-Path $index) {
            if ($IsWindows) { Start-Process $index }
            elseif ($IsMacOS) { & open $index }
            else { & xdg-open $index }
        }
    }
    $index = Join-Path $ReportDir 'index.html'
    if (Test-Path $index) { Write-Host "`n✅ All done. Coverage is glowing in $index ✨" -ForegroundColor Magenta }
}
