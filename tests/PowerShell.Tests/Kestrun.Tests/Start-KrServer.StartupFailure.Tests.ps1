param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'Start-KrServer startup failure handling' -Tag 'Integration' {
    It 'throws when the listener port is already in use' {
        $instance = Start-ExampleScript -Scriptblock {
            param(
                [int]$Port = 5000,
                [IPAddress]$IPAddress = [IPAddress]::Loopback
            )

            New-KrServer -Name 'StartKrServerPortOwner'
            Add-KrEndpoint -Port $Port -IPAddress $IPAddress

            Enable-KrConfiguration
            Start-KrServer
        }

        try {
            $kestrunModulePath = Get-KestrunModulePath
            $tempDir = [System.IO.Path]::GetTempPath()
            $conflictScript = Join-Path $tempDir ('kestrun-startup-conflict-' + [System.Guid]::NewGuid().ToString('N') + '.ps1')
            $conflictStdOut = Join-Path $tempDir ('kestrun-startup-conflict-' + [System.Guid]::NewGuid().ToString('N') + '.out.log')
            $conflictStdErr = Join-Path $tempDir ('kestrun-startup-conflict-' + [System.Guid]::NewGuid().ToString('N') + '.err.log')

            @"
param([int]`$Port = 5000)

New-KrServer -Name 'StartKrServerPortConflict'
Add-KrEndpoint -Port `$Port -IPAddress ([IPAddress]::Loopback)

Enable-KrConfiguration
Start-KrServer -NoWait
"@ | Set-Content -Path $conflictScript -Encoding UTF8

            $process = Start-Process -FilePath 'pwsh' -ArgumentList @(
                '-NoLogo',
                '-NoProfile',
                '-Command',
                "Import-Module '$kestrunModulePath'; . '$conflictScript' -Port $($instance.Port)"
            ) -PassThru -RedirectStandardOutput $conflictStdOut -RedirectStandardError $conflictStdErr

            $null = $process.WaitForExit(20000)
            $process.ExitCode | Should -Not -Be 0

            $stdout = if (Test-Path $conflictStdOut) { Get-Content -Path $conflictStdOut -Raw } else { '' }
            $stderr = if (Test-Path $conflictStdErr) { Get-Content -Path $conflictStdErr -Raw } else { '' }

            $stdout | Should -Not -Match 'Kestrun server started successfully\.'
            ($stdout + "`n" + $stderr) | Should -Match 'Failed to bind to address|Address already in use|Only one usage of each socket address'

            Remove-Item -Path $conflictScript, $conflictStdOut, $conflictStdErr -Force -ErrorAction SilentlyContinue
        } finally {
            if ($instance) {
                Stop-ExampleScript -Instance $instance
                Write-KrExampleInstanceOnFailure -Instance $instance
            }
        }
    }
}
