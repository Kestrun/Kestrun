param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

function Test-UnixSocketSupport {
    [CmdletBinding()]
    param()

    $iwr = Get-Command Invoke-WebRequest -ErrorAction SilentlyContinue
    if ($null -eq $iwr -or -not $iwr.Parameters.ContainsKey('UnixSocket')) {
        return $false
    }

    try {
        $probePath = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "kestrun-unix-probe-$PID.sock"
        [void][System.Net.Sockets.UnixDomainSocketEndPoint]::new($probePath)
        return $true
    } catch {
        return $false
    }
}

$script:skipUnixSocket = -not (Test-UnixSocketSupport)

Describe 'Example 7.5-Unix-Sockets' -Tag 'Tutorial', 'Slow' -Skip:$script:skipUnixSocket {
    BeforeAll {
        $examplePath = Get-ExampleScriptPath -Name '7.5-Unix-Sockets.ps1'
        $runner = [scriptblock]::Create(@"
param([int]`$Port)
. '$examplePath' -SocketPath "kestrun-demo-`$Port.sock"
"@)

        $script:instance = Start-ExampleScript -Scriptblock $runner -SkipPortProbe

        $script:socketPath = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "kestrun-demo-$($script:instance.Port).sock"
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }

        if ($script:socketPath -and (Test-Path $script:socketPath)) {
            Remove-Item -Path $script:socketPath -Force -ErrorAction SilentlyContinue
        }
    }

    It 'GET /ux over unix socket returns expected payload' {
        $resp = $null
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        while ([DateTime]::UtcNow -lt $deadline -and $null -eq $resp) {
            try {
                $resp = Invoke-WebRequest -UnixSocket $script:socketPath -Method Get -Uri 'http://localhost/ux' -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
            } catch {
                Start-Sleep -Milliseconds 200
            }
        }

        $resp | Should -Not -BeNullOrEmpty
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Be 'Hello via unix socket'
        $resp.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
    }
}
