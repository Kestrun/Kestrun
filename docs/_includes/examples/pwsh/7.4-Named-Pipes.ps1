<#
    Sample Kestrun Server - Named Pipes (Windows)
    Demonstrates binding a server to a Windows named pipe.
    FileName: 7.4-Named-Pipes.ps1 (renumbered from 7.6)
#>
if (-not $IsWindows) { throw 'Named pipes example is Windows only.' }

New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null
New-KrServer -Name 'Endpoints Pipes'
Add-KrNamedPipeListener -PipeName 'kestrun.demo.pipe'
Add-KrPowerShellRuntime
Enable-KrConfiguration
Add-KrMapRoute -Verbs Get -Pattern '/pipe' -ScriptBlock {
    Write-KrLog -Level Debug -Message 'Pipe route hit'
    Write-KrTextResponse -InputObject 'Hello over named pipe' -StatusCode 200
}
Write-KrLog -Level Information -Message 'Named pipe listener active.'
Start-KrServer
