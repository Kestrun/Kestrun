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

    $script:GetJsonPayloadFromOutput = {
        param(
            [Parameter(Mandatory)]
            [string]$Output
        )

        $match = [regex]::Match($Output, '(?s)\{.*\}')
        $match.Success | Should -BeTrue
        return $match.Value | ConvertFrom-Json
    }

    $script:GetCurrentServiceRuntimeRid = {
        $archSegment = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
            ([System.Runtime.InteropServices.Architecture]::X64) { 'x64' }
            ([System.Runtime.InteropServices.Architecture]::Arm64) { 'arm64' }
            default { throw "Unsupported architecture for runtime package tests: $([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)" }
        }

        if ($IsWindows) {
            return "win-$archSegment"
        }

        if ($IsLinux) {
            return "linux-$archSegment"
        }

        if ($IsMacOS) {
            return "osx-$archSegment"
        }

        throw 'Unsupported operating system for runtime package tests.'
    }

    $script:NewTestRuntimePackage = {
        param(
            [Parameter(Mandatory)]
            [string]$TempRoot,
            [Parameter(Mandatory)]
            [string]$PackagePath,
            [Parameter(Mandatory)]
            [string]$PackageId,
            [Parameter(Mandatory)]
            [string]$PackageVersion,
            [Parameter(Mandatory)]
            [string]$Rid
        )

        $stagingRoot = Join-Path $TempRoot 'runtime-package'
        $hostRoot = Join-Path $stagingRoot 'host'
        $moduleRoot = Join-Path $stagingRoot 'modules/Demo.Module'
        $hostFileName = if ($IsWindows) { 'kestrun-service-host.exe' } else { 'kestrun-service-host' }
        $manifest = @{
            rid = $Rid
            entryPoint = "host/$hostFileName"
            modulesPath = 'modules'
        } | ConvertTo-Json -Compress

        New-Item -ItemType Directory -Path $hostRoot -Force | Out-Null
        New-Item -ItemType Directory -Path $moduleRoot -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $hostRoot $hostFileName) -Value 'host-binary' -Encoding utf8NoBOM
        Set-Content -LiteralPath (Join-Path $moduleRoot 'Demo.Module.psd1') -Value '@{}' -Encoding utf8NoBOM
        Set-Content -LiteralPath (Join-Path $stagingRoot 'runtime-manifest.json') -Value $manifest -Encoding utf8NoBOM

        $nuspec = @"
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>$PackageId</id>
    <version>$PackageVersion</version>
    <authors>Kestrun Team</authors>
    <description>Test runtime package.</description>
  </metadata>
