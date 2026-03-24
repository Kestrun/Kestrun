<#
.SYNOPSIS
Runs Pester with optional re-runs of failed tests.

.DESCRIPTION
- Executes Pester (in-process or out-of-process).
- Collects failures by precise Path+Line.
- Re-runs only the failed tests up to -MaxReruns.
- Writes result artifacts (TRX by default; optional NUnit).

.EXAMPLE
.\Utility\Test-Pester.ps1 -ReRunFailed -MaxReruns 2  `
    -Verbosity Detailed -ResultsDir ./artifacts/testresults
#>

[CmdletBinding()]
param(
    [switch] $ReRunFailed,
    [int]    $MaxReruns = 1,
    [ValidateSet('None', 'Normal', 'Detailed', 'Diagnostic', 'Quiet')]
    [string] $Verbosity = 'Normal',
    [string] $ResultsDir = (Join-Path -Path (Get-Location) -ChildPath 'TestResults'),
    [string] $TestPath = (Join-Path -Path (Get-Location) -ChildPath 'tests' -AdditionalChildPath 'PowerShell.Tests', 'Kestrun.Tests'),
    [switch] $EmitNUnit,
    [int] $MaxFailedAllowed = 10,
    [ValidateRange(1, 360)]
    [int] $PerFileTimeoutMinutes = 10
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
            [Parameter(Mandatory = $true)] [string] $ResultOutputPath,
            [switch] $EmitNUnit
        )

        # If ResultsDir is a module-level or script-level variable, we assume it's in scope.
        # Otherwise, you may want to pass it as a parameter.
        if (-not (Test-Path $ResultsDir)) {
            New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null
        }

        $cfg = [ordered]@{
            RunPath = @($TestPath)
            Verbosity = $Verbosity
            TestResultEnabled = $true
            TestResultOutputPath = $ResultOutputPath
            TestResultOutputFormat = $null
            ExcludeTag = @()
            FullName = @()
        }

        # Exclude tags based on OS
        $excludeTag = @()
        if ($IsLinux) { $excludeTag += 'Exclude_Linux' }
        if ($IsMacOS) { $excludeTag += 'Exclude_MacOs' }
        if ($IsWindows) { $excludeTag += 'Exclude_Windows' }
        $cfg.ExcludeTag = $excludeTag

        # Optionally produce NUnit XML in addition to TRX
        if ($EmitNUnit) {
            $cfg.TestResultOutputFormat = 'NUnitXml'
        }

        return [pscustomobject]$cfg
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
        $result = @{}
        if ($PesterRun.PSObject.Properties['FailedTests']) {
            foreach ($test in @($PesterRun.FailedTests)) {
                $result[$test.File] = @{
                    File = $test.File
                    Line = $test.Line
                    FullName = $test.FullName
                    ExpandedPath = $test.ExpandedPath
                    Name = $test.Name
                    Result = $test.Result
                }
            }
            return $result.Values
        }

        $PesterRun.Tests |
            Where-Object { $_.Result -eq 'Failed' } |
            ForEach-Object {
                # Prefer ScriptBlock.File; fall back to Block.BlockContainer
                $file = $null
                try { $file = $_.ScriptBlock.File } catch {
                    Write-Warning "Failed to get ScriptBlock.File for test '$($_.Name)': $_"

                    if ( $_.Block -and $_.Block.BlockContainer) {
                        $file = $_.Block.BlockContainer.Item.ResolvedTarget
                    }
                }
                $result[$file] = @{
                    File = $file
                    Line = $_.StartLine
                    FullName = $_.FullName
                    ExpandedPath = $_.ExpandedPath
                    Name = $_.Name
                    Result = $_.Result
                }
            }
        return $result.Values
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
            [Parameter(Mandatory)] $BaseConfig,
            [Parameter(Mandatory = $true)] [string] $ResultOutputPath
        )
        $cfg = [ordered]@{
            RunPath = @($Failed.File | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
            Verbosity = $BaseConfig.Verbosity
            TestResultEnabled = $true
            TestResultOutputPath = $ResultOutputPath
            TestResultOutputFormat = $null
            ExcludeTag = @($BaseConfig.ExcludeTag)
            FullName = @()
        }

        # 🎯 Target ONLY the failing tests by their FullName
        $cfg.FullName = @(
            $Failed |
                Where-Object { $_.FullName } |
                Select-Object -ExpandProperty FullName -Unique
        )
        if (-not $cfg.RunPath -or $cfg.RunPath.Count -eq 0) {
            $cfg.RunPath = @($BaseConfig.RunPath)
        }
        return [pscustomobject]$cfg
    }

    <#
    .SYNOPSIS
        Invokes Pester with the provided configuration, either in-process or out-of-process.
    .PARAMETER Config
        The Pester configuration to use. (Mandatory)
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
            [Parameter(Mandatory = $true)] $Config,
            [Parameter(Mandatory = $true)] [int] $TimeoutSeconds
        )

        $jobConfig = $Config
        $jobWorkingDirectory = (Get-Location).Path
        $job = Start-Job -ScriptBlock {
            $innerConfig = $using:jobConfig
            $workingDirectory = $using:jobWorkingDirectory

            Set-Location -Path $workingDirectory
            Import-Module Pester -Force

            $pesterCfg = [PesterConfiguration]::Default
            $pesterCfg.Run.Path = @($innerConfig.RunPath)
            $pesterCfg.Output.Verbosity = $innerConfig.Verbosity
            $pesterCfg.TestResult.Enabled = [bool]$innerConfig.TestResultEnabled
            $pesterCfg.TestResult.OutputPath = $innerConfig.TestResultOutputPath
            if (-not [string]::IsNullOrWhiteSpace($innerConfig.TestResultOutputFormat)) {
                $pesterCfg.TestResult.OutputFormat = $innerConfig.TestResultOutputFormat
            }
            $pesterCfg.Run.Exit = $false
            $pesterCfg.Run.PassThru = $true
            $pesterCfg.Filter.ExcludeTag = @($innerConfig.ExcludeTag)
            if ($innerConfig.FullName -and $innerConfig.FullName.Count -gt 0) {
                $pesterCfg.Filter.FullName = @($innerConfig.FullName)
            }

            try {
                $run = Invoke-Pester -Configuration $pesterCfg
                $failedTests = @(
                    $run.Tests |
                        Where-Object { $_.Result -eq 'Failed' } |
                        ForEach-Object {
                            $file = $null
                            try { $file = $_.ScriptBlock.File } catch {
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
                )
                return [pscustomobject]@{
                    FailedCount = [int]$run.FailedCount
                    PassedCount = [int]$run.PassedCount
                    SkippedCount = [int]$run.SkippedCount
                    TotalCount = [int]$run.TotalCount
                    FailedTests = $failedTests
                    ExitCode = if ($run.FailedCount -gt 0) { 1 } else { 0 }
                    TimedOut = $false
                }
            } catch {
                return [pscustomobject]@{
                    FailedCount = 1
                    PassedCount = 0
                    SkippedCount = 0
                    TotalCount = 1
                    FailedTests = @()
                    ExitCode = 1
                    TimedOut = $false
                    ErrorMessage = $_.Exception.Message
                }
            }
        }

        $completed = Wait-Job -Job $job -Timeout $TimeoutSeconds
        if (-not $completed) {
            Stop-Job -Job $job -ErrorAction SilentlyContinue | Out-Null
            Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
            $target = @($Config.RunPath | Select-Object -First 1) -join ''
            return @{
                Run = [pscustomobject]@{
                    FailedCount = 1
                    PassedCount = 0
                    SkippedCount = 0
                    TotalCount = 1
                    FailedTests = @(
                        [pscustomobject]@{
                            File = $target
                            Line = 0
                            FullName = "__TIMEOUT__:$target"
                            ExpandedPath = $target
                            Name = "Timed out after $TimeoutSeconds seconds"
                            Result = 'Failed'
                        }
                    )
                    TimedOut = $true
                }
                ExitCode = 1
                TimedOut = $true
            }
        }

        $jobOutput = Receive-Job -Job $job -ErrorAction SilentlyContinue
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
        $run = $jobOutput | Select-Object -Last 1
        if (-not $run) {
            $run = [pscustomobject]@{
                FailedCount = 1
                PassedCount = 0
                SkippedCount = 0
                TotalCount = 1
                FailedTests = @()
                TimedOut = $false
                ErrorMessage = 'No result object returned from Pester worker job.'
            }
            return @{ Run = $run; ExitCode = 1; TimedOut = $false }
        }
        return @{ Run = $run; ExitCode = [int]$run.ExitCode; TimedOut = [bool]$run.TimedOut }
    }

    <#
    .SYNOPSIS
        Resolves a test path to one or more concrete Pester test files.
    .DESCRIPTION
        Accepts either a single test file path or a directory, and returns
        absolute paths to discovered `*.Tests.ps1` files in deterministic order.
    .PARAMETER Path
        A file or directory path containing Pester tests.
    .OUTPUTS
        String array of absolute test file paths.
    #>
    function Resolve-TestFile {
        [OutputType([string[]])]
        param([Parameter(Mandatory = $true)][string]$Path)

        if (-not (Test-Path -Path $Path)) {
            throw "TestPath not found: $Path"
        }

        if (Test-Path -Path $Path -PathType Leaf) {
            return @((Resolve-Path -Path $Path).Path)
        }

        $files = Get-ChildItem -Path $Path -File -Recurse -Filter '*.Tests.ps1' |
            Sort-Object -Property FullName |
            Select-Object -ExpandProperty FullName

        if (-not $files -or $files.Count -eq 0) {
            throw "No Pester test files (*.Tests.ps1) found under: $Path"
        }

        return @($files)
    }

    <#
    .SYNOPSIS
        Builds a safe result file name for a specific test file.
    .DESCRIPTION
        Produces stable per-file artifact names by sanitizing the test file
        base name and optionally prefixing with an index.
    .PARAMETER Prefix
        Prefix for the generated file name.
    .PARAMETER TestFilePath
        The source test file path.
    .PARAMETER Extension
        Output extension without leading dot.
    .PARAMETER Index
        Optional numeric index to prepend.
    .OUTPUTS
        String file name.
    #>
    function Get-ResultFileName {
        [OutputType([string])]
        param(
            [Parameter(Mandatory = $true)][string]$Prefix,
            [Parameter(Mandatory = $true)][string]$TestFilePath,
            [Parameter(Mandatory = $true)][string]$Extension,
            [int]$Index = 0
        )
        $safeBase = [IO.Path]::GetFileNameWithoutExtension($TestFilePath) -replace '[^a-zA-Z0-9\.\-_]', '_'
        if ($Index -gt 0) {
            return "$Prefix.$Index.$safeBase.$Extension"
        }
        return "$Prefix.$safeBase.$Extension"
    }
}

process {
    $testFiles = Resolve-TestFile -Path $TestPath
    $timeoutSeconds = $PerFileTimeoutMinutes * 60

    Write-Host "📁 Test results directory: $ResultsDir" -ForegroundColor Cyan
    Write-Host '📦 GitHub Actions artifact path should include: **/TestResults/**' -ForegroundColor DarkYellow
    Write-Host "🧪 Running Pester tests in '$TestPath'" -ForegroundColor Cyan
    Write-Host "⏱️ Per-file timeout: $PerFileTimeoutMinutes minute(s)" -ForegroundColor Cyan
    Write-Host "📚 Discovered $($testFiles.Count) test file(s)" -ForegroundColor Cyan

    $finalExit = 0
    $fileIndex = 0

    foreach ($testFile in $testFiles) {
        $fileIndex++
        $resultFileName = if ($EmitNUnit) {
            Get-ResultFileName -Prefix 'Pester' -TestFilePath $testFile -Extension 'nunit.xml' -Index $fileIndex
        } else {
            Get-ResultFileName -Prefix 'Pester' -TestFilePath $testFile -Extension 'trx' -Index $fileIndex
        }
        $resultPath = Join-Path -Path $ResultsDir -ChildPath $resultFileName
        $baseCfg = New-BasePesterConfig -TestPath $testFile -Verbosity $Verbosity -ResultOutputPath $resultPath -EmitNUnit:$EmitNUnit

        Write-Host ('▶️ [{0}/{1}] Running {2}' -f $fileIndex, $testFiles.Count, $testFile) -ForegroundColor Cyan
        Write-Host "📄 Result file: $resultPath" -ForegroundColor DarkCyan

        $initial = Invoke-PesterWithConfig -Config $baseCfg -TimeoutSeconds $timeoutSeconds
        if ($initial.TimedOut) {
            Write-Host ("❌ Timeout: '{0}' exceeded {1} minute(s)." -f $testFile, $PerFileTimeoutMinutes) -ForegroundColor Red
            $finalExit = 1
            continue
        }

        $fileExit = $initial.ExitCode
        if ($initial.Run.FailedCount -gt 0 -and $ReRunFailed) {
            $failed = Get-FailedSelector -PesterRun $initial.Run
            if ($failed.Count -le $MaxFailedAllowed) {
                Write-Host ('❌ Initial run had {0} failing test(s); re-running up to {1} time(s)...' -f $failed.Count, $MaxReruns) -ForegroundColor Yellow
            } else {
                Write-Host ('❌ Initial run had {0} failing test(s), exceeds MaxFailedAllowed ({1}); skipping re-runs.' -f $initial.Run.FailedCount, $MaxFailedAllowed) -ForegroundColor Red
                $finalExit = 1
                continue
            }

            $attempt = 0
            while ($attempt -lt $MaxReruns -and $failed.Count -gt 0) {
                $attempt++
                Write-Host ('🔁 Re-run attempt {0} for {1} failing test(s)...' -f $attempt, $failed.Count)

                $rerunResultPath = Join-Path $ResultsDir ('Pester-rerun-{0}-{1:yyyyMMdd-HHmmss-fff}.trx' -f $fileIndex, (Get-Date))
                $rerunCfg = New-RerunConfig -Failed $failed -BaseConfig $baseCfg -ResultOutputPath $rerunResultPath
                Write-Host "📄 Re-run result file: $rerunResultPath" -ForegroundColor DarkCyan

                $rerun = Invoke-PesterWithConfig -Config $rerunCfg -TimeoutSeconds $timeoutSeconds
                if ($rerun.TimedOut) {
                    Write-Host ("❌ Re-run timeout: '{0}' exceeded {1} minute(s)." -f $testFile, $PerFileTimeoutMinutes) -ForegroundColor Red
                    $fileExit = 1
                    break
                }

                if ($rerun.Run.FailedCount -gt 0) {
                    $failed = Get-FailedSelector -PesterRun $rerun.Run
                    $fileExit = 1
                } else {
                    $failed = @()
                    $fileExit = 0
                }
            }
        } elseif ($initial.Run.FailedCount -gt 0) {
            $fileExit = 1
        }

        if ($fileExit -ne 0) {
            $finalExit = 1
        }
    }

    if ($finalExit -ne 0) {
        Write-Host '❌ Some tests failed (after re-runs, if enabled).'
        return $finalExit
    } else {
        Write-Host '✅ All tests passed'
        return 0
    }
}
