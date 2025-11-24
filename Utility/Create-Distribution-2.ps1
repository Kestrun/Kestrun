param(
    [Parameter()]
    [string]$FileVersion = './version.json',
    [Parameter()]
    [string]$ArtifactsPath = './artifacts',
    [Parameter()]
    [switch]$SignModule
)

# Helper utilities
. ./Utility/Helper.ps1
. src/PowerShell/Kestrun/Private/Assembly/Get-KrCommandByContext.ps1
function Remove-CommentHelpBlock {
    param(
        [string]$Path
    #    [string]$OutFile
    )

    $content = Get-Content $Path -Raw

    # Normalize newlines to LF
    $content = $content -replace "`r`n", "`n"
    $content = $content -replace "`r", "`n"

    # Strip the first <# ... #> block (comment-based help)
    $stripped = $content -replace '<#[\s\S]*?#>', ''

    # Collapse 3+ blank lines → one blank line
    $stripped = $stripped -replace "(\n){3,}", "`n`n"

    # Trim leading/trailing blank lines
    return $stripped.Trim()
}

$Version = Get-Version -FileVersion $FileVersion -VersionOnly

$artifactsPath = Join-Path -Path $ArtifactsPath -ChildPath 'modules' -AdditionalChildPath 'Kestrun', $Version
if (Test-Path -Path $artifactsPath) {
    Remove-Item -Path $artifactsPath -Recurse -Force | Out-Null
}
New-Item -Path $artifactsPath -ItemType Directory -Force | Out-Null

$psm1Path = Join-Path -Path $artifactsPath -ChildPath 'Kestrun.psm1'
$privateModulePath = Join-Path -Path $artifactsPath -ChildPath 'Private.ps1'
$routePublicPath = Join-Path -Path $artifactsPath -ChildPath 'Public.Route.ps1'
$otherPublicPath = Join-Path -Path $artifactsPath -ChildPath 'Public.Other.ps1'

# 1. Copy main psm1 (loader that dot-sources Private/Public files)
Copy-Item -Path src/PowerShell/Kestrun/Kestrun.psm1 -Destination $psm1Path -Force

# 2. Figure out which public functions are "in route runspace" using Get-KrCommandsByContext

# Import the dev module so commands & Get-KrCommandsByContext exist
Import-Module ./src/PowerShell/Kestrun/Kestrun.psd1 -Force

# All Kestrun functions (you can restrict to exported if you want)
$allFuncs = Get-Command -Module Kestrun -CommandType Function

# Get those whose KestrunRuntimeApi context includes "Runtime"
$routeCmds =   Get-KrCommandsByContext -AnyOf Runtime -Functions $allFuncs

$routeNames = $routeCmds.Name

# 3. Build Private.ps1 (all private helpers, same as before)

Get-ChildItem src/PowerShell/Kestrun/Private/*.ps1 -Recurse | ForEach-Object {
  #  $tmp = New-TemporaryFile
    #   Remove-CommentHelpBlock -Path $_.FullName -OutFile $tmp
    #  Add-Content -Path $privateModulePath -Value (Get-Content $tmp -Raw)
    $content = Remove-CommentHelpBlock -Path $_.FullName
    $content | Out-File $privateModulePath -Append -Encoding utf8
  #  Add-Content -Path $privateModulePath -Value "`n"
}

# 4. Build Public.Route.ps1 and Public.Other.ps1
#    Split based on function name <-> file name

Get-ChildItem src/PowerShell/Kestrun/Public/*.ps1 -Recurse | ForEach-Object {
    $fnName = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)

    $targetPath = if ($routeNames -contains $fnName) {
        $routePublicPath
    } else {
        $otherPublicPath
    }

  #  $tmp = New-TemporaryFile
  #  Remove-CommentHelpBlock -Path $_.FullName -OutFile $tmp
  #  Add-Content -Path $targetPath -Value (Get-Content $tmp -Raw)
  #  Add-Content -Path $targetPath -Value "`n"
    $content = Remove-CommentHelpBlock -Path $_.FullName
    $content | Out-File $targetPath -Append -Encoding utf8
}

if ($SignModule) {
    # Get certificate once
    $cert = Get-Item Cert:\CurrentUser\My\<thumbprint>

    Write-Host "Signing module at $psm1Path"
    Set-AuthenticodeSignature -FilePath $psm1Path -Certificate $cert | Out-Null

    Write-Host "Signing module at $privateModulePath"
    Set-AuthenticodeSignature -FilePath $privateModulePath -Certificate $cert | Out-Null

    Write-Host "Signing module at $routePublicPath"
    Set-AuthenticodeSignature -FilePath $routePublicPath -Certificate $cert | Out-Null

    Write-Host "Signing module at $otherPublicPath"
    Set-AuthenticodeSignature -FilePath $otherPublicPath -Certificate $cert | Out-Null

    Write-Host "Module files created and signed in $artifactsPath"
} else {
    Write-Host "Module created at $artifactsPath (not signed)"
}
