[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
param(
    [string]$OutputDir = './src/PowerShell/Kestrun/lib',
    [switch]$Force
)

# Add Helper utility
. ./Utility/Helper.ps1

# Where to put the final DLLs
$BaseOut = Join-Path -Path $OutputDir -ChildPath 'Microsoft.CodeAnalysis'

# Versions and packages you want
$Versions = @('4.9.2', '4.11.0', '4.13.0')
$Packages = @(
    'Microsoft.CodeAnalysis.Common',
    'Microsoft.CodeAnalysis.CSharp.Workspaces',
    'Microsoft.CodeAnalysis.CSharp.Scripting',
    'Microsoft.CodeAnalysis.CSharp',
    'Microsoft.CodeAnalysis.Scripting.Common',
    'Microsoft.CodeAnalysis.VisualBasic',
    'Microsoft.CodeAnalysis.VisualBasic.Workspaces',
    'Microsoft.CodeAnalysis.Workspaces.Common'
)

# Cross‑platform temp
$tmpDir = [System.IO.Path]::GetTempPath()
$Tmp = Join-Path $tmpDir '__nuget_tmp_roslyn'

New-Item -ItemType Directory -Path $Tmp -Force | Out-Null
New-Item -ItemType Directory -Path $BaseOut -Force | Out-Null

$missing = @{}

foreach ($ver in $Versions) {
    $TargetDir = Join-Path $BaseOut $ver
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

    foreach ($pkg in $Packages) {
        Write-Host "📦 Fetching $pkg $ver"
        $pkgFolder = Get-PackageFolder -Id $pkg -Version $ver -WorkRoot $Tmp -Force:$Force

        $libFolder = Join-Path $pkgFolder 'lib'
        $best = Get-BestTfmFolder $libFolder
        if (-not $best) {
            Write-Warning "No lib/* TFM folder found for $pkg $ver"
            continue
        }

        # Copy DLLs for that TFM
        Get-ChildItem -Path $best -Filter *.dll -File | ForEach-Object {
            Copy-Item $_.FullName -Destination $TargetDir -Force
        }
    }

    # Quick correctness check for this version
    $required = @(
        'Microsoft.CodeAnalysis.dll',
        'Microsoft.CodeAnalysis.CSharp.dll',
        'Microsoft.CodeAnalysis.Workspaces.dll'
    )
    $missing[$ver] = @()
    foreach ($r in $required) {
        if (-not (Test-Path (Join-Path $TargetDir $r))) { $missing[$ver] += $r }
    }
}

# Clean temp
Remove-Item -Path $Tmp -Recurse -Force

# Report summary
$hadIssue = $false
foreach ($ver in $Versions) {
    if ($missing[$ver].Count -gt 0) {
        $hadIssue = $true
        Write-Warning "[$ver] missing: $($missing[$ver] -join ', ')"
    }
}
if (-not $hadIssue) {
    Write-Host "`n✅ Finished. DLLs are in: $BaseOut"
} else {
    Write-Host "`n⚠️ Finished with missing files. Check warnings above. Output: $BaseOut"
}
