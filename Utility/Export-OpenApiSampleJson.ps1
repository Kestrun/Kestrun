<#
.SYNOPSIS
    Runs OpenAPI tutorial samples and exports generated OpenAPI JSON documents.

.DESCRIPTION
    Discovers PowerShell tutorial samples under docs/_includes/examples/pwsh whose names start with 10.
    and contain OpenAPI, executes each sample using the same lifecycle helpers used by Pester tests,
    and saves generated OpenAPI documents as JSON under Assets/OpenAPI/v<version>.

.PARAMETER ExamplesPath
    Repository-relative path containing tutorial sample scripts.

.PARAMETER OutputRoot
    Repository-relative path where versioned JSON output folders are written.

.PARAMETER Versions
    OpenAPI versions to export (for example: 3.0, 3.1, 3.2).

.PARAMETER SampleNames
    Optional sample base names to run. If omitted, all matching OpenAPI 10.x samples are processed.

.PARAMETER ContinueOnError
    Continue processing remaining samples when one sample fails.

.EXAMPLE
    pwsh -NoLogo -NoProfile -File ./Utility/Export-OpenApiSampleJson.ps1

.EXAMPLE
    pwsh -NoLogo -NoProfile -File ./Utility/Export-OpenApiSampleJson.ps1 -SampleNames '10.15-OpenAPI-Museum'

.EXAMPLE
    Invoke-Build Export-OpenApiSamples
#>
param(
    [string]$ExamplesPath = 'docs/_includes/examples/pwsh',
    [string]$OutputRoot = 'docs/_includes/examples/pwsh/Assets/OpenAPI',
    [string[]]$Versions = @('3.0', '3.1', '3.2'),
    [string[]]$SampleNames,
    [switch]$ContinueOnError
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
    Resolves the repository root from the utility script location.
#>
function Get-RepositoryRoot {
    [CmdletBinding()]
    param()

    return (Resolve-Path -Path (Join-Path $PSScriptRoot '..')).Path
}

<#
.SYNOPSIS
    Returns OpenAPI tutorial sample script files to process.

.PARAMETER FolderPath
    Absolute path to the samples directory.

.PARAMETER Names
    Optional sample names to filter by.
#>
function Get-OpenApiSampleFiles {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FolderPath,
        [string[]]$Names
    )

    if (-not (Test-Path -LiteralPath $FolderPath)) {
        throw "Examples folder not found: $FolderPath"
    }

    $allFiles = Get-ChildItem -Path $FolderPath -File -Filter '*.ps1' |
        Where-Object {
            $_.BaseName -match '^10\.' -and $_.BaseName -match 'OpenAPI'
        } |
        Sort-Object Name

    if (-not $allFiles) {
        throw "No OpenAPI 10.x samples found in: $FolderPath"
    }

    if (-not $Names -or $Names.Count -eq 0) {
        return $allFiles
    }

    $nameSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($name in $Names) {
        if ([string]::IsNullOrWhiteSpace($name)) {
            continue
        }

        $normalized = $name.Trim()
        if ($normalized.EndsWith('.ps1', [System.StringComparison]::OrdinalIgnoreCase)) {
            $normalized = [System.IO.Path]::GetFileNameWithoutExtension($normalized)
        }

        [void]$nameSet.Add($normalized)
    }

    $selected = $allFiles | Where-Object { $nameSet.Contains($_.BaseName) }

    $missing = $nameSet | Where-Object { $_ -notin $selected.BaseName }
    if ($missing) {
        throw "Requested sample(s) not found: $($missing -join ', ')"
    }

    return $selected
}

function Get-AvailableTcpPort {
    <#
    .SYNOPSIS
        Gets a currently available loopback TCP port.
    #>
    [CmdletBinding()]
    param()

    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ($listener.LocalEndpoint).Port
    } finally {
        $listener.Stop()
        $listener.Dispose()
    }
}

<#
.SYNOPSIS
    Downloads and writes OpenAPI JSON documents for a running sample.

.PARAMETER Instance
    Running sample instance object returned by Start-ExampleScript.

