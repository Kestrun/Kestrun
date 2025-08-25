[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
param(
    [parameter(Mandatory = $true)]
    [string]$Path,
    [parameter()]
    [ValidateSet('Text', 'Binary')]
    [string]$Mode = 'Text',
    [parameter()]
    [long]$SizeMB = 100
)

# ─── Setup ────────────────────────────────────────────────────────
$targetBytes = $SizeMB * 1MB

Write-Host "🧪 Generating $Mode file: $Path ($SizeMB MB)..."

if (Test-Path $Path) {
    Remove-Item $Path
}

# ─── Generate Binary File ─────────────────────────────────────────
if ($Mode -eq 'Binary') {
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $buffer = [byte[]]::new(1MB)
    $written = 0
    try {
        $fs = [System.IO.File]::OpenWrite($Path)

        while ($written -lt $targetBytes) {
            $rng.GetBytes($buffer)
            $remaining = $targetBytes - $written
            $bytesToWrite = if ($buffer.Length -lt $remaining) { $buffer.Length } else { [long]$remaining }
            $fs.Write($buffer, 0, $bytesToWrite)
            $written += $bytesToWrite
            if ($written % (10MB) -eq 0) {
                # Print progress every 10MB
                Write-Host '#' -NoNewline
            }
        }
    } finally {
        $fs.Close()
    }
    Write-Host ' ✅' -ForegroundColor Green
}

# ─── Generate Compressible Text File ──────────────────────────────
if ($Mode -eq 'Text') {
    $baseLine = 'COMPRESSIBLE-DATA-LINE-1234567890'
    $entropyRate = 1000  # Inject entropy every N lines
    $lineTemplate = $baseLine * 5 + "`n"  # ~180–200 bytes
    $lineBytes = [System.Text.Encoding]::UTF8.GetByteCount($lineTemplate)
    $lineCount = [math]::Ceiling($targetBytes / $lineBytes)

    $rand = [System.Random]::new()
    try {
        $writer = [System.IO.StreamWriter]::new($Path, $false, [System.Text.Encoding]::UTF8)

        for ($i = 0; $i -lt $lineCount; $i++) {
            if ($i % $entropyRate -eq 0) {
                # Inject a small random string
                $randomSuffix = $rand.Next(100000, 999999)
                $line = "$baseLine-$randomSuffix" * 5 + "`n"
            } else {
                $line = $lineTemplate
            }
            # Write the line to the file
            $writer.Write($line)
            if ($i % (63700) -eq 0) {
                Write-Host '#' -NoNewline
            }
        }
    } finally {
        $writer.Close()
    }
    Write-Host ' ✅' -ForegroundColor Green
}