</package>
"@
        Set-Content -LiteralPath (Join-Path $stagingRoot "$PackageId.nuspec") -Value $nuspec -Encoding utf8NoBOM

        if (Test-Path -LiteralPath $PackagePath) {
            Remove-Item -LiteralPath $PackagePath -Force
        }

        [System.IO.Compression.ZipFile]::CreateFromDirectory($stagingRoot, $PackagePath)
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
        $result.Output | Should -Match 'service install \[--package <path-or-url-to-\.krpack>\]'
        $result.Output | Should -Match 'service remove --name <service-name>'
        $result.Output | Should -Match 'service start --name <service-name> \[--json \| --raw\]'
        $result.Output | Should -Match 'service stop --name <service-name> \[--json \| --raw\]'
        $result.Output | Should -Match 'service query --name <service-name> \[--json \| --raw\]'
        $result.Output | Should -Match 'service-log-path <path-to-log-file>'
        $result.Output | Should -Match 'service-user <account>'
        $result.Output | Should -Match 'service-password <secret>'
        $result.Output | Should -Match '--package <path-or-url>'
        $result.Output | Should -Match 'deployment-root <folder>'
        $result.Output | Should -Match '--runtime-source <path-or-url>'
        $result.Output | Should -Match '--runtime-package <path>'
        $result.Output | Should -Match '--runtime-version <version>'
        $result.Output | Should -Match '--runtime-package-id <id>'
        $result.Output | Should -Match '--runtime-cache <folder>'
        $result.Output | Should -Match '--json\s+For service start/stop/query/info'
        $result.Output | Should -Match '--raw\s+For service start/stop/query'
        $result.Output | Should -Match 'shows progress bars during bundle staging'
        $result.Output | Should -Match 'resolves a runtime package for the current RID'
    }

    It 'fails service install when --service-password is provided without --service-user' {
        $result = & $script:InvokeKestrunCommand -Arguments @('service', 'install', '--name', 'test', '--service-password', 'secret')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match '--service-password requires --service-user\.'
    }

    It 'fails service install without package or runtime acquisition options' {
        $result = & $script:InvokeKestrunCommand -Arguments @('service', 'install')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'Service install requires --package or at least one runtime acquisition option \(--runtime-version, --runtime-source, --runtime-package, or --runtime-package-id\)\.'
    }

    It 'caches runtime-only install artifacts from a local runtime feed' {
        $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-runtime-cache-{0}' -f [guid]::NewGuid().ToString('N'))

        try {
            $runtimeFeed = Join-Path $tempRoot 'runtime-feed'
            $runtimeCache = Join-Path $tempRoot 'runtime-cache'
            $rid = & $script:GetCurrentServiceRuntimeRid
            $packageId = "Kestrun.Service.$rid"
            $packageVersion = '9.9.9-pester'
            $packagePath = Join-Path $runtimeFeed "$packageId.$packageVersion.nupkg"
            $cachedPackagePath = Join-Path $runtimeCache "packages/$packageId/$packageVersion/$packageId.$packageVersion.nupkg"
            $expandedPackageRoot = Join-Path $runtimeCache "expanded/$packageId"
            $hostFileName = if ($IsWindows) { 'kestrun-service-host.exe' } else { 'kestrun-service-host' }

            New-Item -ItemType Directory -Path $runtimeFeed -Force | Out-Null
            & $script:NewTestRuntimePackage -TempRoot $tempRoot -PackagePath $packagePath -PackageId $packageId -PackageVersion $packageVersion -Rid $rid

            $result = & $script:InvokeKestrunCommand -Arguments @(
                'service', 'install',
                '--runtime-version', $packageVersion,
                '--runtime-source', $runtimeFeed,
                '--runtime-cache', $runtimeCache)

            $result.ExitCode | Should -Be 0
            $result.Output | Should -Match 'Cached service runtime package'
            Test-Path -LiteralPath $cachedPackagePath -PathType Leaf | Should -BeTrue
            Test-Path -LiteralPath $expandedPackageRoot -PathType Container | Should -BeTrue

            $manifestFile = Get-ChildItem -Path $expandedPackageRoot -Filter 'runtime-manifest.json' -File -Recurse | Select-Object -First 1
            $hostFile = Get-ChildItem -Path $expandedPackageRoot -Filter $hostFileName -File -Recurse | Select-Object -First 1
            $moduleManifest = Get-ChildItem -Path $expandedPackageRoot -Filter 'Demo.Module.psd1' -File -Recurse | Select-Object -First 1

            ($null -ne $manifestFile) | Should -BeTrue
            ($null -ne $hostFile) | Should -BeTrue
            ($null -ne $moduleManifest) | Should -BeTrue

            $manifest = Get-Content -LiteralPath $manifestFile.FullName -Raw | ConvertFrom-Json
            $manifest.rid | Should -Be $rid
            $manifest.entryPoint | Should -Be "host/$hostFileName"
            $manifest.modulesPath | Should -Be 'modules'
        } finally {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
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

    It 'returns JSON error payload for service start when service is missing' {
        $missingServiceName = "kestrun-missing-start-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        $result = & $script:InvokeKestrunCommand -Arguments @('service', 'start', '--name', $missingServiceName, '--json')

        $result.ExitCode | Should -Not -Be 0
        $payload = & $script:GetJsonPayloadFromOutput -Output $result.Output
        $payload.Operation | Should -Be 'start'
        $payload.ServiceName | Should -Be $missingServiceName
        $payload.Status | Should -Be 'failed'
        $payload.Message | Should -Not -BeNullOrEmpty
    }

    It 'returns JSON error payload for service stop when service is missing' {
        $missingServiceName = "kestrun-missing-stop-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        $result = & $script:InvokeKestrunCommand -Arguments @('service', 'stop', '--name', $missingServiceName, '--json')

        $result.ExitCode | Should -Not -Be 0
        $payload = & $script:GetJsonPayloadFromOutput -Output $result.Output
        $payload.Operation | Should -Be 'stop'
        $payload.ServiceName | Should -Be $missingServiceName
        $payload.Status | Should -Be 'failed'
        $payload.Message | Should -Not -BeNullOrEmpty
    }

    It 'returns JSON error payload for service query when service is missing' {
        $missingServiceName = "kestrun-missing-query-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
        $result = & $script:InvokeKestrunCommand -Arguments @('service', 'query', '--name', $missingServiceName, '--json')

        $result.ExitCode | Should -Not -Be 0
        $payload = & $script:GetJsonPayloadFromOutput -Output $result.Output
        $payload.Operation | Should -Be 'query'
        $payload.ServiceName | Should -Be $missingServiceName
        $payload.Status | Should -Be 'failed'
        $payload.Message | Should -Not -BeNullOrEmpty
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
