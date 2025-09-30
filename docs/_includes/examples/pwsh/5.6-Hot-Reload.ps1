<#
    Sample demonstrating hot reload:
    - Dynamically change minimum level with a LoggingLevelSwitch
    - Re-register a logger with the same name to change sinks/properties
    FileName: 5.6-Hot-Reload.ps1
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Dynamic level switch logger
New-KrLogger |
    Set-KrLoggerMinimumLevel -Dynamic Debug |
    Add-KrSinkConsole |
    Add-KrSinkFile -Path '.\logs\hot-reload.log' -RollingInterval Hour |
    Register-KrLogger -Name 'app' -PassThru

# Default logger for server events
New-KrLogger |
    Set-KrLoggerMinimumLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'Default'

New-KrServer -Name "Hot Reload" -LoggerName 'Default'
Add-KrEndpoint -Port $Port -IPAddress $IPAddress
Add-KrPowerShellRuntime

Enable-KrConfiguration
# Writes both Debug and Information; visibility depends on level switch
Add-KrMapRoute -Verbs Get -Path "/log" -ScriptBlock {
    Write-KrLog -LoggerName 'app' -Level Debug -Message "Debug event"
    Write-KrLog -LoggerName 'app' -Level Information -Message "Information event"
    Write-KrLog -LoggerName 'app' -Level Warning -Message "Warning event"
    Write-KrLog -LoggerName 'app' -Level Error -Message "Error event"
    Write-KrLog -LoggerName 'app' -Level Fatal -Message "Fatal event"
    Write-KrTextResponse -InputObject "ok - $(Get-KrLoggerLevelSwitch -LoggerName 'app')" -StatusCode 200
}

# Change minimum level via route: /level/{level} e.g., /level/Debug or /level/Warning
Add-KrMapRoute -Verbs Get -Path "/level/{level}" -ScriptBlock {
    $lvl = Get-KrRequestRouteParam -Name 'level'
    $parsed = [Serilog.Events.LogEventLevel]::Information
    if ([Enum]::TryParse([Serilog.Events.LogEventLevel], $lvl, $true, [ref]$parsed)) {
        Write-KrLog -Level Debug -LoggerName 'Default' -Message "Changing level to {lvl}" -Values $lvl
        Set-KrLoggerLevelSwitch -LoggerName 'app' -MinimumLevel $parsed | Out-Null
        Write-KrLog -LoggerName 'app' -Level Information -Message "Level switch set to {level}" -Values $lvl
        Write-KrTextResponse -InputObject "level=$lvl" -StatusCode 200
    } else {
        Write-KrLog -LoggerName 'app' -Level Warning -Message "Invalid level value {level}" -Values $lvl
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
Close-KrLogger

