<#
        Sample: Kestrun Server over a Windows Named Pipe
        Demonstrates binding to a Windows named pipe for local IPC instead of a TCP port.
        File:   7.4-Named-Pipes.ps1
#>

param(
    [string]$NamedPipeName = 'kestrun.demo.pipe'
)

# 1. Configure logging (console sink so we can see events)
New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Create the server host
New-KrServer -Name 'Endpoints Pipes'

# 3. Add a named pipe listener. DO NOT prepend \\./pipe/. Provide only the short name.
Add-KrNamedPipeListener -NamedPipeName $NamedPipeName

# 4. Add the PowerShell runtime so script routes can execute
Add-KrPowerShellRuntime

# 5. Finalize configuration (after this, mappings are locked in)
Enable-KrConfiguration

# 6. Map a GET /pipe route that returns a simple text response
Add-KrMapRoute -Verbs Get -Pattern '/pipe' -ScriptBlock {
    Write-KrLog -Level Debug -Message 'Pipe route hit'
    Write-KrTextResponse -InputObject 'Hello over named pipe' -StatusCode 200
}

# 7. Informational log so the console shows readiness
Write-KrLog -Level Information -Message 'Named pipe listener active {Pipe}' -PropertyValues $NamedPipeName

# 8. Start the server and close all the loggers when the server stops
# This is equivalent to calling Close-KrLogger after Start-KrServer
Start-KrServer -CloseLogsOnExit
