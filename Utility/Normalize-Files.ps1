<#
.SYNOPSIS
    Normalizes PowerShell source files (LF, no trailing spaces, single EOF newline),
    and enforces a consistent header/footer with Created/Modified tracking.

.DESCRIPTION
    - Converts CRLF / CR to LF
    - Trims trailing spaces/tabs before newlines
    - Ensures exactly one newline at EOF
    - Inserts or updates a header block (preserves Created date; updates Modified)
    - Inserts or refreshes a footer block
    - Optionally includes git author + short commit hash for the Modified line

.PARAMETER Root
    Root directory to scan. Default is "$PSScriptRoot/src/PowerShell/Kestrun".

.PARAMETER Skip
    Folder names to skip (e.g., bin, obj, .git). Default: bin,obj,.git,.vs,node_modules,vendor,coverage.

.PARAMETER IncludeGitMeta
    If set, the Modified line includes latest git author + short commit hash.

.PARAMETER NoBomEncoding
    If set, writes files as UTF-8 *without* BOM. By default, files are saved with UTF-8 BOM.

.PARAMETER WhatIf
    If set, shows what would change without writing files.

.EXAMPLE
    .\Normalize-Files.ps1 -IncludeGitMeta

.EXAMPLE
    .\Normalize-Files.ps1 -Root ./src/PowerShell/Kestrun -NoBomEncoding -WhatIf
#>
param(
    [string]$Root = (Join-Path $PSScriptRoot 'src/PowerShell/Kestrun'),
    [string[]]$Skip = @('bin', 'obj', '.git', '.vs', 'node_modules', 'vendor', 'coverage'),
    [switch]$IncludeGitMeta,
    [switch]$NoBomEncoding,
    [switch]$WhatIf
)

$today = Get-Date -Format 'yyyy-MM-dd'

<#
    .SYNOPSIS
        Retrieves git metadata for a specified file, including the latest commit hash and author.
    .DESCRIPTION
        This function uses git commands to extract metadata information from the specified file.
    .PARAMETER RepoRoot
        The root directory of the git repository.
    .PARAMETER FilePath
        The full path to the file for which to retrieve metadata.
    .EXAMPLE
        Get-GitMeta -RepoRoot 'C:\Path\To\Repo' -FilePath 'C:\Path\To\Repo\src\MyFile.ps1'
#>
function Get-GitMeta {
    param([string]$RepoRoot, [string]$FilePath)
    try {
        $rel = [System.IO.Path]::GetRelativePath($RepoRoot, $FilePath)
        $log = git -C $RepoRoot log -1 --pretty='%h|%an' -- "$rel" 2>$null
        if ($LASTEXITCODE -eq 0 -and $log) {
            $parts = $log -split '\|', 2
            [pscustomobject]@{ Hash = $parts[0]; Author = $parts[1] }
        }
    } catch {
        Write-Warning "Failed to get git metadata for file '$FilePath': $_"
    }
}

# returns a match ONLY if the first <# ... #> block looks like our file header
function Get-ExistingFileHeaderMatch {
    param([string]$Text)
    # match the very first block comment at BOF
    $m = [regex]::Match($Text, '^\s*<#[\s\S]*?#>\s*\n?', 'Singleline')
    if (-not $m.Success) { return $null }

    $block = $m.Value
    # Heuristics: treat as our header only if it contains "File:" and "Created:" labels
    $isOurs =
    ($block -match '(?m)^\s*File:\s') -and
    ($block -match '(?m)^\s*Created:\s')

    if ($isOurs) { return $m } else { return $null }
}

Get-ChildItem -Path $Root -Recurse -File -Include *.ps1, *.psm1, *.psd1 |
    Where-Object { $Skip -notcontains $_.Directory.Name } |
    ForEach-Object {
        $file = $_.FullName

        # Read raw
        $text = Get-Content -LiteralPath $file -Raw -Encoding UTF8

        # --- Normalize to LF & whitespace ---
        $fixed = $text -replace "`r`n", "`n"
        $fixed = $fixed -replace "`r", "`n"
        $fixed = [regex]::Replace($fixed, '[ \t]+(?=\n)', '')
        $fixed = [regex]::Replace($fixed, '\n*\z', "`n")

        $relPath = [System.IO.Path]::GetRelativePath($Root, $file) -replace '\\', '/'

        $meta = if ($IncludeGitMeta) { Get-GitMeta -RepoRoot $Root -FilePath $file }
        $modifiedLine = if ($meta) {
            "Modified:  $today by $($meta.Author) (commit $($meta.Hash))"
        } else {
            "Modified:  $today"
        }

        # Check if an existing FILE HEADER is present (does NOT match function help)
        $hdrMatch = Get-ExistingFileHeaderMatch -Text $fixed

        # if header exists, keep its Created date; else initialize it
        $createdDate =
        if ($hdrMatch) {
            $m = [regex]::Match($hdrMatch.Value, '(?m)^\s*Created:\s*(\d{4}-\d{2}-\d{2})')
            if ($m.Success) { $m.Groups[1].Value } else { $today }
        } else { $today }

        $headerText = @"
<#
    File:      $relPath

    Created:   $createdDate
    $modifiedLine

    Notes:
        This file is part of the Kestrun framework.
        https://www.kestrun.dev

    License:
        MIT License - See LICENSE.txt file in the project root for the full license information.
#>
"@

        # Remove ONLY our file header if present; otherwise keep whatever is there (e.g., <# .SYNOPSIS #>)
        if ($hdrMatch) {
            $fixed = $fixed.Substring($hdrMatch.Length)
            # Trim extra blank lines after removing our header
            $fixed = [regex]::Replace($fixed, '^\n+', '')
            $body = $fixed
            $prependHeader = $false
        } else {
            # No file header detected → we will prepend our header and leave any existing help intact
            $body = $fixed
            $prependHeader = $true
        }

        # Remove ONLY our footer variant (be strict: must include "End of ")
        $footerPattern = "(?ms)\n?#-+\s*\n#\s*End of\s+.+\n#-+\s*\n*\z"
        $body = [regex]::Replace($body, $footerPattern, "`n")

        $footerText = @"
#------------------------------------------
# End of $relPath
#------------------------------------------
"@

        # Reassemble
        $newContent =
        if ($prependHeader) {
            $headerText.TrimEnd() + "`n`n" + $body.TrimEnd() + "`n" + $footerText.TrimEnd() + "`n"
        } else {
            # we removed an old file header above; replace with the new one
            $headerText.TrimEnd() + "`n`n" + $body.TrimEnd() + "`n" + $footerText.TrimEnd() + "`n"
        }

        # Final safety normalization
        $newContent = $newContent -replace "`r`n", "`n"
        $newContent = $newContent -replace "`r", "`n"
        $newContent = [regex]::Replace($newContent, '[ \t]+(?=\n)', '')
        $newContent = [regex]::Replace($newContent, '\n*\z', "`n")

        if ($newContent -ne $text) {
            if ($WhatIf) {
                Write-Host "⚠️ Would update: $relPath"
            } else {
                $enc = if ($NoBomEncoding) { [System.Text.UTF8Encoding]::new($false) } else { [System.Text.UTF8Encoding]::new($true) }
                [System.IO.File]::WriteAllText($file, $newContent, $enc)
                Write-Host "🔧 Updated: $relPath"
            }
        }
    }
