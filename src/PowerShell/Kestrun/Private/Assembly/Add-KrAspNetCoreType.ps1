<#
    .SYNOPSIS
        Loads required ASP.NET Core assemblies for PowerShell usage.
    .PARAMETER Version
        The .NET version to target (e.g. net8, net9, net10).
#>
function Add-KrAspNetCoreType {
    [CmdletBinding()]
    [OutputType([bool])]
    param (
        [Parameter()]
        [ValidateSet('net8.0', 'net9.0', 'net10.0')]
        [string]$Version = 'net8.0'
    )
    $versionNumber = $Version -replace 'net(\d+).*', '$1'
    $dotnetPath = (Get-Command -Name 'dotnet' -ErrorAction Stop).Source
    $realDotnetPath = (Get-Item $dotnetPath).Target
    if (-not $realDotnetPath) { $realDotnetPath = $dotnetPath }elseif ($realDotnetPath -notmatch '^/') {
        # If the target is a relative path, resolve it from the parent of $dotnetPath (e.g. symlink in same folder)
        $realDotnetPath = Join-Path -Path (Split-Path -Path $dotnetPath -Parent) -ChildPath $realDotnetPath
        $realDotnetPath = [System.IO.Path]::GetFullPath($realDotnetPath)
    }
    $dotnetDir = Split-Path -Path $realDotnetPath -Parent
    if (-not $dotnetDir) {
        throw 'Could not determine the path to the dotnet executable.'
    }
    $baseDir = Join-Path -Path $dotnetDir -ChildPath 'shared\Microsoft.AspNetCore.App'
    if (-not (Test-Path -Path $baseDir -PathType Container)) {
        throw "ASP.NET Core shared framework not found at $baseDir."
    }
    $versionDirs = Get-ChildItem -Path $baseDir -Directory | Where-Object { $_.Name -like "$($versionNumber).*" } | Sort-Object Name -Descending
    foreach ($verDir in $versionDirs) {
        $assemblies = @()

        Get-ChildItem -Path $verDir.FullName -Filter '*.dll' |
            Where-Object {
                $_.Name -like 'Microsoft.*.dll' -or
                $_.Name -eq 'System.IO.Pipelines.dll'
            } | ForEach-Object {
                if ($assemblies -notcontains $_.Name) {
                    $assemblies += $_.Name
                }
            }
        $allFound = $true
        foreach ($asm in $assemblies) {
            $asmPath = Join-Path -Path $verDir.FullName -ChildPath $asm
            if (-not (Test-Path -Path $asmPath)) {
                Write-Verbose "Assembly $asm not found in $($verDir.FullName)"
                $allFound = $false
                break
            }
        }
        if ($allFound) {
            $result = $true
            foreach ($asm in $assemblies) {
                $asmPath = Join-Path -Path $verDir.FullName -ChildPath $asm
                $result = $result -and (Assert-KrAssemblyLoaded -AssemblyPath $asmPath)
            }
            Write-Verbose "Loaded ASP.NET Core assemblies from $($verDir.FullName)"
            return $result
        }
        return $false
    }

    Write-Error "Could not find ASP.NET Core assemblies for version $Version in $baseDir."
    Write-Warning "Please download the Runtime $Version from https://dotnet.microsoft.com/en-us/download/dotnet/$versionNumber.0"

    throw "Microsoft.AspNetCore.App version $Version not found in $baseDir."
}

