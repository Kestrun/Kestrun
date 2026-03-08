param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')

    $script:root = Get-ProjectRootDirectory
    $script:kestrunLauncher = Join-Path $script:root 'src/PowerShell/Kestrun/kestrun.ps1'
    $script:kestrunToolProject = Join-Path $script:root 'src/CSharp/Kestrun.Tool/Kestrun.Tool.csproj'

    if ((-not (Test-Path -Path $script:kestrunLauncher -PathType Leaf)) -and (-not (Test-Path -Path $script:kestrunToolProject -PathType Leaf))) {
        throw "Neither kestrun launcher nor Kestrun.Tool project was found. Checked: $script:kestrunLauncher ; $script:kestrunToolProject"
    }

    $script:InvokeKestrunCommand = {
        param(
            [Parameter(Mandatory)]
            [string[]]$Arguments
        )

        if (Test-Path -Path $script:kestrunLauncher -PathType Leaf) {
            $output = & $script:kestrunLauncher @Arguments 2>&1 | Out-String
        } else {
            $output = & dotnet run --project $script:kestrunToolProject -- @Arguments 2>&1 | Out-String
        }
        [pscustomobject]@{
            ExitCode = $LASTEXITCODE
            Output = $output
        }
    }
}

Describe 'KestrunTool module command surface' {
    It 'shows module command in top-level help' {
        $result = & $script:InvokeKestrunCommand -Arguments @('--help')

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'module\s+Manage Kestrun module'
        $result.Output | Should -Match 'kestrun module help'
    }

    It 'shows module install/update/remove/info usage in module help' {
        $result = & $script:InvokeKestrunCommand -Arguments @('module', 'help')

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'module install \[--version <version>\] \[--scope <local\|global>\]'
        $result.Output | Should -Match 'module update \[--version <version>\] \[--scope <local\|global>\] \[--force\]'
        $result.Output | Should -Match 'module remove \[--scope <local\|global>\]'
        $result.Output | Should -Match 'module info \[--scope <local\|global>\]'
        $result.Output | Should -Match '--force'
        $result.Output | Should -Match 'deletion progress in interactive terminals'
    }

    It 'accepts --nocheck before top-level meta commands' {
        $result = & $script:InvokeKestrunCommand -Arguments @('--nocheck', 'version')

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Not -Match 'Unknown option:'
    }

    It 'accepts --no-check alias before top-level meta commands' {
        $result = & $script:InvokeKestrunCommand -Arguments @('--no-check', 'version')

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Not -Match 'Unknown option:'
    }

    It 'fails module install when --force is provided' {
        $result = & $script:InvokeKestrunCommand -Arguments @('module', 'install', '--force')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'does not accept --force'
    }

    It 'fails module remove when --version is provided' {
        $result = & $script:InvokeKestrunCommand -Arguments @('module', 'remove', '--version', '1.2.3')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'does not accept --version'
    }

    It 'fails module install when --version value is missing' {
        $result = & $script:InvokeKestrunCommand -Arguments @('module', 'install', '--version')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'Missing value for --version'
    }

    It 'fails module install with invalid scope token' {
        $result = & $script:InvokeKestrunCommand -Arguments @('module', 'install', '--scope', 'team')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match 'Unknown module scope:'
    }

    It 'fails module update when --force is provided multiple times' {
        $result = & $script:InvokeKestrunCommand -Arguments @('module', 'update', '--force', '--force')

        $result.ExitCode | Should -Be 2
        $result.Output | Should -Match '--force was provided multiple times'
    }

    It 'accepts launcher injected Kestrun options before module info' {
        $result = & $script:InvokeKestrunCommand -Arguments @(
            '--kestrun-folder', 'C:\temp\module',
            '--kestrun-manifest', 'C:\temp\module\Kestrun.psd1',
            'module', 'info')

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'Module name:\s+Kestrun'
        $result.Output | Should -Match 'Selected module scope:\s+local'
    }
}
