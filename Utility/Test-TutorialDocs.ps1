param(
    [string]$Path='docs/pwsh/tutorial',
    [string]$SubPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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
    $text = Get-Content -Path $itemPath -Raw

    # Skip index pages from strict checks
    if ($name -ieq 'index.md') { return }

    # 1) Filename pattern N.Title.md
    if ($name -notmatch '^[0-9]+\.[^\\/]+\.md$') {
        $failures += "[Name] $rel does not match N.Title.md"
    }

    # Extract front matter
    if ($text -notmatch '(?s)^---\s*\n(.*?)\n---\s*') {
        $failures += "[FrontMatter] $rel missing YAML front matter"
    } else {
        $fm = [regex]::Match($text, '(?s)^---\s*\n(.*?)\n---\s*').Groups[1].Value
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
    foreach ($hdr in '## Full source', '## Step-by-step', '## Try it', '## Troubleshooting', '## References', '### Previous / Next') {
        if ($text -notmatch [regex]::Escape($hdr)) { $failures += "[Section] $rel missing '$hdr'" }
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
    } else {
        $failures += "[Section] $rel missing '## References' content"
    }

    # Step-by-step is numbered starting at 1
    if ($text -notmatch '## Step-by-step\s*\r?\n1\.') { $failures += "[Steps] $rel list must start at 1." }
}

if ($failures.Count) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}
Write-Host 'Tutorial docs pass validation' -ForegroundColor Green
