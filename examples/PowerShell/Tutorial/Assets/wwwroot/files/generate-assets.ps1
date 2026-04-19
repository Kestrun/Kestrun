<#!
    Helper script to regenerate sample static assets (binary files) if needed.
    It overwrites minimal.docx, minimal.xlsx, and placeholder.png.
!#>

$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path

# Tiny 10x10 PNG (same as existing)
$pngBase64 = 'iVBORw0KGgoAAAANSUhEUgAAAAoAAAAKCAYAAACNMs+9AAAAIUlEQVQoU2NkQAKMgmEY/0eMGRgYGJqA8Y8RjhgFo2E0GgAAgDEfA9Nn3IkAAAAASUVORK5CYII='
[IO.File]::WriteAllBytes((Join-Path $here 'placeholder.png'), [Convert]::FromBase64String($pngBase64))

# Minimal DOCX + XLSX (already base64 zipped OOXML containers)
$docx = Get-Content -Raw -Path (Join-Path $here 'minimal.docx.base64') -ErrorAction SilentlyContinue
if ($docx) { [IO.File]::WriteAllBytes((Join-Path $here 'minimal.docx'), [Convert]::FromBase64String($docx)) }

$xlsx = Get-Content -Raw -Path (Join-Path $here 'minimal.xlsx.base64') -ErrorAction SilentlyContinue
if ($xlsx) { [IO.File]::WriteAllBytes((Join-Path $here 'minimal.xlsx'), [Convert]::FromBase64String($xlsx)) }

Write-Host 'Assets regenerated.' -ForegroundColor Green
