<#
    Sample demonstrating hot reload:
    - Dynamically change minimum level with a LoggingLevelSwitch
    - Re-register a logger with the same name to change sinks/properties
    FileName: 5.6-Hot-Reload.ps1
#>

$levelSwitch = New-KrLevelSwitch -MinimumLevel Information

$app = New-KrLogger |
    Set-KrMinimumLevel -Value Information -ControlledBy $levelSwitch |
    Add-KrSinkConsole |
    Add-KrSinkFile -Path '.\logs\hot-reload.log' -RollingInterval Hour |
    Register-KrLogger -Name 'app' -PassThru

New-KrServer -Name "Hot Reload"
Add-KrListener -Port 5005 -IPAddress ([IPAddress]::Loopback)
Add-KrPowerShellRuntime

Enable-KrConfiguration

# Writes both Debug and Information; visibility depends on level switch
Add-KrMapRoute -Verbs Get -Path "/log" -ScriptBlock {
    Write-KrLog -Logger $app -Level Debug -Message "Debug event"
    Write-KrLog -Logger $app -Level Information -Message "Information event"
    Write-KrTextResponse -InputObject "ok" -StatusCode 200
}

# Change minimum level via route: /level/{level} e.g., /level/Debug or /level/Warning
Add-KrMapRoute -Verbs Get -Path "/level/{level}" -ScriptBlock {
    param()
    $lvl = Get-KrRequestRouteParam -Name 'level'
    try {
        $enum = [Serilog.Events.LogEventLevel]::$lvl
        Set-KrLevelSwitch -LevelSwitch $levelSwitch -MinimumLevel $enum | Out-Null
        Write-KrLog -Logger $app -Level Information -Message "Level switch set to {level}" -Values $lvl
        Write-KrTextResponse -InputObject "level=$lvl" -StatusCode 200
    } catch {
        Write-KrTextResponse -InputObject "invalid level '$lvl'" -StatusCode 400
    }
}

# Reconfigure 'app' logger at runtime: add a property and tighten minimum level
Add-KrMapRoute -Verbs Post -Path "/reconfigure" -ScriptBlock {
    $newConfig = New-KrLogger |
        Set-KrMinimumLevel -Value Warning |
        Add-KrProperty -Name 'Reconfigured' -Value $true |
        Add-KrSinkConsole |
        Add-KrSinkFile -Path '.\logs\hot-reload.log' -RollingInterval Hour

    $script:app = $newConfig | Register-KrLogger -Name 'app' -PassThru
    Write-KrTextResponse -InputObject "reconfigured" -StatusCode 200
}

Start-KrServer
