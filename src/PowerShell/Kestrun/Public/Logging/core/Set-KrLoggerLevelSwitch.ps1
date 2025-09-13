<#
    .SYNOPSIS
        Sets the minimum logging level for a level switch.
    .DESCRIPTION
        Sets the minimum logging level for a specified level switch. If ToPreference is specified,
        the logging level will be set to the user's preference.
    .PARAMETER Logger
        An instance of Serilog.Core.Logger to set the level switch for.
        It's mutually exclusive with the LoggerName parameter.
    .PARAMETER LoggerName
        The name of a registered logger to set the level switch for.
        It's mutually exclusive with the Logger parameter.
    .PARAMETER MinimumLevel
        The minimum logging level to set for the switch.
    .EXAMPLE
        PS> Set-KrLoggerLevelSwitch -LoggerName "MyLogger" -MinimumLevel Warning
        Sets the minimum logging level of the level switch for the logger named "MyLogger" to Warning.
    .EXAMPLE
        PS> Set-KrLoggerLevelSwitch -Logger $myLogger -MinimumLevel Error
        Sets the minimum logging level of the level switch for the specified logger instance to Error.
#>
function Set-KrLoggerLevelSwitch {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding(DefaultParameterSetName = 'LoggerName')]
    param(
        [Parameter(Mandatory = $false, ParameterSetName = 'LoggerName')]
        [string]$LoggerName,
        [Parameter(Mandatory = $true, ParameterSetName = 'Logger')]
        [Serilog.Core.Logger]$Logger,
        [Parameter(Mandatory = $true)]
        [Serilog.Events.LogEventLevel]$MinimumLevel
    )

    if ([string]::IsNullOrEmpty($LoggerName)) {
        $LoggerName = [Kestrun.Logging.LoggerManager]::GetName($Logger)
    }
    if ([string]::IsNullOrEmpty($LoggerName)) {
        throw [System.ArgumentException]::new("LoggerName cannot be null or empty.")
    }
    [Kestrun.Logging.LoggerManager]::SetLevelSwitch($LoggerName, $MinimumLevel)
}

