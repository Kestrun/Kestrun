param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')

    $script:root = Get-ProjectRootDirectory
    $script:kestrunLauncher = Join-Path $script:root 'src/PowerShell/Kestrun/kestrun.ps1'

    if (-not (Test-Path -Path $script:kestrunLauncher -PathType Leaf)) {
        throw "kestrun launcher not found: $script:kestrunLauncher"
    }

    $script:port = Get-FreeTcpPort
    $script:tempScript = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-managed-stop-' + [System.Guid]::NewGuid().ToString('N') + '.ps1')
    $script:stdOut = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-managed-stop-' + [System.Guid]::NewGuid().ToString('N') + '.out.log')
    $script:stdErr = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-managed-stop-' + [System.Guid]::NewGuid().ToString('N') + '.err.log')

    $scriptContent = @"
param([int]`$Port = 5000)

`$server = New-KrServer -Name 'KestrunManagedStopTest'
Add-KrEndpoint -Port `$Port -IPAddress '127.0.0.1'

Add-KrMapRoute -Verbs Get -Pattern '/online' -ScriptBlock {
    Write-KrTextResponse -InputObject 'OK' -StatusCode 200
}

Add-KrMapRoute -Verbs Get -Pattern '/shutdown' -ScriptBlock {
    Stop-KrServer
}

Enable-KrConfiguration -Server `$server
Start-KrServer -Server `$server
"@

    Set-Content -Path $script:tempScript -Value $scriptContent -Encoding UTF8

    $escapedLauncher = $script:kestrunLauncher.Replace("'", "''")
    $escapedScript = $script:tempScript.Replace("'", "''")
    $kestrunInvoke = "& '$escapedLauncher' -Arguments @('run','$escapedScript','--arguments','$($script:port)')"

    $script:process = Start-Process -FilePath 'pwsh' -ArgumentList @(
        '-NoLogo',
        '-NoProfile',
        '-Command',
        $kestrunInvoke
    ) -PassThru -RedirectStandardOutput $script:stdOut -RedirectStandardError $script:stdErr

    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    $script:isReady = $false

    while ([DateTime]::UtcNow -lt $deadline -and -not $script:isReady) {
        if ($script:process.HasExited) {
            break
        }

        try {
            $response = Invoke-WebRequest -Uri "http://127.0.0.1:$($script:port)/online" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            if ($response.StatusCode -eq 200) {
                $script:isReady = $true
                break
            }
        } catch {
            Start-Sleep -Milliseconds 200
        }
    }
}

AfterAll {
    if ($script:process -and -not $script:process.HasExited) {
        $script:process | Stop-Process -Force
    }

    if ($script:process) {
        $script:process.Dispose()
    }

    Remove-Item -Path $script:tempScript -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $script:stdOut -Force -ErrorAction SilentlyContinue
    Remove-Item -Path $script:stdErr -Force -ErrorAction SilentlyContinue
}

Describe 'KestrunTool managed shutdown' {
    It 'starts via kestrun and exits cleanly when shutdown route is called' {
        if (-not $script:isReady) {
            $stdout = if (Test-Path $script:stdOut) { Get-Content -Path $script:stdOut -Raw } else { '' }
            $stderr = if (Test-Path $script:stdErr) { Get-Content -Path $script:stdErr -Raw } else { '' }
            throw "kestrun-managed test server did not become ready. stdout:`n$stdout`n---`nstderr:`n$stderr"
        }

        $shutdownResponse = Invoke-WebRequest -Uri "http://127.0.0.1:$($script:port)/shutdown" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        $shutdownResponse.StatusCode | Should -Be 202

        $script:process.WaitForExit(15000) | Should -BeTrue
        $script:process.ExitCode | Should -Be 0
    }

    It 'handles Ctrl+C sent to kestrun process (Windows best-effort)' {
        if (-not $IsWindows) {
            Set-ItResult -Skipped -Because 'Ctrl+C process signaling test is Windows-only.'
            return
        }

        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class NativeCtrl
{
    public const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;
    public const uint CTRL_BREAK_EVENT = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcessW(
        string lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleCtrlHandler(IntPtr handlerRoutine, bool add);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);
}
'@ -ErrorAction SilentlyContinue

        $port2 = Get-FreeTcpPort
        $tempScript2 = Join-Path ([System.IO.Path]::GetTempPath()) ('kestrun-ctrlc-' + [System.Guid]::NewGuid().ToString('N') + '.ps1')

        $scriptContent2 = @"
param([int]`$Port = 5000)

