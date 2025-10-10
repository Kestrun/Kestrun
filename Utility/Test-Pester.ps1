<#
.SYNOPSIS
Runs Pester with optional re-runs of failed tests.

.DESCRIPTION
- Executes Pester (in-process or out-of-process).
- Collects failures by precise Path+Line.
- Re-runs only the failed tests up to -MaxReruns.
- Writes result artifacts (TRX by default; optional NUnit).

.EXAMPLE
.\Utility\Test-Pester.ps1 -ReRunFailed -MaxReruns 2 -RunPesterInProcess `
    -Verbosity Detailed -ResultsDir ./artifacts/testresults
#>

[CmdletBinding()]
param(
    [switch] $ReRunFailed,
    [int]    $MaxReruns = 1,
    [switch] $RunPesterInProcess,
    [ValidateSet('None', 'Normal', 'Detailed', 'Diagnostic', 'Quiet')]
    [string] $Verbosity = 'Normal',
    [string] $ResultsDir = "$((Get-Location).Path)/artifacts/testresults",
    [string] $TestPath = "$((Get-Location).Path)/tests/PowerShell.Tests",
    [switch] $EmitNUnit,
    [int] $MaxFailedAllowed = 10
)

begin {
    # Ensure Pester is available
    if (-not (Get-Module -ListAvailable -Name Pester)) {
        throw 'Pester module not found. Please Install-Module Pester -Scope CurrentUser'
    }
    Import-Module Pester -Force

    if (-not (Test-Path $ResultsDir)) { $null = New-Item -ItemType Directory -Force -Path $ResultsDir }

    <#

    .SYNOPSIS
        Creates a base Pester configuration for running tests.
    .DESCRIPTION
        Constructs a Pester configuration with specified test paths, verbosity, and output settings.
    .PARAMETER TestPath
        The path(s) to the tests to run.
    .PARAMETER Verbosity
        The verbosity level for Pester output. Valid values are 'None', 'Normal', 'Detailed', 'Diagnostic', and 'Quiet'.
    .PARAMETER EmitNUnit
        If specified, configures Pester to emit test results in NUnit XML format in addition to TRX.
    .OUTPUTS
        A PesterConfiguration object configured with the specified settings.
    .NOTES
        This function creates a Pester configuration that sets up the test paths, output verbosity,
        and result output format. It also includes OS-based tag exclusions to allow for platform-specific test
        filtering.
    #>
    function New-BasePesterConfig {
        [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
        param(
            [Parameter(Mandatory = $true)] [string] $TestPath,
            [Parameter(Mandatory = $true)] [string] $Verbosity,
            [switch] $EmitNUnit
        )

        # If ResultsDir is a module-level or script-level variable, we assume it's in scope.
        # Otherwise, you may want to pass it as a parameter.
        if (-not (Test-Path $ResultsDir)) {
            New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null
        }

        $cfg = [PesterConfiguration]::Default

        # Path(s) to test files / directory
        $cfg.Run.Path = @($TestPath)

        # Verbosity for output
        $cfg.Output.Verbosity = $Verbosity

        # Enable test result output
        $cfg.TestResult.Enabled = $true
        $cfg.TestResult.OutputPath = Join-Path $ResultsDir 'Pester.trx'

        # Do not auto-exit; we’ll manage exit logic ourselves (for reruns etc.)
        $cfg.Run.Exit = $false

        # Make sure Invoke-Pester returns a result object
        $cfg.Run.PassThru = $true

        # Exclude tags based on OS
        $excludeTag = @()
        if ($IsLinux) { $excludeTag += 'Exclude_Linux' }
        if ($IsMacOS) { $excludeTag += 'Exclude_MacOs' }
        if ($IsWindows) { $excludeTag += 'Exclude_Windows' }
        $cfg.Filter.ExcludeTag = $excludeTag

        # Optionally produce NUnit XML in addition to TRX
        if ($EmitNUnit) {
            $cfg.TestResult.OutputFormat = 'NUnitXml'
            $cfg.TestResult.OutputPath = Join-Path $ResultsDir 'Pester.nunit.xml'
        }

        return $cfg
    }

    <#
    .SYNOPSIS
        Extracts failed test selectors from a Pester run result.
    .DESCRIPTION
        Processes the Pester run result to identify tests that failed,
        returning their names, file paths, and line numbers for targeted re-runs.
    .PARAMETER PesterRun
        The Pester run result object from which to extract failed tests.
    .OUTPUTS
        A collection of objects with Name, Path, and Line properties for each failed test.
    .NOTES
        This function processes the Pester run result to identify tests that failed,
        returning their names, file paths, and line numbers for targeted re-runs.
    #>
    function Get-FailedSelector {
        param([Parameter(Mandatory)] $PesterRun)

        $PesterRun.Tests |
            Where-Object { $_.Result -eq 'Failed' } | # -or $_.Outcome -eq 'Failed' } |
            ForEach-Object {
                # Prefer ScriptBlock.File; fall back to Block.BlockContainer
                $file = $null
                try { $file = $_.ScriptBlock.File } catch {
                    Write-Warning "Failed to get ScriptBlock.File for test '$($_.Name)': $_"

                    if ( $_.Block -and $_.Block.BlockContainer) {
                        $file = $_.Block.BlockContainer.Item.ResolvedTarget
                    }
                }
                [pscustomobject]@{
                    File = $file
                    Line = $_.StartLine
                    FullName = $_.FullName
                    ExpandedPath = $_.ExpandedPath
                    Name = $_.Name
                    Result = $_.Result
                }
            }
    }

    <#
    .SYNOPSIS
        Creates a Pester configuration to re-run only the specified failed tests.
    .DESCRIPTION
        Constructs a new Pester configuration that filters tests to only those that previously failed,
        allowing for targeted re-runs. It preserves other settings from the provided base configuration.
    .PARAMETER Failed
        A collection of objects with Path and Line properties identifying failed tests.
    .PARAMETER BaseConfig
        The base Pester configuration to copy settings from.
    .OUTPUTS
        A PesterConfiguration object set to run only the specified tests.
    .NOTES
        This function constructs a new Pester configuration that filters tests to only those that previously failed,
        allowing for targeted re-runs. It preserves other settings from the provided base configuration.
    #>
    function New-RerunConfig {
        [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
        param(
            [Parameter(Mandatory)]
            [System.Collections.IEnumerable] $Failed,
            [Parameter(Mandatory)] $BaseConfig
        )
        $cfg = [PesterConfiguration]::Default
        $cfg.Run.Path = $BaseConfig.Run.Path
        $cfg.Output.Verbosity = $BaseConfig.Output.Verbosity
        $cfg.TestResult.Enabled = $true
        $cfg.TestResult.OutputPath = Join-Path $ResultsDir ('Pester-rerun-{0}.trx' -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
        $cfg.Run.Exit = $false
        $cfg.Run.PassThru = $true
        $cfg.Filter.ExcludeTag = $BaseConfig.Filter.ExcludeTag

        # 🎯 Target ONLY the failing tests by their FullName
        $cfg.Filter.FullName = @(
            $Failed |
                Where-Object { $_.FullName } |
                Select-Object -ExpandProperty FullName -Unique
        )
        return $cfg
    }

    <#
    .SYNOPSIS
        Invokes Pester with the provided configuration, either in-process or out-of-process.
    .PARAMETER Config
        The Pester configuration to use.
    .PARAMETER OutOfProcess
        If specified, runs Pester in a separate PowerShell process.
    .OUTPUTS
        A hashtable with keys:
        - Run: The Pester run result object.
        - ExitCode: 0 if all tests passed, 1 if any test failed.
    .NOTES
        This function abstracts the invocation of Pester to support both in-process and out-of-process execution.
        When running out-of-process, it creates a temporary script to execute Pester and captures the results in JSON format.
        The temporary files are cleaned up after execution.
    #>
    function Invoke-PesterWithConfig {
        param(
            [Parameter(Mandatory)] $Config,
            [switch] $OutOfProcess
        )

        if (-not $OutOfProcess) {
            $run = Invoke-Pester -Configuration $Config
            $code = if ($run.FailedCount -gt 0) { 1 } else { 0 }
            return @{ Run = $run; ExitCode = $code }
        }

        # Out-of-process runner writes a minimal JSON payload we read back
        $jsonCfg = $Config | ConvertTo-Json -Depth 10
        $child = Join-Path ([System.IO.Path]::GetTempPath()) ('run-pester-{0}.ps1' -f ([guid]::NewGuid()))
        $runFile = Join-Path $ResultsDir ('run-{0}.json' -f ([guid]::NewGuid()))

        @'
param([string]$ConfigJson,[string]$RunOut)
Import-Module Pester -Force
$cfg = New-PesterConfiguration -Hashtable (($ConfigJson | ConvertFrom-Json -AsHashtable))
$run = Invoke-Pester -Configuration $cfg -PassThru

$payload = [pscustomobject]@{
    FailedCount = $run.FailedCount
    TotalCount  = $run.TotalCount
    Tests       = $run.Tests | ForEach-Object {
        [pscustomobject]@{
            FullName = $_.FullName
            Name     = $_.Name
            Path     = $_.Path
            Line     = $_.Line
            Result   = $_.Result
        }
    }
}
$payload | ConvertTo-Json -Depth 6 | Set-Content -Path $RunOut -Encoding UTF8
'@ | Set-Content -Path $child -Encoding UTF8

        try {
            pwsh -NoProfile -File $child -ConfigJson $jsonCfg -RunOut $runFile | Out-Null
            $payload = Get-Content $runFile -Raw | ConvertFrom-Json
            $run = [pscustomobject]@{
                FailedCount = [int]$payload.FailedCount
                TotalCount = [int]$payload.TotalCount
                Tests = $payload.Tests
            }
            $code = if ($run.FailedCount -gt 0) { 1 } else { 0 }
            return @{ Run = $run; ExitCode = $code }
        } finally {
            Remove-Item $child -ErrorAction SilentlyContinue
            Remove-Item $runFile -ErrorAction SilentlyContinue
        }
    }
}

process {
    $baseCfg = New-BasePesterConfig -TestPath $TestPath -Verbosity $Verbosity -EmitNUnit:$EmitNUnit
    Write-Host "🧪 Running Pester tests in '$($baseCfg.Run.Path.Value)'" -ForegroundColor Cyan

    $initial = if ($RunPesterInProcess) {
        Invoke-PesterWithConfig -Config $baseCfg
    } else {
        Invoke-PesterWithConfig -Config $baseCfg -OutOfProcess
    }

    $finalExit = $initial.ExitCode
    $attempt = 0
    if ($initial.Run.FailedCount -le $MaxFailedAllowed ) {

        if ($ReRunFailed -and $initial.Run.FailedCount -gt 0 -and $MaxReruns -gt 0) {
            $failed = Get-FailedSelector -PesterRun $initial.Run
            while ($attempt -lt $MaxReruns -and $failed.Count -gt 0) {
                $attempt++
                Write-Host ('🔁 Re-run attempt {0} for {1} failing test(s)...' -f $attempt, $failed.Count)

                $rerunCfg = New-RerunConfig -Failed $failed -BaseConfig $baseCfg
                $rerun = if ($RunPesterInProcess) {
                    Invoke-PesterWithConfig -Config $rerunCfg
                } else {
                    Invoke-PesterWithConfig -Config $rerunCfg -OutOfProcess
                }

                if ($rerun.Run.FailedCount -gt 0) {
                    $failed = Get-FailedSelector -PesterRun $rerun.Run
                    $finalExit = 1
                } else {
                    $failed = @()
                    $finalExit = 0
                }
            }
        }
    } else {
        Write-Host ('❌ Too many initial test failures ({0}) - skipping re-runs as MaxFailedAllowed is {1}.' -f $initial.Run.FailedCount, $MaxFailedAllowed) -ForegroundColor Red
        $finalExit = 1
    }

    if ($finalExit -ne 0) {
        Write-Host '❌ Some tests failed (after re-runs, if enabled).'
        exit $finalExit
    } else {
        Write-Host '✅ All tests passed'
    }
}
