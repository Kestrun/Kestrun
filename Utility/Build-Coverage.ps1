[CmdletBinding(DefaultParameterSetName = 'Default')]
param(
    [Parameter()] [string]$Framework = "net9.0",
    [Parameter()] [string]$Configuration = "Release",
    [Parameter()] [string]$TestProject = ".\tests\CSharp.Tests\Kestrun.Tests\KestrunTests.csproj",
    [Parameter()] [string]$CoverageDir = ".\coverage",
    [Parameter()] [string]$PesterPath = ".\tests\PowerShell.Tests\Kestrun.Tests",

    [Parameter(Mandatory = $true, ParameterSetName = 'Clean')]
    [switch]$Clean,

    [Parameter(Mandatory = $true, ParameterSetName = 'Report')]
    [switch]$ReportGenerator,

    [Parameter(ParameterSetName = 'Report')]
    [string]$ReportDir = "./coverage/report", # where HTML lands

    [Parameter(ParameterSetName = 'Report')]
    [string]$ReportTypes = "Html;TextSummary;Cobertura;Badges",

    [Parameter(ParameterSetName = 'Report')]
    [string]$AssemblyFilters = "+Kestrun*;-*.Tests",

    [Parameter(ParameterSetName = 'Report')]
    [string]$FileFilters = "-**/Generated/**;-**/*.g.cs",

    [Parameter(ParameterSetName = 'Report')]
    [switch]$OpenWhenDone,

    [Parameter(ParameterSetName = 'Report')]
    [string]$HistoryDir,

    [Parameter(ParameterSetName = 'Report')]
    [switch]$SkipPowershell
)

# Add Helper utility
. ./Utility/Helper.ps1

# Clean coverage reports
if ($Clean) {
    if (Test-Path $CoverageDir) {
        Write-Host "🧹 Cleaning coverage report..."
        Remove-Item -Path $CoverageDir -Recurse -Force
    } else {
        Write-Host "🧹 No coverage report found to clean."
    }
    return
}

# Generate coverage reports
Write-Host "🔎 Resolving ASP.NET runtime path for $Framework..."
$aspnet = Get-AspNetSharedDir $Framework
Write-Host "📦 Using ASP.NET runtime: $aspnet"

$binDir = Join-Path (Split-Path $TestProject -Parent) "bin\$Configuration\$Framework"
New-Item -ItemType Directory -Force -Path $binDir | Out-Null

Write-Host "📂 Copying ASP.NET runtime assemblies..."
Copy-Item -Path (Join-Path $aspnet '*') -Destination $binDir -Recurse -Force

# Prepare coverage folders
if (-not (Test-Path -Path $CoverageDir)) {
    New-Item -ItemType Directory -Force -Path $CoverageDir | Out-Null
}
$CoverageDir = Resolve-Path -Path $CoverageDir
if ($CoverageDir -is [System.Management.Automation.PathInfo]) { $CoverageDir = $CoverageDir.Path }

# Raw results by TFM (so multi-target runs can live side-by-side)
$resultsDir = Join-Path $CoverageDir "raw\$Framework"
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null
$coverageFile = Join-Path $CoverageDir "csharp.$Framework.cobertura.xml"

Write-Host "🧹 Cleaning previous builds..."
dotnet clean $TestProject --configuration $Configuration | Out-Host

Write-Host "🧪 Running tests with XPlat DataCollector..."
dotnet test $TestProject `
    --configuration $Configuration `
    --framework $Framework `
    --collect:"XPlat Code Coverage" `
    --logger "trx;LogFileName=test-results.trx" `
    --results-directory "$resultsDir" | Out-Host

Write-Host "🗂️ Scanning for Cobertura files in $resultsDir..."
$found = Get-ChildItem "$resultsDir" -Recurse -Filter 'coverage.cobertura.xml' -File |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $found) { throw "❌ No 'coverage.cobertura.xml' found under $resultsDir" }

Copy-Item -LiteralPath $found.FullName -Destination $coverageFile -Force

if ((Get-Item $coverageFile).Length -lt 400) {
    throw "⚠️ Coverage file looks empty: $coverageFile"
} else {
    Write-Host "📊 Coverage (Cobertura) saved: $coverageFile"
}

# ReportGenerator
if ($ReportGenerator) {
    # PowerShell coverage
    if (-not $SkipPowershell) {
        $pesterCoverageDir = Join-Path -Path $CoverageDir -ChildPath 'pester'
        $pesterCoverageFile = Join-Path -Path $pesterCoverageDir -ChildPath 'coverage.cobertura.xml'
        New-Item -Force -ItemType Directory -Path $pesterCoverageDir | Out-Null

        $cfg = New-PesterConfiguration
        $cfg.Run.Path = @($PesterPath)
        $cfg.Output.Verbosity = 'Detailed'
        $cfg.TestResult.Enabled = $true
        $cfg.Run.Exit = $true
        # include both ps1 and psm1 sources
        $cfg.CodeCoverage.Enabled = $true
        $cfg.CodeCoverage.Path = @(
            'src/PowerShell/Kestrun/**/*.ps1',
            'src/PowerShell/Kestrun/**/*.psm1'
        )
        $cfg.CodeCoverage.OutputFormat = 'Cobertura'
        $cfg.CodeCoverage.OutputPath = $pesterCoverageFile

        Invoke-Pester -Configuration $cfg
        if (-not (Test-Path $pesterCoverageFile)) {
            throw '⚠️ Pester coverage output not found.'
        } else {
            Write-Host "📊 Pester Coverage (Cobertura) saved: $pesterCoverageFile"
        }
    }

    $rg = Install-ReportGenerator
    if (-not $rg) { throw "❌ ReportGenerator not found." }

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
    Write-Host "🧾 Report inputs:"; $reportFiles | ForEach-Object { Write-Host "  • $_" }

    $reportsArg = '"' + ($reportFiles -join ';') + '"'
    $title = "Kestrun — " + ($(if ($includePwsh) { "Combined" } else { "C#" })) + " Coverage"

    # --- filters: avoid excluding Pester ---
    if (-not $PSBoundParameters.ContainsKey('AssemblyFilters')) {
        # safer default: exclude tests & infra, but don't force +Kestrun*
        $AssemblyFilters = "-*.Tests;-testhost*;-xunit*"
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
        -filefilters:$FileFilters

    # Open report in browser

    if ($OpenWhenDone) {
        $index = Join-Path $ReportDir "index.html"
        if (Test-Path $index) {
            if ($IsWindows) { Start-Process $index }
            elseif ($IsMacOS) { & open $index }
            else { & xdg-open $index }
        }
    }
    $index = Join-Path $ReportDir "index.html"
    if (Test-Path $index) { Write-Host "`n✅ All done. Coverage is glowing in $index ✨" -ForegroundColor Magenta }
}
