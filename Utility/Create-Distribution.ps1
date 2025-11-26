param(
    [Parameter()]
    [string]$FileVersion = './version.json',
    [Parameter()]
    [string]$ArtifactsPath = './artifacts',
    [Parameter()]
    [switch]$SignModule
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path -Path $FileVersion)) {
    throw "Version file $FileVersion not found"
}
$kestrunProjectPath = './src/CSharp/Kestrun/Kestrun.csproj'
$kestrunSrcPath = './src/PowerShell/Kestrun'
if (-not (Test-Path -Path "$kestrunSrcPath/Kestrun.psm1" -PathType Leaf)) {
    throw 'Kestrun.psm1 file not found in expected location'
}
if (-not (Test-Path -Path "$kestrunSrcPath/Kestrun.psd1" -PathType Leaf)) {
    throw 'Kestrun.psd1 file not found in expected location'
}
if (-not (Test-Path -Path "$kestrunSrcPath/Private" -PathType Container)) {
    throw 'Private folder not found in expected location'
}
if (-not (Test-Path -Path "$kestrunSrcPath/Public" -PathType Container)) {
    throw 'Public folder not found in expected location'
}

if (-not (Test-Path -Path "$kestrunSrcPath/Formats" -PathType Container)) {
    throw 'Formats folder not found in expected location'
}
if (-not (Test-Path -Path "$kestrunSrcPath/en-US/Kestrun/Kestrun-Help.xml" -PathType Leaf)) {
    throw 'Help XML file not found in expected location'
}

# Add Helper utility module
Import-Module -Name './Utility/Modules/Helper.psm1'

$Version = Get-Version -FileVersion $FileVersion -VersionOnly

$artifactsPath = Join-Path -Path $ArtifactsPath -ChildPath 'modules' -AdditionalChildPath 'Kestrun', $Version
if (Test-Path -Path $artifactsPath) {
    Write-Host "🧹 Cleaning existing distribution at $artifactsPath"
    Remove-Item -Path $artifactsPath -Recurse -Force | Out-Null
}
New-Item -Path $artifactsPath -ItemType Directory -Force | Out-Null

$psm1Path = Join-Path -Path $artifactsPath -ChildPath 'Kestrun.psm1'
$privateModulePath = Join-Path -Path $artifactsPath -ChildPath 'Private.ps1'
$routePublicPath = Join-Path -Path $artifactsPath -ChildPath 'Public-Route.ps1'
$definitionPublicPath = Join-Path -Path $artifactsPath -ChildPath 'Public-Definition.ps1'

Write-Host "📦 Creating module distribution at $artifactsPath"
Write-Host "🔍 Version: $Version"


# 1. Figure out which public functions are "in route runspace" using Get-KrCommandsByContext
Write-Host '🔍 Analyzing public functions for runtime context...'
# Import the dev module so commands & Get-KrCommandsByContext exist
Import-Module ./src/PowerShell/Kestrun/Kestrun.psd1 -Force -ErrorAction Stop

Write-Host '📋 Gathering all functions from Kestrun module...'
# All Kestrun functions (you can restrict to exported if you want)
$allFuncs = Get-Command -Module Kestrun -CommandType Function
Write-Host "🔍 Found $($allFuncs.Count) functions in Kestrun module."
# Get those whose KestrunRuntimeApi context includes "Runtime"
$routeCmds = Get-KrCommandsByContext -AnyOf Runtime -Functions $allFuncs
Write-Host "🚀 Identified $($routeCmds.Count) functions for route runspace."
$routeNames = $routeCmds.Name

# 3. Build Private.ps1 (all private helpers, same as before)
Write-Host '🛠️ Building Private.ps1...'
Get-ChildItem "$kestrunSrcPath/Private/*.ps1" -Recurse | ForEach-Object {
    $content = Remove-CommentHelpBlock -Path $_.FullName
    $content | Out-File $privateModulePath -Append -Encoding utf8
    #  Add-Content -Path $privateModulePath -Value "`n"
}

