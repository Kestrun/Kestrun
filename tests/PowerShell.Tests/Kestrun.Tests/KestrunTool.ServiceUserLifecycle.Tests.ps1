param()

BeforeDiscovery {
    # Compute environment capabilities at discovery time so -Skip conditions are accurate.
    $script:isWindowsAdmin = $false
    $script:isLinuxRoot = $false
    $script:isMacRoot = $false
    $script:hasLinuxUserMgmt = $false
    $script:hasLinuxSystemctl = $false
    $script:hasLinuxSystemd = $false
    $script:hasMacDscl = $false
    $script:hasMacLaunchctl = $false

    if ($IsWindows) {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = [Security.Principal.WindowsPrincipal]::new($identity)
        $script:isWindowsAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }

    if ($IsLinux) {
        $script:isLinuxRoot = $env:USER -eq 'root'
        $script:hasLinuxUserMgmt = [bool](Get-Command useradd -ErrorAction SilentlyContinue) -and [bool](Get-Command userdel -ErrorAction SilentlyContinue)
        $script:hasLinuxSystemctl = [bool](Get-Command systemctl -ErrorAction SilentlyContinue)
        $script:hasLinuxSystemd = $script:hasLinuxSystemctl -and (Test-Path -Path '/run/systemd/system' -PathType Container)
    }

    if ($IsMacOS) {
        $script:isMacRoot = $env:USER -eq 'root'
        $script:hasMacDscl = [bool](Get-Command dscl -ErrorAction SilentlyContinue)
        $script:hasMacLaunchctl = [bool](Get-Command launchctl -ErrorAction SilentlyContinue)
    }
}

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
    # Compute environment capabilities at discovery time so -Skip conditions are accurate.
    $script:isWindowsAdmin = $false
    $script:isLinuxRoot = $false
    $script:isMacRoot = $false
    $script:hasLinuxUserMgmt = $false
    $script:hasLinuxSystemctl = $false
    $script:hasLinuxSystemd = $false
    $script:hasMacDscl = $false
    $script:hasMacLaunchctl = $false

    if ($IsWindows) {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = [Security.Principal.WindowsPrincipal]::new($identity)
        $script:isWindowsAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }

    if ($IsLinux) {
        $script:isLinuxRoot = $env:USER -eq 'root'
        $script:hasLinuxUserMgmt = [bool](Get-Command useradd -ErrorAction SilentlyContinue) -and [bool](Get-Command userdel -ErrorAction SilentlyContinue)
        $script:hasLinuxSystemctl = [bool](Get-Command systemctl -ErrorAction SilentlyContinue)
        $script:hasLinuxSystemd = $script:hasLinuxSystemctl -and (Test-Path -Path '/run/systemd/system' -PathType Container)
    }

    if ($IsMacOS) {
        $script:isMacRoot = $env:USER -eq 'root'
        $script:hasMacDscl = [bool](Get-Command dscl -ErrorAction SilentlyContinue)
        $script:hasMacLaunchctl = [bool](Get-Command launchctl -ErrorAction SilentlyContinue)
    }



    $script:root = Get-ProjectRootDirectory
    $script:kestrunLauncher = Join-Path $script:root 'src/PowerShell/Kestrun/kestrun.ps1'
    $script:kestrunToolProject = Join-Path $script:root 'src/CSharp/Kestrun.Tool/Kestrun.Tool.csproj'
    $script:localRuntimeFeed = Join-Path $script:root 'artifacts/nuget'
    $script:hasLocalRuntimeFeed = (Test-Path -Path $script:localRuntimeFeed -PathType Container) -and @(
        Get-ChildItem -Path $script:localRuntimeFeed -Filter 'Kestrun.Service.*.nupkg' -File -ErrorAction SilentlyContinue
    ).Count -gt 0
    $script:hasDotnetKestrunTool = [bool](Get-Command dotnet-kestrun -ErrorAction SilentlyContinue)
    $script:kestrunCommandTimeoutSeconds = 90

    if ((-not (Test-Path -Path $script:kestrunLauncher -PathType Leaf)) -and (-not $script:hasDotnetKestrunTool) -and (-not (Test-Path -Path $script:kestrunToolProject -PathType Leaf))) {
        throw "No Kestrun command source found. Checked launcher: $script:kestrunLauncher ; dotnet tool command: dotnet-kestrun ; project: $script:kestrunToolProject"
    }

    $script:InvokeKestrunCommand = {
        param(
            [Parameter(Mandatory)]
            [string[]]$Arguments
        )

        $stdoutPath = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-tool-{0}.stdout.log' -f [System.IO.Path]::GetRandomFileName())
        $stderrPath = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-tool-{0}.stderr.log' -f [System.IO.Path]::GetRandomFileName())

        try {
            if (Test-Path -Path $script:kestrunLauncher -PathType Leaf) {
                $filePath = Get-PwshExecutable
                $argumentList = @('-NoLogo', '-NoProfile', '-File', $script:kestrunLauncher) + $Arguments
            } elseif ($script:hasDotnetKestrunTool) {
                $filePath = 'dotnet'
                $argumentList = @('kestrun') + $Arguments
            } else {
                $filePath = 'dotnet'
                $argumentList = @('run', '--project', $script:kestrunToolProject, '--') + $Arguments
            }

            $process = Start-Process -FilePath $filePath `
                -ArgumentList $argumentList `
                -WorkingDirectory $script:root `
                -PassThru `
                -RedirectStandardOutput $stdoutPath `
                -RedirectStandardError $stderrPath

            if (-not $process.WaitForExit($script:kestrunCommandTimeoutSeconds * 1000)) {
                try {
                    $process.Kill($true)
                } catch {
                    Write-Verbose ('Failed to terminate timed-out Kestrun command: {0}' -f $_.Exception.Message)
                }

                $stdout = if (Test-Path -Path $stdoutPath) { Get-Content -Path $stdoutPath -Raw } else { '' }
                $stderr = if (Test-Path -Path $stderrPath) { Get-Content -Path $stderrPath -Raw } else { '' }
                $output = @(
                    "Timed out after $($script:kestrunCommandTimeoutSeconds) second(s): $filePath $($argumentList -join ' ')"
                    $stdout
                    $stderr
                ) -join [Environment]::NewLine

                return [pscustomobject]@{
                    ExitCode = 124
                    CommandLine = "$filePath $($argumentList -join ' ')"
                    StdOut = $stdout
                    StdErr = $stderr
                    Output = $output
                }
            }

            $stdout = if (Test-Path -Path $stdoutPath) { Get-Content -Path $stdoutPath -Raw } else { '' }
            $stderr = if (Test-Path -Path $stderrPath) { Get-Content -Path $stderrPath -Raw } else { '' }
            $outputParts = @($stdout, $stderr) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
            $output = $outputParts -join [Environment]::NewLine

            [pscustomobject]@{
                ExitCode = $process.ExitCode
                CommandLine = "$filePath $($argumentList -join ' ')"
                StdOut = $stdout
                StdErr = $stderr
                Output = $output
            }
        } finally {
            Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
        }
    }

    $script:AssertKestrunCommandSucceeded = {
        param(
            [Parameter(Mandatory)]
            $Result,
            [Parameter(Mandatory)]
            [string]$Action
        )

        if ($Result.ExitCode -eq 0) {
            return
        }

        $stdout = if ([string]::IsNullOrWhiteSpace($Result.StdOut)) { '<empty>' } else { $Result.StdOut.TrimEnd() }
        $stderr = if ([string]::IsNullOrWhiteSpace($Result.StdErr)) { '<empty>' } else { $Result.StdErr.TrimEnd() }

        throw @(
            "Kestrun command failed during $Action."
            "ExitCode: $($Result.ExitCode)"
            "Command: $($Result.CommandLine)"
            'stdout:'
            $stdout
            'stderr:'
            $stderr
        ) -join [Environment]::NewLine
    }

    $script:GetServiceInstallRuntimeArguments = {
        param(
            [Parameter(Mandatory)]
            [string]$DeploymentRoot
        )

        if (-not $script:hasLocalRuntimeFeed) {
            return @()
        }

        $runtimeCache = Join-Path $DeploymentRoot '.runtime-cache'
        return @('--runtime-source', $script:localRuntimeFeed, '--runtime-cache', $runtimeCache)
    }

    $script:InvokeKestrunServiceInstall = {
        param(
            [Parameter(Mandatory)]
            [string[]]$Arguments,
            [Parameter(Mandatory)]
            [string]$DeploymentRoot,
            [Parameter(Mandatory)]
            [string]$ScriptPath
        )

        $installArguments = @($Arguments) + @(& $script:GetServiceInstallRuntimeArguments -DeploymentRoot $DeploymentRoot) + @($ScriptPath)
        & $script:InvokeKestrunCommand -Arguments $installArguments
    }
}

Describe 'KestrunTool service user lifecycle' {
    It 'handles full Linux lifecycle with default service account' -Skip:(-not ($IsLinux -and $script:hasLinuxSystemd)) {
        $suffix = ([Guid]::NewGuid().ToString('N')).Substring(0, 8)
        $serviceName = "test-default-$suffix"
        $deploymentRoot = "/tmp/kestrun-service-default-$suffix"
        $scriptPath = Join-Path $script:root 'docs/_includes/examples/pwsh/10.2-OpenAPI-Component-Schema.ps1'

        try {
            $installResult = & $script:InvokeKestrunServiceInstall -Arguments @(
                'service', 'install',
                '--name', $serviceName,
                '--deployment-root', $deploymentRoot
            ) -DeploymentRoot $deploymentRoot -ScriptPath $scriptPath
            & $script:AssertKestrunCommandSucceeded $installResult 'Linux default service install'

            $startResult = & $script:InvokeKestrunCommand -Arguments @('service', 'start', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $startResult 'Linux default service start'

            Start-Sleep -Seconds 2
            $queryResult = & $script:InvokeKestrunCommand -Arguments @('service', 'query', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $queryResult 'Linux default service query'
            $queryResult.Output | Should -Match ('(?im)^\s*query\s*\|\s*' + [regex]::Escape($serviceName) + '\s*\|\s*linux\s*\|\s*success\s*\|\s*running\b')

            $stopResult = & $script:InvokeKestrunCommand -Arguments @('service', 'stop', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $stopResult 'Linux default service stop'

            $removeResult = & $script:InvokeKestrunCommand -Arguments @('service', 'remove', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $removeResult 'Linux default service remove'
        } finally {
            if ($script:isLinuxRoot) {
                & systemctl stop "$serviceName.service" 2>$null | Out-Null
                & systemctl disable "$serviceName.service" 2>$null | Out-Null
                Remove-Item -Path "/etc/systemd/system/$serviceName.service" -Force -ErrorAction SilentlyContinue
                & systemctl daemon-reload 2>$null | Out-Null
            } else {
                & systemctl --user stop "$serviceName.service" 2>$null | Out-Null
                & systemctl --user disable "$serviceName.service" 2>$null | Out-Null
                Remove-Item -Path "$HOME/.config/systemd/user/$serviceName.service" -Force -ErrorAction SilentlyContinue
                & systemctl --user daemon-reload 2>$null | Out-Null
            }

            Remove-Item -LiteralPath $deploymentRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'handles full Linux lifecycle for a dedicated service user' -Skip:(-not ($IsLinux -and $script:isLinuxRoot -and $script:hasLinuxUserMgmt -and $script:hasLinuxSystemd)) {
        $suffix = ([Guid]::NewGuid().ToString('N')).Substring(0, 8)
        $serviceUser = "krsvc_$suffix"
        $serviceName = "test-user-$suffix"
        $deploymentRoot = "/tmp/kestrun-service-user-$suffix"
        $scriptPath = Join-Path $script:root 'docs/_includes/examples/pwsh/10.2-OpenAPI-Component-Schema.ps1'

        try {
            & useradd --system --no-create-home --shell /usr/sbin/nologin $serviceUser | Out-Null
            $LASTEXITCODE | Should -Be 0

            $installResult = & $script:InvokeKestrunServiceInstall -Arguments @(
                'service', 'install',
                '--name', $serviceName,
                '--service-user', $serviceUser,
                '--deployment-root', $deploymentRoot
            ) -DeploymentRoot $deploymentRoot -ScriptPath $scriptPath

            & $script:AssertKestrunCommandSucceeded $installResult 'Linux dedicated-user service install'

            $startResult = & $script:InvokeKestrunCommand -Arguments @('service', 'start', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $startResult 'Linux dedicated-user service start'

            Start-Sleep -Seconds 2
            $queryResult = & $script:InvokeKestrunCommand -Arguments @('service', 'query', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $queryResult 'Linux dedicated-user service query'
            $queryResult.Output | Should -Match ('(?im)^\s*query\s*\|\s*' + [regex]::Escape($serviceName) + '\s*\|\s*linux\s*\|\s*success\s*\|\s*running\b')

            $stopResult = & $script:InvokeKestrunCommand -Arguments @('service', 'stop', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $stopResult 'Linux dedicated-user service stop'

            $removeResult = & $script:InvokeKestrunCommand -Arguments @('service', 'remove', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $removeResult 'Linux dedicated-user service remove'
        } finally {
            & systemctl stop "$serviceName.service" 2>$null | Out-Null
            & systemctl disable "$serviceName.service" 2>$null | Out-Null
            Remove-Item -Path "/etc/systemd/system/$serviceName.service" -Force -ErrorAction SilentlyContinue
            & systemctl daemon-reload 2>$null | Out-Null
            & userdel $serviceUser 2>$null | Out-Null
            Remove-Item -LiteralPath $deploymentRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'handles full macOS lifecycle for a dedicated service user' -Skip:(-not ($IsMacOS -and $script:isMacRoot -and $script:hasMacDscl -and $script:hasMacLaunchctl)) {
        $suffix = ([Guid]::NewGuid().ToString('N')).Substring(0, 8)
        $serviceUser = "krsvc$suffix"
        $serviceName = "test-user-$suffix"
        $deploymentRoot = "/tmp/kestrun-service-user-$suffix"
        $scriptPath = Join-Path $script:root 'docs/_includes/examples/pwsh/10.2-OpenAPI-Component-Schema.ps1'
        $plistPath = "/Library/LaunchDaemons/$serviceName.plist"

        $existingUidLines = @(& dscl . -list /Users UniqueID 2>$null)
        $LASTEXITCODE | Should -Be 0

        $existingUids = @{}
        foreach ($uidLine in $existingUidLines) {
            if ($uidLine -match '^\S+\s+(\d+)$') {
                $existingUids[$Matches[1]] = $true
            }
        }

        $uid = $null
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            $candidateUid = [string](55000 + (Get-Random -Minimum 100 -Maximum 1000))
            if (-not $existingUids.ContainsKey($candidateUid)) {
                $uid = $candidateUid
                break
            }
        }

        if ($null -eq $uid) {
            throw 'Unable to allocate an unused macOS test UniqueID in the 55100-55999 range after 20 attempts.'
        }

        try {
            & dscl . -create "/Users/$serviceUser" | Out-Null
            & dscl . -create "/Users/$serviceUser" UserShell /usr/bin/false | Out-Null
            & dscl . -create "/Users/$serviceUser" RealName "Kestrun Service $suffix" | Out-Null
            & dscl . -create "/Users/$serviceUser" UniqueID $uid | Out-Null
            & dscl . -create "/Users/$serviceUser" PrimaryGroupID 20 | Out-Null
            & dscl . -create "/Users/$serviceUser" NFSHomeDirectory /var/empty | Out-Null
            $LASTEXITCODE | Should -Be 0

            $installResult = & $script:InvokeKestrunServiceInstall -Arguments @(
                'service', 'install',
                '--name', $serviceName,
                '--service-user', $serviceUser,
                '--deployment-root', $deploymentRoot
            ) -DeploymentRoot $deploymentRoot -ScriptPath $scriptPath
            & $script:AssertKestrunCommandSucceeded $installResult 'macOS dedicated-user service install'

            $startResult = & $script:InvokeKestrunCommand -Arguments @('service', 'start', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $startResult 'macOS dedicated-user service start'

            Start-Sleep -Seconds 2
            $queryResult = & $script:InvokeKestrunCommand -Arguments @('service', 'query', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $queryResult 'macOS dedicated-user service query'

            $stopResult = & $script:InvokeKestrunCommand -Arguments @('service', 'stop', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $stopResult 'macOS dedicated-user service stop'

            $removeResult = & $script:InvokeKestrunCommand -Arguments @('service', 'remove', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $removeResult 'macOS dedicated-user service remove'
        } finally {
            & launchctl bootout "system/$serviceName" 2>$null | Out-Null
            Remove-Item -LiteralPath $plistPath -Force -ErrorAction SilentlyContinue
            & dscl . -delete "/Users/$serviceUser" 2>$null | Out-Null
            Remove-Item -LiteralPath $deploymentRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'handles full macOS lifecycle with default service account' -Skip:(-not ($IsMacOS -and $script:hasMacLaunchctl)) {
        $suffix = ([Guid]::NewGuid().ToString('N')).Substring(0, 8)
        $serviceName = "test-default-$suffix"
        $deploymentRoot = "/tmp/kestrun-service-default-$suffix"
        $scriptPath = Join-Path $script:root 'docs/_includes/examples/pwsh/10.2-OpenAPI-Component-Schema.ps1'
        $plistPath = "$HOME/Library/LaunchAgents/$serviceName.plist"

        try {
            $installResult = & $script:InvokeKestrunServiceInstall -Arguments @(
                'service', 'install',
                '--name', $serviceName,
                '--deployment-root', $deploymentRoot
            ) -DeploymentRoot $deploymentRoot -ScriptPath $scriptPath
            & $script:AssertKestrunCommandSucceeded $installResult 'macOS default service install'

            $startResult = & $script:InvokeKestrunCommand -Arguments @('service', 'start', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $startResult 'macOS default service start'

            Start-Sleep -Seconds 2
            $queryResult = & $script:InvokeKestrunCommand -Arguments @('service', 'query', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $queryResult 'macOS default service query'

            $stopResult = & $script:InvokeKestrunCommand -Arguments @('service', 'stop', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $stopResult 'macOS default service stop'

            $removeResult = & $script:InvokeKestrunCommand -Arguments @('service', 'remove', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $removeResult 'macOS default service remove'
        } finally {
            # launchctl gui domain requires the numeric user ID (uid), not the process session ID.
            $userId = (& /usr/bin/id -u 2>$null | Select-Object -First 1)
            if (-not [string]::IsNullOrWhiteSpace($userId)) {
                & launchctl bootout "gui/$userId/$serviceName" 2>$null | Out-Null
            }
            & launchctl unload "$plistPath" 2>$null | Out-Null
            Remove-Item -LiteralPath $plistPath -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $deploymentRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'handles full Windows lifecycle for a dedicated service user' -Skip:(-not ($IsWindows -and $script:isWindowsAdmin)) {
        $suffix = ([Guid]::NewGuid().ToString('N')).Substring(0, 8)
        $serviceUser = "krsvc_$suffix"
        $machineQualifiedServiceUser = "$env:COMPUTERNAME\$serviceUser"
        # Keep password <=14 chars to avoid net.exe interactive legacy warning prompt.
        # Explicitly guarantee complexity: upper (T/Z), lower (a/b), digit (9), special (!).
        $servicePassword = "T!a9Zb$($suffix.Substring(0, 4))"
        $serviceName = "test-user-$suffix"
        $deploymentRoot = Join-Path $env:TEMP "kestrun-service-user-$suffix"
        $scriptPath = Join-Path $script:root 'docs/_includes/examples/pwsh/10.2-OpenAPI-Component-Schema.ps1'
        $originalServiceLogonRightSids = $null
        $serviceLogonRightUpdated = $false
        $servicePolicySid = $null

        try {
            & net.exe user $serviceUser $servicePassword /add | Out-Null
            $LASTEXITCODE | Should -Be 0

            $originalServiceLogonRightSids = @(Get-WindowsServiceLogonRightSid -WorkingDirectory $script:root)
            $servicePolicySid = Convert-AccountToPolicySid -AccountName $machineQualifiedServiceUser

            $denyServiceLogonRightSids = @(Get-WindowsDenyServiceLogonRightSid -WorkingDirectory $script:root)
            if ($denyServiceLogonRightSids -contains $servicePolicySid) {
                Set-ItResult -Skipped -Because 'Service user is denied "Log on as a service" by local/domain policy.'
                return
            }

            $updatedServiceLogonRightSids = @($originalServiceLogonRightSids + $servicePolicySid | Select-Object -Unique)
            Set-WindowsServiceLogonRightSid -Sids $updatedServiceLogonRightSids -WorkingDirectory $script:root
            $serviceLogonRightUpdated = $true

            $effectiveServiceLogonRightSids = @(Get-WindowsServiceLogonRightSid -WorkingDirectory $script:root)
            if (-not ($effectiveServiceLogonRightSids -contains $servicePolicySid)) {
                Set-ItResult -Skipped -Because 'Unable to grant "Log on as a service" due to host policy restrictions.'
                return
            }

            $installResult = & $script:InvokeKestrunServiceInstall -Arguments @(
                'service', 'install',
                '--name', $serviceName,
                '--service-user', $machineQualifiedServiceUser,
                '--service-password', $servicePassword,
                '--deployment-root', $deploymentRoot
            ) -DeploymentRoot $deploymentRoot -ScriptPath $scriptPath
            & $script:AssertKestrunCommandSucceeded $installResult 'Windows dedicated-user service install'

            & icacls.exe $deploymentRoot /grant "${machineQualifiedServiceUser}:(OI)(CI)M" /t /c | Out-Null

            $startResult = & $script:InvokeKestrunCommand -Arguments @('service', 'start', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $startResult 'Windows dedicated-user service start'

            Start-Sleep -Seconds 2
            $queryResult = & $script:InvokeKestrunCommand -Arguments @('service', 'query', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $queryResult 'Windows dedicated-user service query'
            $queryResult.Output | Should -Match 'RUNNING'

            $stopResult = & $script:InvokeKestrunCommand -Arguments @('service', 'stop', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $stopResult 'Windows dedicated-user service stop'

            $removeResult = & $script:InvokeKestrunCommand -Arguments @('service', 'remove', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $removeResult 'Windows dedicated-user service remove'
        } finally {
            & sc.exe stop $serviceName 2>$null | Out-Null
            & sc.exe delete $serviceName 2>$null | Out-Null
            & net.exe user $serviceUser /delete 2>$null | Out-Null
            if ($serviceLogonRightUpdated -and $null -ne $originalServiceLogonRightSids) {
                Set-WindowsServiceLogonRightSid -Sids $originalServiceLogonRightSids -WorkingDirectory $script:root
            }
            Remove-Item -LiteralPath $deploymentRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'handles full Windows lifecycle with default service account' -Skip:(-not ($IsWindows -and $script:isWindowsAdmin)) {
        $suffix = ([Guid]::NewGuid().ToString('N')).Substring(0, 8)
        $serviceName = "test-default-$suffix"
        $deploymentRoot = Join-Path $env:TEMP "kestrun-service-default-$suffix"
        $scriptPath = Join-Path $script:root 'docs/_includes/examples/pwsh/10.2-OpenAPI-Component-Schema.ps1'

        try {
            $installResult = & $script:InvokeKestrunServiceInstall -Arguments @(
                'service', 'install',
                '--name', $serviceName,
                '--deployment-root', $deploymentRoot
            ) -DeploymentRoot $deploymentRoot -ScriptPath $scriptPath
            & $script:AssertKestrunCommandSucceeded $installResult 'Windows default service install'

            $startResult = & $script:InvokeKestrunCommand -Arguments @('service', 'start', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $startResult 'Windows default service start'

            Start-Sleep -Seconds 2
            $queryResult = & $script:InvokeKestrunCommand -Arguments @('service', 'query', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $queryResult 'Windows default service query'
            $queryResult.Output | Should -Match 'RUNNING'

            $stopResult = & $script:InvokeKestrunCommand -Arguments @('service', 'stop', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $stopResult 'Windows default service stop'

            $removeResult = & $script:InvokeKestrunCommand -Arguments @('service', 'remove', '--name', $serviceName)
            & $script:AssertKestrunCommandSucceeded $removeResult 'Windows default service remove'
        } finally {
            & sc.exe stop $serviceName 2>$null | Out-Null
            & sc.exe delete $serviceName 2>$null | Out-Null
            Remove-Item -LiteralPath $deploymentRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'handles full Windows lifecycle with NetworkService account' -Skip:(-not ($IsWindows -and $script:isWindowsAdmin)) {
        $suffix = ([Guid]::NewGuid().ToString('N')).Substring(0, 8)
        $serviceName = "test-networkservice-$suffix"
        $deploymentRoot = Join-Path $env:TEMP "kestrun-service-networkservice-$suffix"
        $scriptPath = Join-Path $script:root 'docs/_includes/examples/pwsh/10.2-OpenAPI-Component-Schema.ps1'
        $skipBecause = $null

        try {
            try {
                [void]([System.Security.Principal.NTAccount]'NT AUTHORITY\NETWORK SERVICE').Translate([System.Security.Principal.SecurityIdentifier])
            } catch {
                Set-ItResult -Skipped -Because 'NetworkService principal is not resolvable on this host.'
                return
            }

            $denyServiceLogonRightSids = @(Get-WindowsDenyServiceLogonRightSid -WorkingDirectory $script:root)
            if ($denyServiceLogonRightSids -contains 'S-1-5-20') {
                Set-ItResult -Skipped -Because 'NetworkService is denied "Log on as a service" by local/domain policy.'
                return
            }

            $installResult = & $script:InvokeKestrunServiceInstall -Arguments @(
                'service', 'install',
                '--name', $serviceName,
                '--service-user', 'NetworkService',
                '--deployment-root', $deploymentRoot
            ) -DeploymentRoot $deploymentRoot -ScriptPath $scriptPath

            if ($installResult.ExitCode -ne 0) {
                if ($installResult.Output -match '(?i)access is denied|logon failure|service logon|cannot find the account|networkservice') {
                    $skipBecause = 'NetworkService install is blocked by host policy on this machine.'
                } else {
                    & $script:AssertKestrunCommandSucceeded $installResult 'Windows NetworkService install'
                }
            }

            if ($null -ne $skipBecause) {
                return
            }

            & icacls.exe $deploymentRoot /grant '*S-1-5-20:(OI)(CI)M' /t /c | Out-Null

            $startResult = & $script:InvokeKestrunCommand -Arguments @('service', 'start', '--name', $serviceName)
            if ($startResult.ExitCode -eq 5 -or $startResult.Output -match '(?i)access is denied|logon failure|service logon|1069|1057') {
                $skipBecause = 'NetworkService lacks required host permissions on this machine.'
            } else {
                & $script:AssertKestrunCommandSucceeded $startResult 'Windows NetworkService start'

                Start-Sleep -Seconds 2
                $queryResult = & $script:InvokeKestrunCommand -Arguments @('service', 'query', '--name', $serviceName)
                & $script:AssertKestrunCommandSucceeded $queryResult 'Windows NetworkService query'
                $queryResult.Output | Should -Match 'RUNNING'

                $stopResult = & $script:InvokeKestrunCommand -Arguments @('service', 'stop', '--name', $serviceName)
                & $script:AssertKestrunCommandSucceeded $stopResult 'Windows NetworkService stop'

                $removeResult = & $script:InvokeKestrunCommand -Arguments @('service', 'remove', '--name', $serviceName)
                & $script:AssertKestrunCommandSucceeded $removeResult 'Windows NetworkService remove'
            }
        } finally {
            & sc.exe stop $serviceName 2>$null | Out-Null
            & sc.exe delete $serviceName 2>$null | Out-Null
            Remove-Item -LiteralPath $deploymentRoot -Recurse -Force -ErrorAction SilentlyContinue
        }

        if ($null -ne $skipBecause) {
            Set-ItResult -Skipped -Because $skipBecause
        }
    }
}