.PARAMETER SampleBaseName
    Base sample name used for output file naming.

.PARAMETER TargetVersions
    OpenAPI versions to fetch.

.PARAMETER TargetOutputRoot
    Absolute output root folder containing v<version> subfolders.
#>
function Export-SampleOpenApiJson {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Instance,
        [Parameter(Mandatory)]
        [string]$SampleBaseName,
        [Parameter(Mandatory)]
        [string[]]$TargetVersions,
        [Parameter(Mandatory)]
        [string]$TargetOutputRoot
    )

    foreach ($version in $TargetVersions) {
        $outputDir = Join-Path $TargetOutputRoot ("v$version")
        if (-not (Test-Path -LiteralPath $outputDir)) {
            New-Item -Path $outputDir -ItemType Directory -Force | Out-Null
        }

        $openApiUrl = "$($Instance.Url)/openapi/v$version/openapi.json"
        $response = $null
        $attempt = 0
        do {
            $attempt++
            $response = Invoke-WebRequest -Uri $openApiUrl -SkipCertificateCheck -SkipHttpErrorCheck
            if ($response.StatusCode -eq 200) {
                break
            }

            Start-Sleep -Milliseconds 250
        } while ($attempt -lt 8)

        if ($response.StatusCode -ne 200) {
            throw "Sample '$SampleBaseName' returned HTTP $($response.StatusCode) for $openApiUrl"
        }

        $jsonContent = $response.Content | ConvertFrom-Json -Depth 100 | ConvertTo-Json -Depth 100
        $outputPath = Join-Path $outputDir ("$SampleBaseName.json")
        Set-Content -Path $outputPath -Value $jsonContent -Encoding utf8
        Write-Host "Saved $outputPath"
    }
}

$repoRoot = Get-RepositoryRoot
$resolvedExamplesPath = Join-Path $repoRoot $ExamplesPath
$resolvedOutputRoot = Join-Path $repoRoot $OutputRoot

$helperPath = Join-Path $repoRoot 'tests/PowerShell.Tests/Kestrun.Tests/PesterHelpers.ps1'
if (-not (Test-Path -LiteralPath $helperPath)) {
    throw "Pester helper file not found: $helperPath"
}

. $helperPath

$normalizedVersions = @(@(
        foreach ($version in $Versions) {
            if ([string]::IsNullOrWhiteSpace($version)) {
                continue
            }

            $normalized = $version.Trim()
            if ($normalized -notmatch '^[0-9]+\.[0-9]+$') {
                throw "Invalid OpenAPI version '$version'. Use values like 3.0, 3.1, 3.2."
            }

            $normalized
        }
    ) | Sort-Object -Unique)

if (-not $normalizedVersions -or $normalizedVersions.Count -eq 0) {
    throw 'No valid OpenAPI versions were provided.'
}

$sampleFiles = @(Get-OpenApiSampleFiles -FolderPath $resolvedExamplesPath -Names $SampleNames)
Write-Host "Found $($sampleFiles.Count) OpenAPI sample(s) to process."

$failures = [System.Collections.Generic.List[string]]::new()

foreach ($sample in $sampleFiles) {
    $instance = $null
    try {
        Write-Host "Running sample $($sample.Name)..."
        $port = Get-AvailableTcpPort
        $instance = Start-ExampleScript -Name $sample.Name -Port $port
        Export-SampleOpenApiJson -Instance $instance -SampleBaseName $sample.BaseName -TargetVersions $normalizedVersions -TargetOutputRoot $resolvedOutputRoot
    } catch {
        $message = "Failed for $($sample.Name): $($_.Exception.Message)"
        $failures.Add($message) | Out-Null
        Write-Warning $message

        if (-not $ContinueOnError) {
            throw
        }
    } finally {
        if ($null -ne $instance) {
            Stop-ExampleScript -Instance $instance
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host 'Completed with failures:'
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }

    if (-not $ContinueOnError) {
        throw 'One or more OpenAPI samples failed.'
    }
}

Write-Host 'OpenAPI extraction completed.'
