[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
param(
    [ValidateSet('net8.0', 'net9.0')] [string]$Tfm = 'net8.0',
    [string]$Project = 'src/CSharp/Kestrun/Kestrun.csproj',
    [string]$StageDir = 'artifacts/xmldoc',
    [string]$ApiOut = 'docs/cs/api',
    [switch]$Clean
)

# Add Helper utility
. ./Utility/Helper.ps1

if ($Clean) {
    Write-Host '🧹 Cleaning up...'
    Remove-Item -Path $StageDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $ApiOut -Recurse -Force -ErrorAction SilentlyContinue
    return
}
$ErrorActionPreference = 'Stop'
$major = if ($Tfm -like 'net8.*') { '8' } else { '9' }

Write-Host "🏗️ Building $Project for $Tfm..."
dotnet build $Project -c Release -f $Tfm | Out-Host

# Prepare folders
$bin = Join-Path (Split-Path $Project -Parent) "bin/Release/$Tfm"
New-Item -ItemType Directory -Force -Path $StageDir | Out-Null
New-Item -ItemType Directory -Force -Path $ApiOut | Out-Null

# 1) Copy your build output (includes NuGet deps thanks to CopyLocalLockFileAssemblies=true)
Copy-Item "$bin/*" $StageDir -Recurse -Force | Out-Null

$aspnet = Get-SharedFrameworkPath 'Microsoft.AspNetCore.App' $major
Copy-Item "$aspnet/*.dll" $StageDir -Force

$netcore = Get-SharedFrameworkPath 'Microsoft.NETCore.App' $major
Copy-Item "$netcore/*.dll" $StageDir -Force

# 4) Ensure System.Management.Automation.dll is present
$smaTarget = Join-Path $StageDir 'System.Management.Automation.dll'
if (-not (Test-Path $smaTarget)) {
    $smaFromPSHOME = Join-Path $PSHOME 'System.Management.Automation.dll'
    if (Test-Path $smaFromPSHOME) {
        Copy-Item $smaFromPSHOME $StageDir -Force | Out-Null
    } else {
        # Fallback to NuGet cache (works in CI)
        $pkgRoot = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.powershell.sdk'
        $cand = Get-ChildItem $pkgRoot -Recurse -Filter System.Management.Automation.dll -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\(lib|runtimes\\[^\\]+\\lib)\\$([Regex]::Escape($Tfm))\\$" } |
            Sort-Object FullName -Descending | Select-Object -First 1
        if ($cand) {
            Copy-Item $cand.FullName $StageDir -Force | Out-Null
        } else {
            throw 'System.Management.Automation.dll not found in PSHOME or NuGet cache.'
        }
    }
}

# 5) Verify essentials
@('Kestrun.dll', 'System.Runtime.dll', 'System.Management.Automation.dll') | ForEach-Object {
    $p = Join-Path $StageDir $_
    if (-not (Test-Path $p)) { throw "❌ Missing $_ in $StageDir" }
}

# 6) Generate Markdown docs
$xDll = Join-Path $StageDir 'Kestrun.dll'
Write-Host "🧠 Generating API docs with xmldocmd → $xDll"
xmldocmd "$xDll" "$ApiOut" --visibility public --clean `
    --source https://github.com/Kestrun/Kestrun/tree/main/src/CSharp/Kestrun

Write-Host "✅ Docs generated in $ApiOut"
