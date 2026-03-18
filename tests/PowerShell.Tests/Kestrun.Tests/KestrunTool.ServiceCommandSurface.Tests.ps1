param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')

    $script:root = Get-ProjectRootDirectory
    $script:kestrunLauncher = Join-Path $script:root 'src/PowerShell/Kestrun/kestrun.ps1'
    $script:kestrunToolProject = Join-Path $script:root 'src/CSharp/Kestrun.Tool/Kestrun.Tool.csproj'

    if ((-not (Test-Path -Path $script:kestrunLauncher -PathType Leaf)) -and (-not (Test-Path -Path $script:kestrunToolProject -PathType Leaf))) {
        throw "Neither kestrun launcher nor Kestrun.Tool project was found. Checked: $script:kestrunLauncher ; $script:kestrunToolProject"
    }

    $serviceSuffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
    $script:serviceNameToCleanup = "kestrun-test-$PID-$serviceSuffix"
    $script:isWindowsAdmin = $false



    $script:InvokeKestrunCommand = {
        param(
            [Parameter(Mandatory)]
            [string[]]$Arguments
        )
        # Invoke the Kestrun.Tool with the provided arguments and capture the output and exit code
        $output = & dotnet run --project $script:kestrunToolProject -- @Arguments 2>&1 | Out-String

        [pscustomobject]@{
            ExitCode = $LASTEXITCODE
            Output = $output
        }
    }
}

AfterAll {
    if (-not $IsWindows) {
        return
    }

    if (-not $script:isWindowsAdmin) {
        return
    }

    $queryOutput = & sc.exe query $script:serviceNameToCleanup 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0 -and $queryOutput -match '1060') {
        return
    }

    & sc.exe stop $script:serviceNameToCleanup | Out-Null
    & sc.exe delete $script:serviceNameToCleanup | Out-Null
}

Describe 'KestrunTool service command surface' {
    It 'shows service install/remove/start/stop/query usage in help' {
        $result = & $script:InvokeKestrunCommand -Arguments @('service', 'help')

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'service install --name <service-name>'
        $result.Output | Should -Match 'service remove --name <service-name>'
        $result.Output | Should -Match 'service start --name <service-name>'
        $result.Output | Should -Match 'service stop --name <service-name>'
        $result.Output | Should -Match 'service query --name <service-name>'
        $result.Output | Should -Match 'service-log-path <path-to-log-file>'
        $result.Output | Should -Match 'service-user <account>'
        $result.Output | Should -Match 'service-password <secret>'
        $result.Output | Should -Match 'content-root <folder(?:-or-archive)?>'
        $result.Output | Should -Match 'deployment-root <folder>'
        $result.Output | Should -Match 'shows progress bars during bundle staging'
    }

    It 'fails service install when --service-password is provided without --service-user' {
        $result = & $script:InvokeKestrunCommand -Arguments @('service', 'install', '--name', 'test', '--service-password', 'secret')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match '--service-password requires --service-user\.'
    }

    It 'fails service install without --name' {
        $result = & $script:InvokeKestrunCommand -Arguments @('service', 'install')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'Service name is required\. Use --name <value>\.'
    }

    It 'fails service remove without --name' {
        $result = & $script:InvokeKestrunCommand -Arguments @('service', 'remove')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'Service name is required\. Use --name <value>\.'
    }

    It 'fails service start without --name' {
        $result = & $script:InvokeKestrunCommand -Arguments @('service', 'start')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'Service name is required\. Use --name <value>\.'
    }

    It 'fails service stop without --name' {
        $result = & $script:InvokeKestrunCommand -Arguments @('service', 'stop')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'Service name is required\. Use --name <value>\.'
    }

    It 'fails service query without --name' {
        $result = & $script:InvokeKestrunCommand -Arguments @('service', 'query')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'Service name is required\. Use --name <value>\.'
    }

    It 'fails service install when --service-log-path value is missing' {
        $result = & $script:InvokeKestrunCommand -Arguments @('service', 'install', '--name', 'test', '--service-log-path')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'Missing value for --service-log-path\.'
    }

    It 'fails service install when --deployment-root value is missing' {
        $result = & $script:InvokeKestrunCommand -Arguments @('service', 'install', '--name', 'test', '--deployment-root')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'Missing value for --deployment-root\.'
    }

    It 'installs without auto-starting the service' -Skip:(-not ($IsWindows -and $script:isWindowsAdmin)) {
        $scriptPath = Join-Path $script:root 'docs/_includes/examples/pwsh/10.2-OpenAPI-Component-Schema.ps1'
        $result = & $script:InvokeKestrunCommand -Arguments @('service', 'install', '--name', $script:serviceNameToCleanup, $scriptPath)

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match "Installed Windows service '$([regex]::Escape($script:serviceNameToCleanup))' \(not started\)\."

        $queryOutput = & sc.exe query $script:serviceNameToCleanup 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $queryOutput | Should -Match 'STATE\s+:\s+\d+\s+STOPPED'

        $qcOutput = & sc.exe qc $script:serviceNameToCleanup 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 0

        $expectedDeploymentRoot = Join-Path $env:ProgramData (Join-Path $script:serviceDeploymentProductFolderName $script:serviceDeploymentServicesFolderName)
        $expectedBundleRuntimePath = Join-Path $expectedDeploymentRoot "$($script:serviceNameToCleanup)\$($script:serviceBundleRuntimeDirectoryName)\$($script:windowsServiceRuntimeBinaryName)"
        $qcOutput | Should -Match ([regex]::Escape($expectedBundleRuntimePath))
        $qcOutput | Should -Not -Match 'dotnet\.exe'
    }

    It 'writes install and remove operations to the service log path' -Skip:(-not ($IsWindows -and $script:isWindowsAdmin)) {

        $serviceName = "kestrun-test-log-$PID-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        $scriptPath = Join-Path $script:root 'docs/_includes/examples/pwsh/10.2-OpenAPI-Component-Schema.ps1'
        $logFileName = "$serviceName.log"
        $logPath = Join-Path $env:ProgramData (Join-Path 'Kestrun\custom-logs' $logFileName)

        if (Test-Path -LiteralPath $logPath) {
            Remove-Item -LiteralPath $logPath -Force
        }

        $installResult = & $script:InvokeKestrunCommand -Arguments @(
            'service', 'install',
            '--name', $serviceName,
            '--service-log-path', $logPath,
            $scriptPath)

        $installResult.ExitCode | Should -Be 0

        $removeResult = & $script:InvokeKestrunCommand -Arguments @('service', 'remove', '--name', $serviceName)
        $removeResult.ExitCode | Should -Be 0

        Test-Path -LiteralPath $logPath | Should -BeTrue
        $content = Get-Content -LiteralPath $logPath -Raw
        $content | Should -Match "Service '$serviceName' install operation completed\."
        $content | Should -Match "Service '$serviceName' remove operation completed\."
    }
}