`$server = New-KrServer -Name 'KestrunCtrlCTest'
Add-KrEndpoint -Port `$Port -IPAddress '127.0.0.1'

Add-KrMapRoute -Verbs Get -Pattern '/online' -ScriptBlock {
    Write-KrTextResponse -InputObject 'OK' -StatusCode 200
}

Enable-KrConfiguration -Server `$server
Start-KrServer -Server `$server -Quiet:`$false
"@

        Set-Content -Path $tempScript2 -Value $scriptContent2 -Encoding UTF8

        $escapedLauncher2 = $script:kestrunLauncher.Replace("'", "''")
        $escapedScript2 = $tempScript2.Replace("'", "''")
        $kestrunInvoke2 = "& '$escapedLauncher2' -Arguments @('run','$escapedScript2','--arguments','$port2')"
        $pwshPath = (Get-Command pwsh -ErrorAction Stop).Source
        $escapedInvoke2 = $kestrunInvoke2.Replace('"', '""')
        $cmdLine = "`"$pwshPath`" -NoLogo -NoProfile -NonInteractive -Command `"$escapedInvoke2`""

        $si = New-Object NativeCtrl+STARTUPINFO
        $si.cb = [System.Runtime.InteropServices.Marshal]::SizeOf($si)
        $pi = New-Object NativeCtrl+PROCESS_INFORMATION

        $created = [NativeCtrl]::CreateProcessW(
            $pwshPath,
            $cmdLine,
            [System.IntPtr]::Zero,
            [System.IntPtr]::Zero,
            $true,
            [NativeCtrl]::CREATE_NEW_PROCESS_GROUP,
            [System.IntPtr]::Zero,
            $script:root,
            [ref]$si,
            [ref]$pi)

        if (-not $created) {
            $winErr = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
            throw "Failed to create kestrun process group for Ctrl+C test. Win32Error=$winErr"
        }

        $process2 = [System.Diagnostics.Process]::GetProcessById($pi.dwProcessId)

        try {
            $deadline2 = [DateTime]::UtcNow.AddSeconds(25)
            $ready2 = $false
            while ([DateTime]::UtcNow -lt $deadline2 -and -not $ready2) {
                if ($process2.HasExited) {
                    break
                }

                try {
                    $probe2 = Invoke-WebRequest -Uri "http://127.0.0.1:$port2/online" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
                    if ($probe2.StatusCode -eq 200) {
                        $ready2 = $true
                    }
                } catch {
                    Start-Sleep -Milliseconds 200
                }
            }

            if (-not $ready2) {
                throw 'kestrun Ctrl+C test server did not become ready.'
            }

            [NativeCtrl]::SetConsoleCtrlHandler([System.IntPtr]::Zero, $true) | Out-Null
            try {
                $sent = [NativeCtrl]::GenerateConsoleCtrlEvent([NativeCtrl]::CTRL_BREAK_EVENT, [uint32]$process2.Id)
            } finally {
                [NativeCtrl]::SetConsoleCtrlHandler([System.IntPtr]::Zero, $false) | Out-Null
            }

            if (-not $sent) {
                $winErr = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
                throw "Failed to send CTRL_BREAK_EVENT to kestrun process group. Win32Error=$winErr"
            }

            $process2.WaitForExit(20000) | Should -BeTrue
            Start-Sleep -Milliseconds 200
            (Get-Process -Id $process2.Id -ErrorAction SilentlyContinue) | Should -BeNullOrEmpty
        } finally {
            if ($process2 -and -not $process2.HasExited) {
                $process2 | Stop-Process -Force
            }

            if ($process2) {
                $process2.Dispose()
            }

            if ($pi.hThread -ne [System.IntPtr]::Zero) {
                [NativeCtrl]::CloseHandle($pi.hThread) | Out-Null
            }
            if ($pi.hProcess -ne [System.IntPtr]::Zero) {
                [NativeCtrl]::CloseHandle($pi.hProcess) | Out-Null
            }

            Remove-Item -Path $tempScript2 -Force -ErrorAction SilentlyContinue
        }
    }
}
