<#
    Sample demonstrating hot reload:
    - Dynamically change minimum level with a LoggingLevelSwitch
    - Re-register a logger with the same name to change sinks/properties
    FileName: 5.6-Hot-Reload.ps1
#>

New-KrLogger |
    Set-KrLoggerMinimumLevel -Dynamic Debug |
    Add-KrSinkConsole |
    Add-KrSinkFile -Path '.\logs\hot-reload.log' -RollingInterval Hour |
    Register-KrLogger -Name 'app' -PassThru

New-KrServer -Name "Hot Reload"
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)
Add-KrPowerShellRuntime

Enable-KrConfiguration
# Writes both Debug and Information; visibility depends on level switch
Add-KrMapRoute -Verbs Get -Path "/log" -ScriptBlock {
    Write-KrLog -LoggerName 'app' -Level Debug -Message "Debug event"
    Write-KrLog -LoggerName 'app' -Level Information -Message "Information event"
    Write-KrLog -LoggerName 'app' -Level Warning -Message "Warning event"
    Write-KrLog -LoggerName 'app' -Level Error -Message "Error event"
    Write-KrLog -LoggerName 'app' -Level Fatal -Message "Fatal event"
    Write-KrTextResponse -InputObject "ok - $(Get-KrLevelSwitch -LoggerName 'app')" -StatusCode 200
}

# Change minimum level via route: /level/{level} e.g., /level/Debug or /level/Warning
Add-KrMapRoute -Verbs Get -Path "/level/{level}" -ScriptBlock {
    $lvl = Get-KrRequestRouteParam -Name 'level'
    try {
        $enum = [Serilog.Events.LogEventLevel]::$lvl
        Set-KrLevelSwitch -LoggerName 'app' -MinimumLevel $enum | Out-Null
        Write-KrLog -LoggerName 'app' -Level Information -Message "Level switch set to {level}" -Properties $lvl
        Write-KrTextResponse -InputObject "level=$lvl" -StatusCode 200
    } catch {
        Write-KrTextResponse -InputObject "invalid level '$lvl'" -StatusCode 400
    }
}

# Reconfigure 'app' logger at runtime: add a property and tighten minimum level
Add-KrMapRoute -Verbs Post -Path "/reconfigure" -ScriptBlock {
    New-KrLogger |
        Set-KrLoggerMinimumLevel -Value Warning |
        Add-KrEnrichProperty -Name 'Reconfigured' -Value $true |
        Add-KrSinkConsole |
        Add-KrSinkFile -Path '.\logs\hot-reload.log' -RollingInterval Hour |
        Register-KrLogger -Name 'app'
    Write-KrTextResponse -InputObject "reconfigured" -StatusCode 200
}

# Start the server
Start-KrServer

# Clean up and close the logger when the server stops
Close-KrLogger -Logger $app

