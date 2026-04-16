[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$IncludeLogs
)

function Remove-BikeRentalArtifact {
    <#
    .SYNOPSIS
        Removes a generated BikeRentalShop artifact folder when it exists.
    .DESCRIPTION
        This helper deletes one generated data, certificate, or log path for the BikeRentalShop
        samples while honoring ShouldProcess so callers can use -WhatIf or -Confirm.
    .PARAMETER Path
        The artifact path to remove.
    .PARAMETER Description
        A short label describing the artifact path in status output.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Host "Skipped ${Description}: ${Path} does not exist."
        return
    }

    if ($PSCmdlet.ShouldProcess($Path, "Remove $Description")) {
        Remove-Item -LiteralPath $Path -Recurse -Force
        Write-Host "Removed ${Description}: ${Path}"
    }
}

$targets = @(
    [pscustomobject]@{
        Path = Join-Path $PSScriptRoot 'certs'
        Description = 'shared root certificate cache'
    }
    [pscustomobject]@{
        Path = Join-Path $PSScriptRoot 'Concurrent/data'
        Description = 'Concurrent sample data and local certificates'
    }
    [pscustomobject]@{
        Path = Join-Path $PSScriptRoot 'Synchronized/data'
        Description = 'Synchronized sample data and local certificates'
    }
    [pscustomobject]@{
        Path = Join-Path $PSScriptRoot 'Web/data'
        Description = 'Web sample data and local certificates'
    }
)

if ($IncludeLogs) {
    $targets += @(
        [pscustomobject]@{
            Path = Join-Path $PSScriptRoot 'Concurrent/logs'
            Description = 'Concurrent sample logs'
        }
        [pscustomobject]@{
            Path = Join-Path $PSScriptRoot 'Synchronized/logs'
            Description = 'Synchronized sample logs'
        }
        [pscustomobject]@{
            Path = Join-Path $PSScriptRoot 'Web/logs'
            Description = 'Web sample logs'
        }
    )
}

foreach ($target in $targets) {
    Remove-BikeRentalArtifact -Path $target.Path -Description $target.Description
}