# 4. Build Public.Route.ps1 and Public.Definition.ps1
Write-Host '🛠️ Building Public.Route.ps1 and Public.Definition.ps1...'
#    Split based on function name <-> file name
Get-ChildItem "$kestrunSrcPath/Public/*.ps1" -Recurse | ForEach-Object {
    $fnName = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)

    $targetPath = if ($routeNames -contains $fnName) {
        $routePublicPath
    } else {
        $definitionPublicPath
    }
    $content = Remove-CommentHelpBlock -Path $_.FullName
    $content | Out-File $targetPath -Append -Encoding utf8
}
# 5. Build the module manifest
Write-Host '🛠️ Updating module manifest...'
& .\Utility\Update-Manifest.ps1
Write-Host '📦 Copying module manifest...'
Copy-Item -Path "$kestrunSrcPath/Kestrun.psd1" -Destination (Join-Path -Path $artifactsPath -ChildPath 'Kestrun.psd1') -Force

# 6. Build the help files
Write-Host '🛠️ Building help files...'
pwsh -NoProfile -File .\Utility\Build-Help.ps1 -ModulePath "$kestrunSrcPath/Kestrun.psd1" -XmlFolder $artifactsPath -XmlCulture 'en-US'
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build help files"
}
Move-Item -Path "$artifactsPath/en-US/Kestrun/Kestrun-Help.xml" -Destination "$artifactsPath/en-US/Kestrun-help.xml" -Force -ErrorAction Stop
Remove-Item -Path "$artifactsPath/en-US/Kestrun" -Recurse -Force -ErrorAction Stop
# 7. Copy additional module files
Write-Host '📦 Copying additional module files...'
Copy-Item -Path "$kestrunSrcPath/Kestrun.psm1" -Destination (Join-Path -Path $artifactsPath -ChildPath 'Kestrun.psm1') -Force
Copy-Item -Path "$kestrunSrcPath/Formats" -Destination (Join-Path -Path $artifactsPath -ChildPath 'Formats') -Recurse -Force

# 8. Generate and copy THIRD-PARTY-NOTICES.md, LICENSE.txt, README.md
Write-Host '📄 Generating and copying THIRD-PARTY-NOTICES.md...'
& .\Utility\Update-ThirdPartyNotices.ps1 -Version $Version -Path (Join-Path -Path $artifactsPath -ChildPath 'THIRD-PARTY-NOTICES.md')
if ($LASTEXITCODE -ne 0) {
    throw "Failed to generate THIRD-PARTY-NOTICES.md"
}

# 9. Copy LICENSE.txt and README.md
Copy-Item -Path './LICENSE.txt' -Destination (Join-Path -Path $artifactsPath -ChildPath 'LICENSE.txt') -Force
Copy-Item -Path './README.md' -Destination (Join-Path -Path $artifactsPath -ChildPath 'README.md') -Force

# 10. Build and copy the DLLs
Write-Host '🛠️ Building Kestrun.dll and copying to module lib folder'
$destReleaseLib = (Join-Path -Path $artifactsPath -ChildPath 'lib')
Remove-Item -Path "$artifactsPath/lib" -Recurse -Force -ErrorAction SilentlyContinue
dotnet build $kestrunProjectPath -c Release
Sync-PowerShellDll -Configuration 'Release' -dest $destReleaseLib
Write-Host "📦 DLLs copied to $destReleaseLib"

# Summary
Write-Host
Write-Host
Write-Host 'Summary of created module distribution:' -ForegroundColor Green
Write-Host '-------------------------------'
Write-Host '✅ Module manifest created.'
Write-Host '✅ THIRD-PARTY-NOTICES.md created.'
Write-Host '✅ Public scripts built.'
Write-Host '✅ Private script built.'
Write-Host '✅ Module components created.'
Write-Host '✅ Additional files copied.'
Write-Host '✅ DLLs built and copied.'

Write-Host "📦 Module distribution ready at: $artifactsPath"
# 11. (Optional) Sign the module files during
if ($SignModule) {
    Write-Host '🔐 Signing module files...'
    # Get certificate once
    $cert = Get-Item Cert:\CurrentUser\My\<thumbprint>

    Write-Host "Signing module at $psm1Path"
    Set-AuthenticodeSignature -FilePath $psm1Path -Certificate $cert | Out-Null

    Write-Host "Signing module at $privateModulePath"
    Set-AuthenticodeSignature -FilePath $privateModulePath -Certificate $cert | Out-Null

    Write-Host "Signing module at $routePublicPath"
    Set-AuthenticodeSignature -FilePath $routePublicPath -Certificate $cert | Out-Null

    Write-Host "Signing module at $definitionPublicPath"
    Set-AuthenticodeSignature -FilePath $definitionPublicPath -Certificate $cert | Out-Null

    Write-Host "Module files created and signed in $artifactsPath"
} else {
    Write-Host "Module created at $artifactsPath (not signed)"
}
