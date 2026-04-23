param(
    [string]$Root = (Get-Location).Path,
    [string[]]$Extensions = @('*.ps1', '*.psm1', '*.cs', '*.md', '*.psd1')
)

Write-Host "🔄 Normalizing files under $Root"

$utf8NoBom = [System.Text.UTF8Encoding]::new($false, $true) # no BOM, throw on invalid
$badFiles = [System.Collections.Generic.List[string]]::new()

Get-ChildItem -Path $Root -Recurse -Include $Extensions |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    ForEach-Object {
        $path = $_.FullName
        $bytes = [System.IO.File]::ReadAllBytes($path)

        # --- BOM detection ---
        $hasUtf8Bom  = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        $hasUtf16Le  = $bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE
        $hasUtf16Be  = $bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF
        $hasUtf32Le  = $bytes.Length -ge 4 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE -and $bytes[2] -eq 0x00 -and $bytes[3] -eq 0x00
        $hasUtf32Be  = $bytes.Length -ge 4 -and $bytes[0] -eq 0x00 -and $bytes[1] -eq 0x00 -and $bytes[2] -eq 0xFE -and $bytes[3] -eq 0xFF

        if ($hasUtf16Le -or $hasUtf16Be -or $hasUtf32Le -or $hasUtf32Be) {
            $badFiles.Add("$path (UTF-16/UTF-32)")
            return
        }

        try {
            $contentBytes = if ($hasUtf8Bom) { $bytes[3..($bytes.Length - 1)] } else { $bytes }
            $text = $utf8NoBom.GetString($contentBytes)
        }
        catch {
            $badFiles.Add("$path (invalid UTF-8)")
            return
        }

        # --- normalize ---
        $newContent = $text -replace "`r`n", "`n"
        $newContent = $newContent -replace "`r", "`n"
        $newContent = [regex]::Replace($newContent, '[ \t]+(?=\n)', '')

        if (-not $newContent.EndsWith("`n")) {
            $newContent += "`n"
        }

        $needsRewrite = $hasUtf8Bom -or ($newContent -ne $text)

        if ($needsRewrite) {
            [System.IO.File]::WriteAllText($path, $newContent, $utf8NoBom)
            Write-Host "✔ Fixed $path"
        }
    }

if ($badFiles.Count -gt 0) {
    Write-Host "`n❌ Non UTF-8 files detected:" -ForegroundColor Red
    $badFiles | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    throw "Encoding validation failed."
}

Write-Host "✅ Normalization complete"
