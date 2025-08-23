﻿[CmdletBinding(DefaultParameterSetName = 'Default')]
param(
    [Parameter()] [string]$Framework = "net9.0",
    [Parameter()] [string]$Configuration = "Release",
    [Parameter()] [string]$TestProject = ".\tests\CSharp.Tests\Kestrun.Tests\KestrunTests.csproj",
    [Parameter()] [string]$CoverageDir = ".\coverage",

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
    [switch]$OpenWhenDone
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

Write-Host "🗂️  Scanning for Cobertura files in $resultsDir..."
$found = Get-ChildItem "$resultsDir" -Recurse -Filter 'coverage.cobertura.xml' -File |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $found) { throw "❌ No 'coverage.cobertura.xml' found under $resultsDir" }

Copy-Item -LiteralPath $found.FullName -Destination $coverageFile -Force

if ((Get-Item $coverageFile).Length -lt 400) {
    throw "⚠️ Coverage file looks empty: $coverageFile"
} else {
    Write-Host "📊 Coverage (Cobertura) saved: $coverageFile"
}

if ($ReportGenerator) {
    $rg = Install-ReportGenerator
    New-Item -ItemType Directory -Force -Path $ReportDir | Out-Null
    $reportsArg = '"{0}"' -f $coverageFile

    Write-Host "📈 Generating coverage report → $ReportDir"
    & $rg `
        -reports:$reportsArg `
        -targetdir:$ReportDir `
        -reporttypes:$ReportTypes `
        -assemblyfilters:$AssemblyFilters `
        -filefilters:$FileFilters

    if ($OpenWhenDone) {
        $index = Join-Path $ReportDir "index.html"
        if (Test-Path $index) {
            if ($IsWindows) { Start-Process $index }
            elseif ($IsMacOS) { & open $index }
            else { & xdg-open $index }
        }
    }
    Write-Host "`nAll done. Coverage is glowing in $ReportDir ✨" -ForegroundColor Magenta
}
