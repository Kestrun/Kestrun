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
    [ValidateSet('BeforeFunction', 'InsideBeforeParam', 'AfterFunction')]
    [string]$FunctionHelpPlacement = 'BeforeFunction',
    [switch]$ReformatFunctionHelp,
    [switch]$NoBomEncoding,
    [switch]$WhatIf
)

$today = Get-Date -Format 'yyyy-MM-dd'

<#
.SYNOPSIS
    Repositions comment-based help blocks (< ... >) for each function.
.DESCRIPTION
    Uses the PowerShell parser/AST to:
    - Find functions
    - Detect an associated help block located:
    * immediately before the function
    * inside the function at the very top (before param/first statement)
    * immediately after the function
    - move that block to the desired placement (BeforeFunction, InsideBeforeParam, AfterFunction)
    Idempotent: run multiple times; It will just keep the chosen placement.
#>
function Set-FunctionHelpPlacement {
    param(
        [Parameter(Mandatory)]
        [string]$Text,

        [Parameter(Mandatory)]
        [ValidateSet('BeforeFunction', 'InsideBeforeParam', 'AfterFunction')]
        [string]$Placement
    )

    $tokens = $null; $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseInput($Text, [ref]$tokens, [ref]$errors)
    if ($errors -and $errors.Count) { return $Text } # be safe on parse errors

    # Collect all block-comment tokens: "<# ... #>"
    $blockComments = @($tokens | Where-Object { $_.Kind -eq 'Comment' -and $_.Text -like '<#*#>' })

    # Find all functions
    $funcAsts = $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true)
    if (-not $funcAsts) { return $Text }
    <#
    .SYNOPSIS
        Checks if there is only whitespace between two offsets in a string.
    .DESCRIPTION
        This function checks if the substring of the given string between the specified start and end offsets consists
        only of whitespace characters (spaces, tabs, newlines).
    .PARAMETER s
        The string to check.
    .PARAMETER from
        The starting offset (inclusive).
    .PARAMETER to
        The ending offset (exclusive).
    .OUTPUTS
        [bool] Returns true if only whitespace is found between the specified offsets, false otherwise.
    .EXAMPLE
        Test-WhitespaceBetween "   \n\t  " 0 7
        Returns true.
    .EXAMPLE
        Test-WhitespaceBetween "abc def" 0 3
        Returns false.
    .EXAMPLE
        Test-WhitespaceBetween "abc def" 3 7
        Returns true.
    #>
    function Test-WhitespaceBetween {
        param($s, [int]$from, [int]$to)
        if ($from -lt 0 -or $to -gt $s.Length -or $to -lt $from) { return $false }
        return ([string]::IsNullOrWhiteSpace($s.Substring($from, $to - $from)))
    }

    <#
    .SYNOPSIS
        Computes a score for a help comment based on the presence of key tags and length.
    .DESCRIPTION
        This function evaluates a help comment block and assigns a score based on the presence of common help tags
        such as .SYNOPSIS, .DESCRIPTION, .PARAMETER, .EXAMPLE, and .NOTES. The score is also influenced by the length of the comment.
    .PARAMETER t
        The text of the help comment to evaluate.
    .OUTPUTS
        [int] Returns a score representing the quality of the help comment. Higher scores indicate more comprehensive help comments.
    .EXAMPLE
        Get-HelpScore -t @"
        .SYNOPSIS
            This is a sample function.
        .DESCRIPTION
            This function does something useful.
        .PARAMETER Name
            The name of the item.
        .EXAMPLE
            SampleFunction -Name "Test"
        .NOTES
            Additional notes about the function.
        "@
        Returns a score based on the presence of help tags and length of the comment.
    #>
    function Get-HelpScore {
        param([string]$t)
        $tags = @('.SYNOPSIS', '.DESCRIPTION', '.PARAMETER', '.EXAMPLE', '.NOTES')
        $score = 0
        foreach ($tag in $tags) { if ($t -match [regex]::Escape($tag)) { $score++ } }
        # length gives minor bias
        return ($score * 100000 + $t.Length)
    }

    $edits = New-Object System.Collections.Generic.List[object]

    foreach ($f in ($funcAsts | Sort-Object { $_.Extent.StartOffset } -Descending)) {
        $fStart = $f.Extent.StartOffset
        $fEnd = $f.Extent.EndOffset
        $brace = $f.Body.Extent.StartOffset  # '{'

        # Gather associated help blocks (always coerce to arrays!)
        $beforeBlocks = @(
            $blockComments | Where-Object {
                $_.Extent.EndOffset -le $fStart -and (Test-WhitespaceBetween $Text $_.Extent.EndOffset $fStart)
            }
        )
        # Consider "top-inside" as within first 200 chars after '{'
        $insideBlocks = @(
            $blockComments | Where-Object {
                $_.Extent.StartOffset -ge ($brace + 1) -and $_.Extent.StartOffset -le [Math]::Min($fEnd, $brace + 200)
            }
        )
        $afterBlocks = @(
            $blockComments | Where-Object {
                $_.Extent.StartOffset -ge $fEnd -and (Test-WhitespaceBetween $Text $fEnd $_.Extent.StartOffset)
            }
        )

        $assoc = @()
        if ($beforeBlocks) { $assoc += $beforeBlocks }
        if ($insideBlocks) { $assoc += $insideBlocks }
        if ($afterBlocks) { $assoc += $afterBlocks }

        if (-not $assoc -or $assoc.Count -eq 0) { continue }

        # Choose canonical help block
        $best = $assoc | Sort-Object { - (Get-HelpScore $_.Text) } | Select-Object -First 1
        $helpText = $best.Text

        # Remove ALL associated help blocks (prevent duplicates)
        foreach ($blk in $assoc) {
            $edits.Add([pscustomobject]@{
                    Type = 'Remove'
                    Start = $blk.Extent.StartOffset
                    End = $blk.Extent.EndOffset
                    Text = $null
                })
        }

        # Compute insertion
        switch ($Placement) {
            'BeforeFunction' {
                $insertOffset = $fStart
                $indent = (' ' * ($f.Extent.StartColumnNumber - 1))
                $insertion = "{0}{1}`n" -f $indent, $helpText
            }
            'InsideBeforeParam' {
                # Insert immediately AFTER '{'
                $insertOffset = $brace + 1

                # Strip any spaces/tabs immediately after '{' (same line) so our newline lands cleanly
                $wsStart = $insertOffset
                $wsEnd = $wsStart
                while ($wsEnd -lt $Text.Length -and ($Text[$wsEnd] -eq ' ' -or $Text[$wsEnd] -eq "`t")) { $wsEnd++ }
                if ($insertOffset -lt $Text.Length -and $Text[$insertOffset] -ne "`n" -and $wsEnd -gt $wsStart) {
                    $edits.Add([pscustomobject]@{
                            Type = 'Remove'
                            Start = $wsStart
                            End = $wsEnd
                            Text = $null
                        })
                }

                # Force newline after '{', then help, then newline
                $indent = (' ' * (($f.Extent.StartColumnNumber - 1) + 4))
                $insertion = "`n{0}{1}`n" -f $indent, $helpText.Replace("#>", "`t#>")
            }
            'AfterFunction' {
                $insertOffset = $fEnd
                $indent = (' ' * ($f.Extent.StartColumnNumber - 1))
                $insertion = "`n{0}{1}`n" -f $indent, $helpText
            }
        }

        # Insert the single help block
        $edits.Add([pscustomobject]@{
                Type = 'Insert'
                Start = $insertOffset
                End = $insertOffset
                Text = $insertion
            })
    }

    if ($edits.Count -eq 0) { return $Text }

    # Apply edits from end → start
    $sb = New-Object System.Text.StringBuilder($Text)
    foreach ($e in ($edits | Sort-Object { $_.Start } -Descending)) {
        if ($e.Type -eq 'Remove') {
            [void]$sb.Remove($e.Start, ($e.End - $e.Start))
        } else {
            [void]$sb.Insert($e.Start, $e.Text)
        }
    }

    return $sb.ToString()
}

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

<#
    .SYNOPSIS
        Checks if a given text contains an existing file header match.
    .DESCRIPTION
        This function checks if the provided text contains a file header that matches the expected format for Kestrun files.
    .PARAMETER Text
        The text content to check for an existing file header.
    .OUTPUTS
        Returns a regex match object if a valid file header is found; otherwise, returns $null.
#>
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
#------------------------------------------
#   File:      $relPath
#
#   Created:   $createdDate
#   $modifiedLine
#
#   Notes:
#       This file is part of the Kestrun framework.
#       https://www.kestrun.dev
#
#   License:
#       MIT License - See LICENSE.txt file in the project root for more information.
#------------------------------------------
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

        if ($ReformatFunctionHelp) {
            $body = Set-FunctionHelpPlacement -Text $body -Placement $FunctionHelpPlacement
            # normalize again after edits
            $body = $body -replace "`r`n", "`n"
            $body = $body -replace "`r", "`n"
            $body = [regex]::Replace($body, '[ \t]+(?=\n)', '')
            $body = [regex]::Replace($body, '\n*\z', "`n")
        }
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
