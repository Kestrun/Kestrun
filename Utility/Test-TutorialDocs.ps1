param(
    [string]$Path = 'docs/pwsh/tutorial',
    [string]$SubPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
    Upload endpoint for file hash calculation
.DESCRIPTION
    Accepts multipart/form-data with a single binary file and returns MD5/SHA1/SHA256/SHA384/SHA512 hashes.
.PARAMETER FormPayload
    The parsed multipart/form-data payload.
.OUTPUTS
    JSON object with file metadata and hashes.
#>
function Get-TextFileContent {
    param([string]$Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    return [System.Text.Encoding]::UTF8.GetString($bytes)
}


<#
.SYNOPSIS
Validates tutorial docs under docs/pwsh/tutorial according to the authoring guide.

.DESCRIPTION
This script enforces tutorial documentation standards including:
- Required front matter and section structure
- Reference-style link format with matching text/key
- Absolute path usage in reference definitions for consistency
- Proper cmdlet, guide, and example script reference formats

The validator checks for:
- Cmdlet references: Should use '/pwsh/cmdlets/' or '/guides/' with anchors
- Guide references: Should use '/guides/' instead of relative paths like '../../../../guides/'
- Example scripts: Should use '/pwsh/tutorial/examples/' instead of relative paths
- Complex relative paths: Flags references with multiple '../' traversals

.PARAMETER Path
Base path to scan for tutorial documentation. Defaults to 'docs/pwsh/tutorial'.

.PARAMETER SubPath
Optional sub-path within the base path to scan.

.EXAMPLE
.\Test-TutorialDocs.ps1
Validates all tutorials under docs/pwsh/tutorial

.EXAMPLE
.\Test-TutorialDocs.ps1 -Path "docs/pwsh/tutorial/10.middleware/4.Https-Hsts.md"
Validates a specific tutorial file
#>

# Validates tutorial docs under docs/pwsh/tutorial according to the authoring guide
$docs = (Resolve-Path -Path $Path).Path
$scan = if ($SubPath) {
    if ([System.IO.Path]::IsPathRooted($SubPath)) { $SubPath } else { Join-Path $docs $SubPath }
} else { $docs }

$failures = @()
Get-ChildItem -Path $scan -Recurse -Filter *.md | ForEach-Object {
    $itemPath = $_.FullName
    $rel = $itemPath.Substring($docs.Length).TrimStart('\/')
    $name = $_.Name
    $text = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($itemPath))

    # Normalize content across platforms/editors (BOM + CRLF) so section checks are stable.
    if ($text -and $text.StartsWith([char]0xFEFF)) {
        $text = $text.Substring(1)
    }
    $text = $text -replace "`r`n", "`n"

    # Skip index pages from strict checks
    if ($name -ieq 'index.md') { return }

    # 1) Filename pattern N.Title.md
    if ($name -notmatch '^[0-9]+\.[^\\/]+\.md$') {
        $failures += "[Name] $rel does not match N.Title.md"
    }

    # Extract front matter
    $fmMatch = [regex]::Match($text, '(?s)\A---\s*\r?\n(.*?)\r?\n---\s*')
    $fm = $null
    if ($fmMatch.Success) {
        $fmCandidate = $fmMatch.Groups[1].Value
        if ($fmCandidate -match '(?m)^title:\s*.+$' -and $fmCandidate -match '(?m)^parent:\s*.+$' -and $fmCandidate -match '(?m)^nav_order:\s*.+$') {
            $fm = $fmCandidate
        }
    }

    if (-not $fm) {
        $failures += "[FrontMatter] $rel missing YAML front matter"
    } else {
        $title = ([regex]::Match($fm, '(?m)^title:\s*(.+)$')).Groups[1].Value
        $parent = ([regex]::Match($fm, '(?m)^parent:\s*(.+)$')).Groups[1].Value
        $nav = ([regex]::Match($fm, '(?m)^nav_order:\s*(.+)$')).Groups[1].Value
        if (-not $title) { $failures += "[FrontMatter] $rel missing title" }
        if (-not $parent) { $failures += "[FrontMatter] $rel missing parent" }
        if (-not $nav) { $failures += "[FrontMatter] $rel missing nav_order" }

        # 4) H1 must match title
        $h1 = ([regex]::Match($text, '^#\s+(.+)$', 'Multiline')).Groups[1].Value
        if ($h1 -ne $title) { $failures += "[H1] $rel H1 does not match title '$title'" }
    }

    # Ensure required sections exist
    foreach ($hdr in '## Full source', '## Step-by-step', '## Try it', '## Troubleshooting', '## References') {
        if ($text -notmatch [regex]::Escape($hdr)) { $failures += "[Section] $rel missing '$hdr'" }
    }

    # Required footer navigation block (allow CRLF/LF and harmless whitespace variance)
    $previousNextPattern = '(?s)###\s+Previous\s*/\s*Next\s*\n\s*\n\s*\{:\s*\.fs-4\s*\.fw-500\s*\}'
    if ($text -notmatch $previousNextPattern) {
        $failures += "[Section] $rel missing '### Previous / Next\n\n{: .fs-4 .fw-500}'"
    }

    # Include directive present
    if ($text -notmatch '{% include examples/pwsh/.+?\.ps1 %}') {
        $failures += "[Include] $rel missing include directive"
    }

    # Code fences language check skipped here; enforced by markdownlint config

    # References section must use reference-style links and have definitions
    $refMatch = [regex]::Match($text, '(?s)##\s+References\s*\r?\n(.*?)(?:\r?\n##\s+|\r?\n---\s*\r?\n|$)')
    if ($refMatch.Success) {
        $refBlock = $refMatch.Groups[1].Value
        $lines = $refBlock -split '\r?\n'
        $refs = @()
        foreach ($ln in $lines) {
            if ($ln -match '^\s*-\s*\[(?<text>[^\]]+)\]\[(?<key>[^\]]+)\]\s*$') {
                if ($Matches['text'] -ne $Matches['key']) {
                    $failures += "[Links] $rel reference text and key must match in References: '$ln'"
                } else {
                    $refs += $Matches['key']
                }
            } elseif ($ln -match '^\s*-\s*\[[^\]]+\]\([^\)]+\)') {
                $failures += "[Links] $rel References must use reference-style links: '[Name][Name]' not inline: '$ln'"
            }
        }

        foreach ($r in ($refs | Sort-Object -Unique)) {
            $pattern = '(?m)^\[' + [regex]::Escape($r) + '\]:\s+\S+'
            if ($text -notmatch $pattern) {
                $failures += "[Links] $rel missing reference definition for '[$r]'"
            }
        }

        # Check reference definitions for absolute vs relative paths
        $refDefPattern = '(?m)^\[([^\]]+)\]:\s+(.+)$'
        $refDefs = [regex]::Matches($text, $refDefPattern)
        foreach ($refDef in $refDefs) {
            $refKey = $refDef.Groups[1].Value
            $refUrl = $refDef.Groups[2].Value.Trim()

            # Skip external URLs (http/https) and anchors (#)
            if ($refUrl -match '^https?://' -or $refUrl -match '^#') {
                continue
            }

            # Check for cmdlet references - should use absolute paths (but allow guide references with anchors)
            if ($refKey -match '^(Add|Remove|Set|Get|New|Test|Start|Stop|Enable|Disable|Write|Read|Import|Export)-Kr' -and
                $refUrl -notmatch '^/pwsh/cmdlets/' -and
                $refUrl -notmatch '^/guides/.*#' -and
                $refUrl -match '\.\./') {
                $failures += "[Links] $rel cmdlet reference '$refKey' uses relative path '$refUrl' - consider absolute path"
            }

            # Check for guide references - should use absolute paths when relative
            if ($refUrl -match '\.\./.*(guides)/' -and $refUrl -notmatch '^/(guides)/') {
                $failures += "[Links] $rel guide reference '$refKey' uses relative path '$refUrl' - should use absolute path '/guides/'"
            }

            # Check for example script references - should use absolute paths when relative
            if ($refKey -match '\.ps1$' -and $refUrl -match '\.\./.*examples/' -and $refUrl -notmatch '^/pwsh/tutorial/examples/') {
                $failures += "[Links] $rel example script reference '$refKey' uses relative path '$refUrl' - should use absolute path '/pwsh/tutorial/examples/'"
            }

            # Check for relative paths that traverse directories (../)
            if ($refUrl -match '\.\./.*\.\./') {
                $failures += "[Links] $rel reference '$refKey' uses complex relative path '$refUrl' - consider absolute path"
            }
        }
    } else {
        $failures += "[Section] $rel missing '## References' content"
    }

    # Step-by-step is numbered starting at 1
    if ($text -notmatch '## Step-by-step\s*\r?\n1\.') { $failures += "[Steps] $rel list must start at 1." }
}

# Validate footer Previous/Next navigation.
# - Supports both inline links: [Text](./path)
# - And reference links: [Text][Key] with [Key]: ./path
# - Enforces ordering across the tutorial:
#   - Within a section: Previous/Next should point to adjacent chapters
#   - Section boundary: last chapter's Next should point to next section's index
try {
    function Normalize-TutorialText {
        param([string]$Text)
        if ($Text -and $Text.StartsWith([char]0xFEFF)) { $Text = $Text.Substring(1) }
        return ($Text -replace "`r`n", "`n")
    }

    <#
        .SYNOPSIS
        Parses reference-style link definitions from text into a hashtable.
        .DESCRIPTION
        Scans the provided text for reference-style link definitions of the form:
        [key]: url
        and returns a hashtable mapping keys to URLs.
        .PARAMETER Text
        The input text to scan for reference definitions.
        .OUTPUTS
        A hashtable where keys are reference keys and values are URLs.
    #>
    function Get-TutorialRefMap {
        param([string]$Text)
        $map = @{}
        foreach ($m in [regex]::Matches($Text, '(?m)^\[(?<key>[^\]]+)\]:\s+(?<url>\S+)\s*$')) {
            $map[$m.Groups['key'].Value] = $m.Groups['url'].Value
        }
        return $map
    }

    function Resolve-TutorialFooterLink {
        param(
            [string]$Value,
            [hashtable]$RefMap
        )

        $v = (($null -ne $Value)?$Value : '' ).Trim()
        if (-not $v) { return $null }

        $inline = [regex]::Match($v, '^\[(?<text>[^\]]+)\]\((?<href>[^\)]+)\)$')
        if ($inline.Success) {
            return [pscustomobject]@{ Text = $inline.Groups['text'].Value; Href = $inline.Groups['href'].Value }
        }

        $ref = [regex]::Match($v, '^\[(?<text>[^\]]+)\]\[(?<key>[^\]]+)\]$')
        if ($ref.Success) {
            $key = $ref.Groups['key'].Value
            if ($RefMap.ContainsKey($key)) {
                return [pscustomobject]@{ Text = $ref.Groups['text'].Value; Href = $RefMap[$key] }
            }
            return [pscustomobject]@{ Text = $ref.Groups['text'].Value; Href = $null; MissingKey = $key }
        }

        return [pscustomobject]@{ Text = $null; Href = $null; Raw = $v }
    }

    <#
        .SYNOPSIS
        Gets the target file path for a tutorial footer link.
        .DESCRIPTION
        Given the source file and the href from a footer link, resolves to the target file path.
        Handles relative paths, index.md conventions, and ignores external/absolute links.
        .PARAMETER FromFile
        The source tutorial file path.
        .PARAMETER Href
        The href from the footer link.
        .OUTPUTS
        The resolved target file path, or $null if not applicable.
    #>
    function Get-TutorialTargetPath {
        param(
            [string]$FromFile,
            [string]$Href
        )

        if (-not $Href) { return $null }
        $h = $Href.Split('#')[0]
        if (-not $h) { return $null }

        # Convention for "no previous/next" is [_None_](.)
        if ($h -eq '.' -or $h -eq './' -or $h -eq '.\\') { return $null }

        if ($h -match '^https?://') { return $null }
        if ($h -match '^/') { return $null }

        $h = $h -replace '/', '\\'
        $fromDir = Split-Path -Path $FromFile -Parent
        $candidate = Join-Path -Path $fromDir -ChildPath $h

        if ($candidate -match '\.md$') {
            return $candidate
        }

        # Footer links omit .md; resolve to index.md when targeting /index, otherwise to .md.
        if ($candidate -match '(\\|/)index$') {
            return ($candidate + '.md')
        }
        return ($candidate + '.md')
    }

    $sectionDirs = Get-ChildItem -Path $docs -Directory | Where-Object {
        $_.Name -match '^\d+\.' -and (Test-Path -LiteralPath (Join-Path $_.FullName 'index.md'))
    } | Sort-Object {
        [int]([regex]::Match($_.Name, '^(\d+)\.').Groups[1].Value)
    }

    $chapters = @()
    foreach ($sectionDir in $sectionDirs) {
        $sectionNum = [int]([regex]::Match($sectionDir.Name, '^(\d+)\.').Groups[1].Value)
        $files = Get-ChildItem -Path $sectionDir.FullName -File -Filter '*.md' |
            Where-Object { $_.Name -ne 'index.md' -and $_.Name -match '^\d+\..+\.md$' }

        foreach ($f in $files) {
            $m = [regex]::Match($f.BaseName, '^(\d+)\.(\d+)')
            if ($m.Success -and [int]$m.Groups[1].Value -eq $sectionNum) {
                $chapterNum = [int]$m.Groups[2].Value
            } else {
                $n = [regex]::Match($f.BaseName, '^(\d+)\.')
                $chapterNum = if ($n.Success) { [int]$n.Groups[1].Value } else { 9999 }
            }
            $chapters += [pscustomobject]@{
                SectionDir = $sectionDir
                SectionNum = $sectionNum
                ChapterNum = $chapterNum
                File = $f
            }
        }
    }

    $chapters = @($chapters | Sort-Object SectionNum, ChapterNum, @{ Expression = { $_.File.Name } })

    for ($i = 0; $i -lt $chapters.Count; $i++) {
        $c = $chapters[$i]
        $prevChapter = if ($i -gt 0) { $chapters[$i - 1] } else { $null }

        $isFirstInSection = $false
        if ($i -eq 0) {
            $isFirstInSection = $true
        } else {
            $isFirstInSection = $chapters[$i - 1].SectionDir.FullName -ne $c.SectionDir.FullName
        }

        $isLastInSection = $false
        if ($i -eq $chapters.Count - 1) {
            $isLastInSection = $true
        } else {
            $isLastInSection = $chapters[$i + 1].SectionDir.FullName -ne $c.SectionDir.FullName
        }

        $nextExpected = $null
        if ($i -eq $chapters.Count - 1) {
            $nextExpected = [pscustomobject]@{ Kind = 'none' }
        } elseif ($isLastInSection) {
            $nextSectionDir = $chapters[$i + 1].SectionDir
            $nextExpected = [pscustomobject]@{ Kind = 'file'; Path = (Join-Path $nextSectionDir.FullName 'index.md') }
        } else {
            $nextExpected = [pscustomobject]@{ Kind = 'file'; Path = $chapters[$i + 1].File.FullName }
        }

        $prevExpected = $null
        if (-not $prevChapter) {
            $prevExpected = [pscustomobject]@{ Kind = 'none' }
        } elseif ($isFirstInSection) {
            $prevExpected = [pscustomobject]@{ Kind = 'file'; Path = (Join-Path $prevChapter.SectionDir.FullName 'index.md') }
        } else {
            $prevExpected = [pscustomobject]@{ Kind = 'file'; Path = $prevChapter.File.FullName }
        }

        $text = Normalize-TutorialText (Get-TextFileContent -Path $c.File.FullName)
        $refMap = Get-TutorialRefMap -Text $text

        $rel = $c.File.FullName.Substring($docs.Length).TrimStart('\\').TrimStart('/')

        $prevMatch = [regex]::Match($text, '(?m)^Previous:\s*(.+?)\s*$')
        $nextMatch = [regex]::Match($text, '(?m)^Next:\s*(.+?)\s*$')
        if (-not $prevMatch.Success) { $failures += "[Nav] $rel missing 'Previous:' line in footer navigation"; continue }
        if (-not $nextMatch.Success) { $failures += "[Nav] $rel missing 'Next:' line in footer navigation"; continue }

        $prevLink = Resolve-TutorialFooterLink -Value $prevMatch.Groups[1].Value -RefMap $refMap
        $nextLink = Resolve-TutorialFooterLink -Value $nextMatch.Groups[1].Value -RefMap $refMap

        if ($prevLink -and $prevLink.PSObject.Properties.Name -contains 'MissingKey' -and $prevLink.MissingKey) {
            $failures += "[Nav] $rel Previous uses reference key '$($prevLink.MissingKey)' but has no matching reference definition"
            continue
        }
        if ($nextLink -and $nextLink.PSObject.Properties.Name -contains 'MissingKey' -and $nextLink.MissingKey) {
            $failures += "[Nav] $rel Next uses reference key '$($nextLink.MissingKey)' but has no matching reference definition"
            continue
        }

        $prevTarget = if ($prevLink) { Get-TutorialTargetPath -FromFile $c.File.FullName -Href $prevLink.Href } else { $null }
        $nextTarget = if ($nextLink) { Get-TutorialTargetPath -FromFile $c.File.FullName -Href $nextLink.Href } else { $null }

        $prevTargetN = if ($prevTarget) { [System.IO.Path]::GetFullPath($prevTarget) } else { $null }
        $nextTargetN = if ($nextTarget) { [System.IO.Path]::GetFullPath($nextTarget) } else { $null }
        $prevExpectedN = if ($prevExpected.Kind -eq 'file') {
            [System.IO.Path]::GetFullPath($prevExpected.Path)
        } else {
            $null
        }
        $nextExpectedN = if ($nextExpected.Kind -eq 'file') { [System.IO.Path]::GetFullPath($nextExpected.Path) } else { $null }

        if ($prevExpected.Kind -eq 'none') {
            if (-not $prevLink -or $prevLink.Href -ne '.' -or $prevTarget) {
                $failures += "[Nav] $rel Previous must be [_None_](.)"
            }
        } else {
            if (-not $prevTargetN) {
                $failures += "[Nav] $rel Previous must link to the previous chapter"
            } elseif (-not (Test-Path -LiteralPath $prevTargetN)) {
                $failures += "[Nav] $rel Previous target does not exist: '$($prevLink.Href)'"
            } elseif ($prevTargetN -ne $prevExpectedN) {
                $failures += "[Nav] $rel Previous target is '$($prevLink.Href)' but expected '$([System.IO.Path]::GetFileNameWithoutExtension($prevExpected.Path))'"
            }
        }

        if ($nextExpected.Kind -eq 'none') {
            if (-not $nextLink -or $nextLink.Href -ne '.' -or $nextTarget) {
                $failures += "[Nav] $rel Next must be [_None_](.)"
            }
        } else {
            if (-not $nextTargetN) {
                $failures += "[Nav] $rel Next must link to the next page"
            } elseif (-not (Test-Path -LiteralPath $nextTargetN)) {
                $failures += "[Nav] $rel Next target does not exist: '$($nextLink.Href)'"
            } elseif ($nextTargetN -ne $nextExpectedN) {
                $failures += "[Nav] $rel Next target is '$($nextLink.Href)' but expected '$([System.IO.Path]::GetFileNameWithoutExtension($nextExpected.Path))'"
            }
        }
    }
} catch {
    $failures += "[Nav] Failed to validate footer navigation: $($_.Exception.Message)"
}

if ($failures.Count) {
    # Ensure we emit all failures even though the script runs with $ErrorActionPreference = 'Stop'.
    foreach ($f in $failures) {
        Write-Error -Message $f -ErrorAction Continue
    }
    exit 1
}
Write-Host 'Tutorial docs pass validation' -ForegroundColor Green
