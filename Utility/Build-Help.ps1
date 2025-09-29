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
    [switch]$EmitXmlHelp,
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

Write-Host "📦 Importing module: $ModulePath"
Import-Module $ModulePath -Force
New-KrServer -Name 'Docs'
Remove-KrServer -Name 'Docs' -Force
Import-Module -Name ./Utility/PlatyPS/platyPS.psm1

Write-Host '🧾 Generating Markdown help...'
# Create or update Markdown help
if (Test-Path $OutDir ) {
    Write-Host "🗑️ Clearing existing help in $OutDir"
    Remove-Item -Path $OutDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "🛠️ Creating markdown help in $OutDir"
New-MarkdownHelp -Module (Get-Module -Name Kestrun) -OutputFolder $OutDir -Force
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

Set-Content -Path (Join-Path $OutDir 'index.md') -Values $index_md -Encoding UTF8

# Normalize cmdlet pages for Just the Docs
$files = Get-ChildItem $OutDir -Recurse -Filter *.md | Sort-Object Name
$i = 1
foreach ($f in $files) {
    if ($f.Name -ieq 'index.md') { continue }

    $raw = Get-Content $f.FullName -Raw

    # Title from first H1 or filename
    $title = ($raw -split "`n" | Where-Object { $_ -match '^\s*#\s+(.+)$' } | Select-Object -First 1)
    $title = if ($title) { $title -replace '^\s*#\s+', '' } else { [IO.Path]::GetFileNameWithoutExtension($f.Name) }

    if ($raw -notmatch '^\s*---\s*$') {
        @"
---
layout: default
parent: PowerShell Cmdlets
title: $title
nav_order: $i
render_with_liquid: false
$($raw.Substring(5))
"@ | Set-Content $f.FullName -NoNewline
    } else {
        # If it already has front matter, ensure the key bits are present
        $lines = $raw -split "`n"
        $endIdx = ($lines | Select-String -SimpleMatch '---' -AllMatches).Matches[1].Index
        $head = $lines[0..$endIdx]
        $body = $lines[($endIdx + 1)..($lines.Length - 1)]

        $ensure = @()
        if ($head -notcontains 'layout: default') { $ensure += 'layout: default' }
        if ($head -notmatch '^parent:\s') { $ensure += 'parent: PowerShell Cmdlets' }
        if ($head -notmatch '^title:\s') { $ensure += "title: $title" }
        if ($head -notmatch '^nav_order:\s') { $ensure += "nav_order: $i" }
        if ($head -notcontains 'render_with_liquid: false') { $ensure += 'render_with_liquid: false' }

        $newHead = @($head[0]) + ($head[1..($head.Count - 1)] + $ensure | Select-Object -Unique)
        ($newHead + $body) -join "`n" | Set-Content $f.FullName -NoNewline
    }

    $i++
}



# (Optional) emit external help XML to ship in your module
if ($EmitXmlHelp) {
    $xmlOut = Join-Path $OutDir $XmlCulture
    New-Item -ItemType Directory -Force -Path $xmlOut | Out-Null
    Write-Host '🧬 Generating external help XML…'
    New-ExternalHelp -Path $OutDir -OutputPath $xmlOut -Force
}
Write-Host "✅ Done. Markdown at $OutDir"
