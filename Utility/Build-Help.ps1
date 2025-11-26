[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
[CmdletBinding()]
param(
    # Path to the module manifest (psd1) or psm1
    [string]$ModulePath = './src/PowerShell/Kestrun/Kestrun.psm1',
    # Where Markdown will be written
    [string]$OutDir = './docs/pwsh/cmdlets',
    # Optional culture for XML help (not required for web site)
    [string]$XmlCulture = 'en-US',
    # Optional folder to put XML help in (overrides default module folder)
    [string]$XmlFolder,
    # Create/refresh XML help too?
    [switch]$NotEmitXmlHelp,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

if ($Clean) {
    Write-Host '🧹 Cleaning PlatyPS...'
    Remove-Item -Path $OutDir -Recurse -Force -ErrorAction SilentlyContinue
    return
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
if (Test-Path -Path "$OutDir/about_.md") {
    Write-Host "🗑️ Clearing existing help in $OutDir/about_.md"
    Remove-Item -Path "$OutDir/about_.md" -Force
}
# Import PlatyPS module
Import-Module Microsoft.PowerShell.PlatyPS
Write-Host "📦 Importing module: $ModulePath"
Import-Module $ModulePath -Force
New-KrServer -Name 'Docs'
Remove-KrServer -Name 'Docs' -Force
#Import-Module -Name ./Utility/PlatyPS/platyPS.psm1

Write-Host '🧾 Generating Markdown help...'
# Create or update Markdown help
if (Test-Path $OutDir ) {
    Write-Host "🗑️ Clearing existing help in $OutDir"
    Remove-Item -Path $OutDir -Recurse -Force -ErrorAction SilentlyContinue
}

$newMdSplat = @{
    ModuleInfo = Get-Module Kestrun
    OutputFolder = $OutDir
    HelpVersion = '1.0.0.0'
    WithModulePage = $true
    Force = $true
}
Write-Host "🛠️ Creating markdown help in $OutDir"
New-MarkdownCommandHelp @newMdSplat

$index_md = @'
---
layout: default
title: PowerShell Cmdlets
parent: PowerShell
nav_order: 2
# children inherit parent via _config.yml defaults
---

# PowerShell Cmdlets
Browse the cmdlet reference in the sidebar.
This documentation is generated from the Kestrun PowerShell module and provides detailed information on available cmdlets, their parameters, and usage examples.
'@


Set-Content -Path (Join-Path $OutDir 'index.md') -Value $index_md -Encoding UTF8

# Normalize cmdlet pages for Just the Docs
$files = Get-ChildItem $OutDir -Recurse -Filter *.md | Sort-Object Name
$i = 1
foreach ($f in $files) {
    if ($f.Name -ieq 'index.md') { continue }

    $lines = Get-Content $f.FullName

    # 1. Fix MD040 - add language to bare code fences
    #    Converts:
    #       ```
    #       Resolve-KrPath ...
    #       ```
    #    To:
    #       ```powershell
    #       Resolve-KrPath ...
    #       ```
    #
    #    Only touch opening fences that are just ``` on their own line.
    $inFence = $false

    for ($j = 0; $j -lt $lines.Count; $j++) {
        $line = $lines[$j]

        if ($line -match '^\s*```(\w+)?\s*$') {
            $lang = $Matches[1]

            if (-not $inFence) {
                # Opening fence
                if (-not $lang) {
                    # Bare ``` -> add powershell
                    $lines[$j] = '```powershell'
                }
                $inFence = $true
            } else {
                # Closing fence – leave it exactly as is
                $inFence = $false
            }
        }
    }

    $raw = ($lines -join "`n")

    if ($raw -notmatch '^\s*---\s*$') {
        @"
---
layout: default
parent: PowerShell Cmdlets
nav_order: $i
render_with_liquid: false
$($raw.Substring(5))
"@ | Set-Content $f.FullName -NoNewline
    }
    $i++
}

# (Optional) emit external help XML to ship in your module
if (-not $NotEmitXmlHelp) {
    $md = Measure-PlatyPSMarkdown -Path "$OutDir/Kestrun/*.md"
    if ( $XmlFolder) {
        $xmlOut = Join-Path -Path $XmlFolder -ChildPath $XmlCulture
    } else {
        $srcDir = Split-Path -Path $ModulePath -Parent
        $xmlOut = Join-Path -Path $srcDir -ChildPath $XmlCulture
    }
    New-Item -ItemType Directory -Force -Path $xmlOut | Out-Null
    Write-Host '🧬 Generating external help XML…'
    # Import only CommandHelp → export MAML
    $md |
        Where-Object FileType -Match 'CommandHelp' |
        Import-MarkdownCommandHelp -Path { $_.FilePath } |
        Export-MamlCommandHelp -OutputFolder $xmlOut -Force -Verbose
    Write-Host "✅ Done. XML Help at $xmlOut"
}
Write-Host "✅ Done. Markdown at $OutDir"
