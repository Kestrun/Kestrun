<#
.SYNOPSIS
Runs the Kestrun Pester suite with optional targeted re-runs.

.DESCRIPTION
- Executes Pester one test file at a time so CI artifacts show the exact last
  file that started before a hang or timeout.
- Collects failures by precise file, line, and FullName.
- Records both initial and remaining failures as JSON manifests.
- Re-runs only the failed tests up to -MaxReruns.
- Writes NUnit XML, a discovered-file manifest, a progress log, and a console
  transcript under the selected results directory.

.EXAMPLE
.\Utility\Test-Pester.ps1 -ReRunFailed -MaxReruns 2 `
    -Verbosity Detailed -ResultsDir ./artifacts/testresults
#>

[CmdletBinding()]
param(
    [switch] $ReRunFailed,
    [int] $MaxReruns = 1,
    [ValidateSet('None', 'Normal', 'Detailed', 'Diagnostic', 'Quiet')]
    [string] $Verbosity = 'Normal',
    [switch] $DebugMode,
    [string] $ResultsDir = (Join-Path -Path (Get-Location) -ChildPath 'TestResults'),
    [string] $TestPath = (Join-Path -Path (Get-Location) -ChildPath 'tests' -AdditionalChildPath 'PowerShell.Tests', 'Kestrun.Tests'),
    [string[]] $ExcludePath = @(),
    [ValidateRange(1, 256)]
    [int] $ShardCount = 1,
    [ValidateRange(1, 256)]
    [int] $ShardIndex = 1,
    [switch] $EmitNUnit,
    [int] $MaxFailedAllowed = 10,
    [switch] $PassThru
)

begin {
    if (-not (Get-Module -ListAvailable -Name Pester)) {
        throw 'Pester module not found. Please Install-Module Pester -Scope CurrentUser'
    }

    Import-Module (Join-Path -Path $PSScriptRoot -ChildPath 'Modules\Helper.psm1') -Force
    Import-Module Pester -Force

    if (-not (Test-Path -LiteralPath $ResultsDir)) {
        $null = New-Item -ItemType Directory -Force -Path $ResultsDir
    }
}

process {
    $runStamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $pesterResultsDir = Join-Path -Path $ResultsDir -ChildPath 'pester' -AdditionalChildPath $runStamp
    $progressLogPath = Join-Path -Path $pesterResultsDir -ChildPath 'Pester.progress.log'
    $consoleLogPath = Join-Path -Path $pesterResultsDir -ChildPath 'Pester.console.log'
    $discoveredFilesPath = Join-Path -Path $pesterResultsDir -ChildPath 'Pester.discovered-files.txt'
    $baseXmlPath = Join-Path -Path $pesterResultsDir -ChildPath 'Pester.initial.xml'
    $initialFailuresPath = Join-Path -Path $pesterResultsDir -ChildPath 'Pester.initial-failures.json'
    $remainingFailuresPath = Join-Path -Path $pesterResultsDir -ChildPath 'Pester.remaining-failures.json'

    if (-not (Test-Path -LiteralPath $pesterResultsDir)) {
        $null = New-Item -ItemType Directory -Force -Path $pesterResultsDir
    }

    $baseCfg = New-KestrunPesterConfig `
        -TestPath $TestPath `
        -Verbosity $Verbosity `
        -ResultsPath $baseXmlPath `
        -TestSuiteName 'Kestrun Pester'

    $testFiles = @(Get-KestrunPesterTestFile -TestPath $TestPath -ExcludePath $ExcludePath -ShardCount $ShardCount -ShardIndex $ShardIndex)
    if ($testFiles.Count -eq 0) {
        throw "No Pester test files were found under '$TestPath'."
    }

    $discoveredFileLines = for ($index = 0; $index -lt $testFiles.Count; $index++) {
        '{0:D3}/{1:D3} {2}' -f ($index + 1), $testFiles.Count, $testFiles[$index]
    }
    Set-Content -LiteralPath $discoveredFilesPath -Value $discoveredFileLines

    Write-Host "Pester results directory: $pesterResultsDir" -ForegroundColor Cyan
    Write-Host "Pester progress log: $progressLogPath" -ForegroundColor DarkCyan
    Write-Host "Pester console transcript: $consoleLogPath" -ForegroundColor DarkCyan
    Write-Host "Pester discovered-file manifest: $discoveredFilesPath" -ForegroundColor DarkCyan
    Write-Host "Pester initial failure manifest: $initialFailuresPath" -ForegroundColor DarkCyan
    Write-Host "Pester remaining failure manifest: $remainingFailuresPath" -ForegroundColor DarkCyan
    Write-Host "GitHub Actions artifact hint: include './TestResults/**' (or '**/TestResults/**') so the Pester XML, progress log, transcript, and manifest are uploaded." -ForegroundColor DarkYellow

    $transcriptStarted = $false
    try {
        Start-Transcript -LiteralPath $consoleLogPath -Force | Out-Null
        $transcriptStarted = $true
    } catch {
        Set-Content -LiteralPath $consoleLogPath -Value ('[{0}] Start-Transcript failed: {1}' -f [DateTimeOffset]::UtcNow.ToString('o'), $_.Exception.Message)
        Write-Warning "Unable to start transcript at '$consoleLogPath': $($_.Exception.Message)"
    }

    Write-KestrunTimestampedLog -Path $progressLogPath -Message "Running Pester tests in '$TestPath'" -ForegroundColor Cyan | Out-Null
    if ($ExcludePath.Count -gt 0) {
        Write-KestrunTimestampedLog -Path $progressLogPath -Message ('Excluding Pester paths: {0}' -f ($ExcludePath -join ', ')) -ForegroundColor DarkCyan | Out-Null
    }
    if ($ShardCount -gt 1) {
        Write-KestrunTimestampedLog -Path $progressLogPath -Message ('Running Pester shard {0}/{1}.' -f $ShardIndex, $ShardCount) -ForegroundColor DarkCyan | Out-Null
    }
    Write-KestrunTimestampedLog -Path $progressLogPath -Message "Discovered $($testFiles.Count) Pester test files." -ForegroundColor DarkCyan | Out-Null
    if ($EmitNUnit) {
        Write-KestrunTimestampedLog -Path $progressLogPath -Message 'EmitNUnit is retained for compatibility; Pester result output is already written as NUnit XML.' -ForegroundColor DarkYellow | Out-Null
    }

    $httpTimeoutSeconds = if ($env:KR_TEST_HTTP_TIMEOUT_SECONDS) { [int] $env:KR_TEST_HTTP_TIMEOUT_SECONDS } else { 30 }
    Write-KestrunTimestampedLog -Path $progressLogPath -Message "Default Invoke-WebRequest/Invoke-RestMethod timeout: ${httpTimeoutSeconds}s" -ForegroundColor DarkCyan | Out-Null

    $defaultTimeoutKeys = @(
        'Invoke-WebRequest:TimeoutSec',
        'Invoke-RestMethod:TimeoutSec'
    )
    $previousTimeoutValues = @{}
    foreach ($key in $defaultTimeoutKeys) {
        if ($PSDefaultParameterValues.Contains($key)) {
            $previousTimeoutValues[$key] = $PSDefaultParameterValues[$key]
        }
        $PSDefaultParameterValues[$key] = $httpTimeoutSeconds
    }

    $originalDebugPreference = $DebugPreference
    try {
        if ($DebugMode) {
            $DebugPreference = 'Continue'
            Write-KestrunTimestampedLog -Path $progressLogPath -Message 'PowerShell debug output is enabled for the Pester run.' -ForegroundColor Yellow | Out-Null
        }

        $allFailedSelectors = @()
        $initialExitCode = 0

        for ($index = 0; $index -lt $testFiles.Count; $index++) {
            $testFile = $testFiles[$index]
            $displayIndex = '{0:D3}/{1:D3}' -f ($index + 1), $testFiles.Count
            $safeBaseName = ([System.IO.Path]::GetFileNameWithoutExtension($testFile) -replace '[^A-Za-z0-9._-]', '_')
            $resultXmlPath = Join-Path -Path $pesterResultsDir -ChildPath ('Pester-{0:D3}-{1}.xml' -f ($index + 1), $safeBaseName)
            $fileCfg = New-KestrunPesterConfig `
                -TestPath $testFile `
                -Verbosity $Verbosity `
                -ResultsPath $resultXmlPath `
                -TestSuiteName ('Kestrun Pester :: {0}' -f [System.IO.Path]::GetFileName($testFile))

            Write-KestrunTimestampedLog -Path $progressLogPath -Message "Starting Pester file [$displayIndex]: $testFile" -ForegroundColor Cyan | Out-Null
            Write-KestrunTimestampedLog -Path $progressLogPath -Message "Writing NUnit XML to: $resultXmlPath" -ForegroundColor DarkCyan | Out-Null

            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            try {
                $runResult = Invoke-KestrunPesterWithConfig -Config $fileCfg
            } catch {
                $stopwatch.Stop()
                Write-KestrunTimestampedLog -Path $progressLogPath -Message "Pester invocation threw while running [$displayIndex]: $testFile :: $($_.Exception.Message)" -ForegroundColor Red | Out-Null
                throw
            }
            $stopwatch.Stop()

            if ($runResult.Run.FailedCount -gt 0) {
                $initialExitCode = 1
                $allFailedSelectors += @(Get-KestrunPesterFailedSelector -PesterRun $runResult.Run)
                Write-KestrunTimestampedLog -Path $progressLogPath -Message ('Completed Pester file [{0}] with failures in {1:c}. FailedCount={2}' `
                        -f $displayIndex, $stopwatch.Elapsed, $runResult.Run.FailedCount) -ForegroundColor Yellow | Out-Null
            } else {
                Write-KestrunTimestampedLog -Path $progressLogPath -Message ('Completed Pester file [{0}] successfully in {1:c}. TotalCount={2}' `
                        -f $displayIndex, $stopwatch.Elapsed, $runResult.Run.TotalCount) -ForegroundColor Green | Out-Null
            }
        }

        $initialFailedSelectors = @(
            $allFailedSelectors |
                Group-Object -Property { '{0}|{1}|{2}' -f $_.File, $_.Line, $_.FullName } |
                ForEach-Object { $_.Group[0] }
        )
        ConvertTo-Json -InputObject @($initialFailedSelectors) -Depth 6 |
            Set-Content -LiteralPath $initialFailuresPath -Encoding utf8NoBOM

        $finalExit = $initialExitCode
        $attempt = 0
        $remainingFailedSelectors = @($initialFailedSelectors)

        if ($remainingFailedSelectors.Count -gt 0 -and $ReRunFailed) {
            if ($remainingFailedSelectors.Count -le $MaxFailedAllowed) {
                Write-KestrunTimestampedLog -Path $progressLogPath -Message ('Initial pass had {0} failing test(s); preparing to re-run up to {1} time(s).' `
                        -f $remainingFailedSelectors.Count, $MaxReruns) -ForegroundColor Yellow | Out-Null
                while ($attempt -lt $MaxReruns -and $remainingFailedSelectors.Count -gt 0) {
                    $attempt++
                    $rerunXmlPath = Join-Path -Path $pesterResultsDir -ChildPath ('Pester-rerun-{0:00}-{1}.xml' -f $attempt, (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
                    $rerunCfg = New-KestrunPesterRerunConfig `
                        -Failed $remainingFailedSelectors `
                        -BaseConfig $baseCfg `
                        -ResultsPath $rerunXmlPath `
                        -TestSuiteName ('Kestrun Pester Rerun {0}' -f $attempt)

                    Write-KestrunTimestampedLog -Path $progressLogPath -Message ('Starting rerun attempt {0} for {1} failing test(s).' -f $attempt, $remainingFailedSelectors.Count) -ForegroundColor Yellow | Out-Null
                    Write-KestrunTimestampedLog -Path $progressLogPath -Message "Writing rerun NUnit XML to: $rerunXmlPath" -ForegroundColor DarkCyan | Out-Null

                    $rerun = Invoke-KestrunPesterWithConfig -Config $rerunCfg
                    if ($rerun.Run.FailedCount -gt 0) {
                        $remainingFailedSelectors = @(Get-KestrunPesterFailedSelector -PesterRun $rerun.Run)
                        $finalExit = 1
                        Write-KestrunTimestampedLog -Path $progressLogPath -Message ('Rerun attempt {0} still has {1} failing test(s).' -f $attempt, $remainingFailedSelectors.Count) -ForegroundColor Red | Out-Null
                    } else {
                        $remainingFailedSelectors = @()
                        $finalExit = 0
                        Write-KestrunTimestampedLog -Path $progressLogPath -Message ('Rerun attempt {0} cleared all remaining failures.' -f $attempt) -ForegroundColor Green | Out-Null
                    }
                }
            } else {
                Write-KestrunTimestampedLog -Path $progressLogPath -Message ('Initial pass had {0} failing test(s), which exceeds MaxFailedAllowed ({1}); skipping re-runs.' `
                        -f $remainingFailedSelectors.Count, $MaxFailedAllowed) -ForegroundColor Red | Out-Null
                $finalExit = 1
            }
        }

        ConvertTo-Json -InputObject @($remainingFailedSelectors) -Depth 6 |
            Set-Content -LiteralPath $remainingFailuresPath -Encoding utf8NoBOM

        if ($finalExit -ne 0) {
            Write-KestrunTimestampedLog -Path $progressLogPath -Message 'Some Pester tests failed.' -ForegroundColor Red | Out-Null
            if ($PassThru) {
                return [pscustomobject]@{
                    ExitCode = $finalExit
                    InitialExitCode = $initialExitCode
                    ResultsDirectory = $pesterResultsDir
                    ProgressLogPath = $progressLogPath
                    ConsoleLogPath = $consoleLogPath
                    DiscoveredFilesPath = $discoveredFilesPath
                    InitialFailuresPath = $initialFailuresPath
                    RemainingFailuresPath = $remainingFailuresPath
                    InitialFailedSelectors = @($initialFailedSelectors)
                    RemainingFailedSelectors = @($remainingFailedSelectors)
                }
            }

            return $finalExit
        }

        Write-KestrunTimestampedLog -Path $progressLogPath -Message 'All Pester tests passed.' -ForegroundColor Green | Out-Null
        if ($PassThru) {
            return [pscustomobject]@{
                ExitCode = 0
                InitialExitCode = $initialExitCode
                ResultsDirectory = $pesterResultsDir
                ProgressLogPath = $progressLogPath
                ConsoleLogPath = $consoleLogPath
                DiscoveredFilesPath = $discoveredFilesPath
                InitialFailuresPath = $initialFailuresPath
                RemainingFailuresPath = $remainingFailuresPath
                InitialFailedSelectors = @($initialFailedSelectors)
                RemainingFailedSelectors = @($remainingFailedSelectors)
            }
        }

        return 0
    } finally {
        foreach ($key in $defaultTimeoutKeys) {
            if ($previousTimeoutValues.ContainsKey($key)) {
                $PSDefaultParameterValues[$key] = $previousTimeoutValues[$key]
            } else {
                $null = $PSDefaultParameterValues.Remove($key)
            }
        }

        $DebugPreference = $originalDebugPreference

        if ($transcriptStarted) {
            try {
                Stop-Transcript | Out-Null
            } catch {
                Write-Warning "Failed to stop transcript cleanly: $($_.Exception.Message)"
            }
        }
    }
}
