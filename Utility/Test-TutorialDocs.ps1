param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Validates tutorial docs under docs/pwsh/tutorial according to the authoring guide
$root = Join-Path $PSScriptRoot '..'
$docs = Join-Path $root 'docs/pwsh/tutorial'

$failures = @()
Get-ChildItem -Path $docs -Recurse -Filter *.md | ForEach-Object {
    $path = $_.FullName
    $rel = $path.Substring($root.Length).TrimStart('\/')
    $name = $_.Name
    $text = Get-Content -Path $path -Raw

    # 1) Filename pattern N.Title.md
    if ($name -notmatch '^[0-9]+\.[^\\/]+\.md$') {
        $failures += "[Name] $rel does not match N.Title.md"
    }

    # Extract front matter
    if ($text -notmatch '(?s)^---\s*\n(.*?)\n---\s*') {
        $failures += "[FrontMatter] $rel missing YAML front matter"
    } else {
        $fm = [regex]::Match($text, '(?s)^---\s*\n(.*?)\n---\s*').Groups[1].Value
        $title = ($fm | Select-String -Pattern '^title:\s*(.+)$' -AllMatches).Matches.Value -replace '^title:\s*', ''
        $parent = ($fm | Select-String -Pattern '^parent:\s*(.+)$' -AllMatches).Matches.Value -replace '^parent:\s*', ''
        $nav = ($fm | Select-String -Pattern '^nav_order:\s*(.+)$' -AllMatches).Matches.Value -replace '^nav_order:\s*', ''
        if (-not $title) { $failures += "[FrontMatter] $rel missing title" }
        if (-not $parent) { $failures += "[FrontMatter] $rel missing parent" }
        if (-not $nav) { $failures += "[FrontMatter] $rel missing nav_order" }

        # 4) H1 must match title
        $h1 = ([regex]::Match($text, '^#\s+(.+)$', 'Multiline')).Groups[1].Value
        if ($h1 -ne $title) { $failures += "[H1] $rel H1 does not match title '$title'" }
    }

    # Ensure required sections exist
    foreach ($hdr in '## Full source', '## Step-by-step', '## Try it', '## References', '### Previous / Next') {
        if ($text -notmatch [regex]::Escape($hdr)) { $failures += "[Section] $rel missing '$hdr'" }
    }

    # Include directive present
    if ($text -notmatch '{% include examples/pwsh/.+?\.ps1 %}') {
        $failures += "[Include] $rel missing include directive"
    }

    # Code fences have language
    if ($text -match "```\n") { $failures += "[Fence] $rel has code fence without language" }

    # Step-by-step is numbered starting at 1
    if ($text -notmatch '## Step-by-step\s*\r?\n1\.') { $failures += "[Steps] $rel list must start at 1." }
}

if ($failures.Count) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}
Write-Host 'Tutorial docs pass validation' -ForegroundColor Green
