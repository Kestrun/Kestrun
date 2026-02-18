[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$OutputPath = 'docs/pwsh/tutorial/index.md',
    [string]$TutorialRoot = 'docs/pwsh/tutorial'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
Generates docs/pwsh/tutorial/index.md from tutorial chapter files.

.DESCRIPTION
Scans tutorial sections under docs/pwsh/tutorial, reads each chapter's H1 and
intro sentence, locates its example script include, and generates the index
with a consistent table plus reference links.

.PARAMETER OutputPath
Path to the tutorial index file to write.

.PARAMETER TutorialRoot
Root folder containing tutorial sections (subfolders like 1.introduction).

.EXAMPLE
.\Utility\Build-TutorialIndex.ps1

.EXAMPLE
.\Utility\Build-TutorialIndex.ps1 -OutputPath "docs/pwsh/tutorial/index.md"
#>

<#
.SYNOPSIS
Removes a UTF-8 BOM from the start of a string if present.
.PARAMETER Text
Input text to normalize.
.OUTPUTS
System.String
#>
function ConvertFrom-BomText {
    param([string]$Text)
    if ($Text -and $Text.StartsWith([char]0xFEFF)) {
        return $Text.Substring(1)
    }
    return $Text
}

<#
.SYNOPSIS
Normalizes tutorial text line endings to LF.
.PARAMETER Text
Input text to normalize.
.OUTPUTS
System.String
#>
function ConvertTo-TutorialText {
    param([string]$Text)
    if (-not $Text) { return $Text }
    return ($Text -replace "`r`n", "`n")
}

<#
.SYNOPSIS
Escapes table cell content for Markdown tables.
.PARAMETER Text
Input text to escape.
.OUTPUTS
System.String
#>
function ConvertTo-MarkdownCell {
    param([string]$Text)
    if (-not $Text) { return '' }
    $clean = $Text -replace "\r?\n", ' '
    $clean = $clean -replace '\|', '\\|'
    return $clean.Trim()
}

<#
.SYNOPSIS
Extracts the first sentence from a text block.
.PARAMETER Text
Input text to inspect.
.OUTPUTS
System.String
#>
function Get-FirstSentence {
    param([string]$Text)
    if (-not $Text) { return '' }
    $m = [regex]::Match($Text, '^(.+?[\.\!\?])(\s|$)')
    if ($m.Success) { return $m.Groups[1].Value.Trim() }
    return $Text.Trim()
}

<#
.SYNOPSIS
Builds a stable slug from a chapter base filename.
.PARAMETER BaseName
Base filename (without extension).
.OUTPUTS
System.String
#>
function Get-BaseSlug {
    param([string]$BaseName)
    if (-not $BaseName) { return '' }
    $slug = $BaseName.ToLowerInvariant()
    $slug = [regex]::Replace($slug, '[^a-z0-9]+', '-')
    $slug = $slug.Trim('-')
    $slug = [regex]::Replace($slug, '^(\d+-)+', '')
    return $slug
}

<#
.SYNOPSIS
Gets the chapter number from a filename.
.PARAMETER BaseName
Base filename (without extension).
.PARAMETER SectionNumber
Section number for validation.
.OUTPUTS
System.Int32
#>
function Get-ChapterNumber {
    param(
        [string]$BaseName,
        [int]$SectionNumber
    )

    $m2 = [regex]::Match($BaseName, '^(\d+)\.(\d+)')
    if ($m2.Success) {
        $first = [int]$m2.Groups[1].Value
        $second = [int]$m2.Groups[2].Value
        if ($first -eq $SectionNumber) { return $second }
        return $first
    }

    $m1 = [regex]::Match($BaseName, '^(\d+)\.')
    if ($m1.Success) { return [int]$m1.Groups[1].Value }

    return 9999
}

<#
.SYNOPSIS
Gets the first non-heading line after the H1.
.PARAMETER Lines
Document lines.
.PARAMETER StartIndex
Index of the H1 line.
.OUTPUTS
System.String
#>
function Get-IntroLine {
    param(
        [string[]]$Lines,
        [int]$StartIndex
    )

    for ($i = $StartIndex + 1; $i -lt $Lines.Length; $i++) {
        $line = $Lines[$i].Trim()
        if (-not $line) { continue }
        if ($line -match '^#') { continue }
        return $line
    }

    return ''
}

<#
.SYNOPSIS
Finds the example script include directive and returns the script name.
.PARAMETER Text
Document text to scan.
.OUTPUTS
System.String
#>
function Get-IncludeScriptName {
    param([string]$Text)
    $m = [regex]::Match($Text, '\{\%\s*include\s+examples/pwsh/(?<file>[^\s]+?\.ps1)\s*\%\}')
    if (-not $m.Success) { return $null }
    return $m.Groups['file'].Value
}

<#
.SYNOPSIS
Locates the H1 title and its line index.
.PARAMETER Lines
Document lines.
.OUTPUTS
System.Management.Automation.PSCustomObject
#>
function Get-H1Info {
    param([string[]]$Lines)
    for ($i = 0; $i -lt $Lines.Length; $i++) {
        $m = [regex]::Match($Lines[$i], '^#\s+(.+)$')
        if ($m.Success) {
            return [pscustomobject]@{
                Title = $m.Groups[1].Value.Trim()
                Index = $i
            }
        }
    }
    return [pscustomobject]@{ Title = ''; Index = -1 }
}

<#
.SYNOPSIS
Extracts the title from YAML front matter.
.PARAMETER Text
Document text to scan.
.OUTPUTS
System.String
#>
function Get-FrontMatterTitle {
    param([string]$Text)
    $m = [regex]::Match($Text, '(?s)\A---\s*\r?\n(.*?)\r?\n---\s*')
    if (-not $m.Success) { return '' }
    $fm = $m.Groups[1].Value
    $t = [regex]::Match($fm, '(?m)^title:\s*(.+)$')
    if ($t.Success) { return $t.Groups[1].Value.Trim() }
    return ''
}

$header = @'
---
title: Tutorials
parent: PowerShell

nav_order: 0
---

# Tutorials

Step-by-step guides to build and ship with Kestrun. This index lists runnable sample scripts and documentation chapters in recommended learning order.

## Prerequisites

- PowerShell 7.4, 7.5, or 7.6 (preview)
- .NET (run-only scenarios do NOT require the full SDK)

| PowerShell Version | Install (Run Samples) | Notes |
|--------------------|-----------------------|-------|
| 7.4 / 7.5 | [.NET 8 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) | Bundles base runtime + ASP.NET Core |
| 7.6 (preview) | [.NET 10 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) | Preview - updates frequently |

If you already have the **.NET SDK** for those versions installed you don't need to install the runtime separately.

- Kestrun module: installed or available from this repository at `src/PowerShell/Kestrun/Kestrun.psm1`
- Supported OS: same as .NET 8/10 (Windows, Linux, macOS), including ARM/ARM64

Verify (optional):

```powershell
dotnet --list-runtimes | Where-Object { $_ -match 'Microsoft.(AspNetCore|NETCore).App' }
```

You should see `Microsoft.AspNetCore.App 8.0.x` (and 10.0.x if using PS 7.6 preview).

## Quick start: run the samples

From the repository root:

```powershell
# 1) Hello World
pwsh .\examples\PowerShell\Tutorial\1-Hello-World.ps1
```

Then browse the routes (default listener: <http://127.0.0.1:5000>):
Read the note on each sample for the routes detail.

Stop the server with Ctrl+C in the terminal.

## Samples & Chapters Overview

| Order | Topic | Chapter | Sample Script | Focus |
| ----- | ----- | ------- | ------------- | ----- |
'@

$tutorialRootFull = (Resolve-Path -Path $TutorialRoot).Path
$sectionDirs = Get-ChildItem -Path $tutorialRootFull -Directory | Where-Object {
    $_.Name -match '^\d+\.' -and (Test-Path -LiteralPath (Join-Path $_.FullName 'index.md'))
} | Sort-Object {
    [int]([regex]::Match($_.Name, '^(\d+)\.').Groups[1].Value)
}

$rows = New-Object System.Collections.Generic.List[string]
$chapterRefs = New-Object System.Collections.Generic.List[string]
$scriptRefs = New-Object System.Collections.Generic.List[string]

$order = 0
foreach ($sectionDir in $sectionDirs) {
    $sectionNum = [int]([regex]::Match($sectionDir.Name, '^(\d+)\.').Groups[1].Value)
    $sectionIndexPath = Join-Path $sectionDir.FullName 'index.md'
    $sectionIndexText = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($sectionIndexPath))
    $sectionIndexText = ConvertTo-TutorialText (ConvertFrom-BomText $sectionIndexText)
    $sectionTitle = Get-FrontMatterTitle -Text $sectionIndexText
    if (-not $sectionTitle) {
        $sectionLines = $sectionIndexText -split "`n"
        $sectionTitle = (Get-H1Info -Lines $sectionLines).Title
    }
    if (-not $sectionTitle) {
        $sectionTitle = $sectionDir.Name
    }
    $files = Get-ChildItem -Path $sectionDir.FullName -File -Filter '*.md' |
        Where-Object { $_.Name -ne 'index.md' -and $_.Name -match '^\d+\..+\.md$' }

    $files = $files | Sort-Object {
        Get-ChapterNumber -BaseName $_.BaseName -SectionNumber $sectionNum
    }, Name

    foreach ($file in $files) {
        $order++
        $content = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($file.FullName))
        $content = ConvertTo-TutorialText (ConvertFrom-BomText $content)
        $lines = $content -split "`n"

        $h1 = Get-H1Info -Lines $lines
        $title = $h1.Title
        if (-not $title) {
            throw "Missing H1 title in $($file.FullName)"
        }

        $intro = Get-IntroLine -Lines $lines -StartIndex $h1.Index

        $scriptName = Get-IncludeScriptName -Text $content
        if (-not $scriptName) {
            throw "Missing include directive in $($file.FullName)"
        }

        $chapterNum = Get-ChapterNumber -BaseName $file.BaseName -SectionNumber $sectionNum
        $baseSlug = Get-BaseSlug -BaseName $file.BaseName
        if ($baseSlug) {
            $chKey = "ch-$sectionNum-$chapterNum-$baseSlug"
            $scKey = "sc-$sectionNum-$chapterNum-$baseSlug"
        } else {
            $chKey = "ch-$sectionNum-$chapterNum"
            $scKey = "sc-$sectionNum-$chapterNum"
        }

        $relPath = [System.IO.Path]::GetRelativePath($tutorialRootFull, $file.FullName)
        $relPath = $relPath -replace '\\', '/'
        $relPath = $relPath.Substring(0, $relPath.Length - 3)

        $topicCell = ConvertTo-MarkdownCell $sectionTitle
        $titleCell = ConvertTo-MarkdownCell $title
        $focusCell = ConvertTo-MarkdownCell (Get-FirstSentence $intro)

        $rows.Add("| $order | $topicCell | [$titleCell][$chKey] | [Script][$scKey] | $focusCell |")
        $chapterRefs.Add("[$chKey]: ./$relPath")
        $scriptRefs.Add("[$scKey]: /pwsh/tutorial/examples/$scriptName")
    }
}

$footer = @'


Static chapters and scripts are all linked directly above for quick navigation.


'@

$output = $header.TrimEnd() + "`n" + ($rows -join "`n") + $footer + ($chapterRefs -join "`n") + "`n" + ($scriptRefs -join "`n") + "`n"

if ($PSCmdlet.ShouldProcess($OutputPath, 'Write generated tutorial index')) {
    $outDir = Split-Path -Path $OutputPath -Parent
    if (-not (Test-Path -LiteralPath $outDir)) {
        [void](New-Item -ItemType Directory -Path $outDir -Force)
    }
    [System.IO.File]::WriteAllText($OutputPath, $output, [System.Text.Encoding]::UTF8)
}
