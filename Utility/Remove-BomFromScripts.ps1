<#!
.SYNOPSIS
    Removes a UTF-8 BOM from PowerShell and related script files.
.DESCRIPTION
    Scans provided paths (files or directories) for .ps1, .psm1, .psd1, .pssc, .psrc.
    If a file begins with the UTF-8 BOM (0xEF 0xBB 0xBF), rewrites it without the BOM.
    Leaves other encodings untouched (UTF-16, etc.) and skips already no-BOM UTF-8.
.PARAMETER Path
    One or more file or directory paths (supports glob patterns) to process.
.PARAMETER Recurse
    Recurse into directories.
.PARAMETER WhatIf
    Show actions without modifying files.
.EXAMPLE
    ./Utility/Remove-BomFromScripts.ps1 -Path ./src/PowerShell/Kestrun -Recurse
.EXAMPLE
    git ls-files *.ps1 | ./Utility/Remove-BomFromScripts.ps1
.NOTES
    Target policy: UTF-8 (no BOM) for PowerShell 7+ cross-platform friendliness.
#>
[CmdletBinding()]
param(
    [Parameter(ValueFromPipeline, ValueFromPipelineByPropertyName, Mandatory)]
    [string[]]$Path,
    [switch]$Recurse,
    [switch]$WhatIf
)

begin {
    $extensions = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    '.ps1', '.psm1', '.psd1', '.pssc', '.psrc','.cs' | ForEach-Object { [void] $extensions.Add($_) }
    # propagate WhatIf decision to helper without extra parameter (avoids duplicate ShouldProcess warnings)
    $script:InvokeWhatIf = $WhatIf.IsPresent

    <#
        .SYNOPSIS
            Removes the UTF-8 BOM from a file if present.
        .DESCRIPTION
            Scans the specified file for a UTF-8 BOM and removes it if found.
        .PARAMETER File
            The file path to process.
        .NOTES
            Uses ShouldProcess for -WhatIf support.
    #>
    function Remove-BomFile {
        [CmdletBinding(SupportsShouldProcess)]
        param(
            [Parameter(Mandatory)] [string] $File
        )
        if (-not (Test-Path -LiteralPath $File -PathType Leaf)) { return }
        $bytes = [System.IO.File]::ReadAllBytes($File)
        $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        if ($hasBom -and $PSCmdlet.ShouldProcess($File, 'Remove UTF-8 BOM')) {
            if ($script:InvokeWhatIf) {
                Write-Host "⚠️ Would remove BOM: $File"
            } else {
                $outBytes = $bytes[3..($bytes.Length - 1)]
                [System.IO.File]::WriteAllBytes($File, $outBytes)
                Write-Host "✅ Removed BOM: $File"
            }
        } else {
            Write-Verbose "No BOM: $File"
        }
    }
}
process {
    foreach ($p in $Path) {
        $resolved = @(Get-Item -LiteralPath $p -ErrorAction SilentlyContinue)
        if (-not $resolved) { $resolved = @(Get-ChildItem -Path $p -Recurse -ErrorAction SilentlyContinue) }
        foreach ($item in $resolved) {
            if ($item.PSIsContainer) {
                $files = if ($Recurse) { Get-ChildItem -LiteralPath $item.FullName -Recurse -File } else { Get-ChildItem -LiteralPath $item.FullName -File }
                foreach ($f in $files) {
                    if ($extensions.Contains($f.Extension)) { Remove-BomFile -File $f.FullName -WhatIf:$WhatIf }
                }
            } else {
                if ($extensions.Contains($item.Extension)) { Remove-BomFile -File $item.FullName -WhatIf:$WhatIf }
            }
        }
    }
}
end {}
