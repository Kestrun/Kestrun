[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
[CmdletBinding()]
param(
    # Path to the module manifest (psd1) or psm1
    [string]$ModulePath = './src/PowerShell/Kestrun/Kestrun.psm1',
    # Where Markdown will be written
    [string]$OutDir = './docs/pwsh/cmdlets',
    # Optional culture for XML help (not required for web site)
    [string]$XmlCulture = 'en-US',
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
    HelpVersion = "1.0.0.0"
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

    $raw = Get-Content $f.FullName -Raw

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
    #$raw = $raw -replace '```(\r?\n[\s\S]*?\r?\n)```', '```powershell$1```'

    #
    # 2. Fix MD012 - collapse 3+ consecutive blank lines into just 2 (one empty line)
    #
    #$raw = $raw -replace "(\r?\n){3,}", "`r`n`r`n"

    # Title from first H1 or filename
    # $title = ($raw -split "`n" | Where-Object { $_ -match '^\s*#\s+(.+)$' } | Select-Object -First 1)
    # $title = if ($title) { $title -replace '^\s*#\s+', '' } else { [IO.Path]::GetFileNameWithoutExtension($f.Name) }

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
    $srcDir = Split-Path -Path $ModulePath -Parent
    $xmlOut = Join-Path -Path $srcDir -ChildPath $XmlCulture
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
