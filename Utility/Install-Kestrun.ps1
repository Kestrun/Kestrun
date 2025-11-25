[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
[CmdletBinding()]
param(
    [Parameter()]
    [string]$FileVersion = './version.json',
    [Parameter()]
    [switch]$Remove
)

# Add Helper utility module
Import-Module -Name './Utility/Modules/Helper.psm1'

$PSPaths = if ($IsWindows) {
    $env:PSModulePath -split ';'
} else {
    $env:PSModulePath -split ':'
}
$Version = Get-Version -FileVersion $FileVersion -VersionOnly

$dest = Join-Path -Path $PSPaths[0] -ChildPath 'Kestrun' -AdditionalChildPath $Version

# Remove the module if requested
if ($Remove) {
    if (Test-Path -Path $dest) {
        Write-Host "🗑️ Deleting module from $dest"
        Remove-Item -Path $dest -Recurse -Force | Out-Null
    } else {
        Write-Warning "Directory $dest doesn't exist"
    }
    return
}

if (Test-Path -Path $dest) {
    if ($Force) {
        Remove-Item -Path $dest -Recurse -Force | Out-Null
    } else {
        Write-Warning "Directory $dest already exists. Use -Force to overwrite."
        return
    }
}

# create the dest dir
New-Item -Path $dest -ItemType Directory -Force | Out-Null
$path = './src/PowerShell/Kestrun/*'

# Copy the files to the destination
Copy-Item -Path $path -Destination $dest -Force -Recurse | Out-Null

# Confirm the deployment
Write-Host "🚀 Deployed to $dest"
