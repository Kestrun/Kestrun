<#
    Sample Kestrun Server - Unix Sockets
    Demonstrates binding to a Unix domain socket.
    FileName: 7.5-Unix-Sockets.ps1 (renumbered from 7.7)
#>
if ($IsWindows) { throw 'Unix socket example is for Linux/macOS only.' }

New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null
New-KrServer -Name 'Endpoints Unix'
Add-KrListenUnixSocket -SocketPath '/tmp/kestrun-demo.sock'
Add-KrPowerShellRuntime
Enable-KrConfiguration
Add-KrMapRoute -Verbs Get -Pattern '/ux' -ScriptBlock {
    Write-KrLog -Level Debug -Message 'Unix socket route'
    Write-KrTextResponse -InputObject 'Hello via unix socket'
}
Write-KrLog -Level Information -Message 'Unix socket listener active.'
Start-KrServer
