
param(
    [Parameter()]
    [string]$FileVersion = './version.json',
    [Parameter()]
    [string]$ArtifactsPath = './artifacts',
    [Parameter()]
    [switch]$SignModule
)

# Add Helper utility
. ./Utility/Helper.ps1

function Remove-CommentHelpBlock {
    param(
        [string]$Path,
        [string]$OutFile
    )
    try {
        # Load with original encoding as raw text
        $content = Get-Content $Path -Raw

        # Normalize all newlines to LF only
        $content = $content -replace "`r`n", "`n"  # CRLF → LF
        $content = $content -replace "`r", "`n"    # lone CR → LF (rare but safe)

        # Remove only the first <# ... #> block (comment-based help)
        # This leaves license headers intact
        $stripped = $content -replace '<#[\s\S]*?#>', ''

        # Collapse 3+ blank lines → exactly 2 LF (one visible blank line)
        $stripped = $stripped -replace "(\n){2,}", "`n"

        # Trim leading and trailing blank lines
        $stripped = $stripped.Trim()

        # Save with UTF-8 (no BOM) and LF newlines
        Set-Content -Path $OutFile -Value $stripped -NoNewline -Encoding utf8
    } catch {
        Write-Error "Error processing file $($Path): $_"
    }
}


$Version = Get-Version -FileVersion $FileVersion -VersionOnly

$artifactsPath = Join-Path -Path $ArtifactsPath -ChildPath 'modules' -AdditionalChildPath 'Kestrun', $Version
if ( (Test-Path -Path $artifactsPath)) {
    Remove-Item -Path $artifactsPath -Recurse -Force | Out-Null
}
New-Item -Path $artifactsPath -ItemType Directory -Force | Out-Null

$psm1Path = Join-Path -Path $artifactsPath -ChildPath 'Kestrun.psm1'
$privateModulePath = Join-Path -Path $artifactsPath -ChildPath 'Private.ps1'
$publicModulePath = Join-Path -Path $artifactsPath -ChildPath 'Public.ps1'


# First: psm1 file
Copy-Item -Path src/PowerShell/Kestrun/Kestrun.psm1 -Destination $psm1Path -Force

# private helpers
Get-ChildItem src/PowerShell/Kestrun/Private/*.ps1 -Recurse | ForEach-Object {
    $tmp = New-TemporaryFile
    Remove-CommentHelpBlock -Path $_.FullName -OutFile $tmp
    Add-Content -Path $privateModulePath -Value (Get-Content $tmp -Raw)
   # Add-Content -Path $privateModulePath -Value "`n"
}

# Then: public functions
Get-ChildItem src/PowerShell/Kestrun/Public/*.ps1 -Recurse | ForEach-Object {
    $tmp = New-TemporaryFile
    Remove-CommentHelpBlock -Path $_.FullName -OutFile $tmp
    Add-Content -Path $publicModulePath -Value (Get-Content $tmp -Raw)
  #  Add-Content -Path $publicModulePath -Value "`n"
}

if ($SignModule) {
    Write-Host "Signing module at $psm1Path"
    Set-AuthenticodeSignature -FilePath $psm1Path -Certificate $cert | Out-Null
    Write-Host "Module file created and signed at $psm1Path"


    Write-Host "Signing module at $publicModulePath"
    # Sign the final module
    $cert = Get-Item Cert:\CurrentUser\My\<thumbprint>
    Set-AuthenticodeSignature -FilePath $publicModulePath -Certificate $cert | Out-Null
    Write-Host "Public module created and signed at $publicModulePath"

    Write-Host "Signing module at $privateModulePath"
    # Sign the final module
    $cert = Get-Item Cert:\CurrentUser\My\<thumbprint>
    Set-AuthenticodeSignature -FilePath $privateModulePath -Certificate $cert | Out-Null
    Write-Host "Private module created and signed at $privateModulePath"
} else {
    Write-Host "Module created at $artifactsPath (not signed)"
    return
}
