<#
    .SYNOPSIS
        Lists discovered localization cultures under a resources folder.
    .DESCRIPTION
        Scans the provided `ResourcesBasePath` (relative to current folder or content root) and lists subfolders
        that contain the localization file (default Messages.psd1).
    .PARAMETER ResourcesBasePath
        Path to the resources root (defaults to './Assets/i18n'). May be absolute or relative.
    .PARAMETER FileName
        Localization file name to look for (defaults to 'Messages.psd1').
    .PARAMETER ContentRoot
        Optional content root to resolve relative `ResourcesBasePath` against. Defaults to current directory.
    .EXAMPLE
        Get-KrLocalizationCultures -ResourcesBasePath './Assets/i18n'
#>
function Get-KrLocalizationCultures {
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$ResourcesBasePath = './Assets/i18n',

        [Parameter()]
        [string]$FileName = 'Messages.psd1',

        [Parameter()]
        [string]$ContentRoot = (Get-Location).Path
    )

    # Prefer returning runtime-loaded cultures from the active Kestrun host's localization store.
    try {
        $server = [Kestrun.KestrunHostManager]::Default
        if ($null -ne $server -and $null -ne $server.LocalizationStore) {
            $store = $server.LocalizationStore
            # AvailableCultures is an IReadOnlyCollection<string>
            $store.AvailableCultures | ForEach-Object {
                [PSCustomObject]@{
                    Name = $_
                }
            }
            return
        }
    } catch {
        # ignore and fall back to filesystem enumeration
    }

    $resolved = if (Test-Path $ResourcesBasePath) { Resolve-Path $ResourcesBasePath -ErrorAction SilentlyContinue } else { Resolve-Path (Join-Path $ContentRoot $ResourcesBasePath) -ErrorAction SilentlyContinue }
    if (-not $resolved) {
        Write-Error "Resources path '$ResourcesBasePath' not found (content root: $ContentRoot)."
        return
    }

    $root = $resolved.ProviderPath
    Get-ChildItem -Path $root -Directory | Where-Object { Test-Path (Join-Path $_.FullName $FileName) } | ForEach-Object {
        [PSCustomObject]@{
            Name = $_.Name
            Path = $_.FullName
            File = (Join-Path $_.FullName $FileName)
        }
    }
}
