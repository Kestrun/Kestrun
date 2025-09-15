<#
    .SYNOPSIS
        Gets the current logging level for a level switch.
    .DESCRIPTION
        Retrieves the current logging level for a specified level switch. If the LoggerName is not provided,
        it will be derived from the provided Logger instance.
    .PARAMETER Logger
        An instance of Serilog.Core.Logger to set the level switch for.
        It's mutually exclusive with the LoggerName parameter.
    .PARAMETER LoggerName
        The name of a registered logger to set the level switch for.
        It's mutually exclusive with the Logger parameter.
    .EXAMPLE
        PS> Get-KrLoggerLevelSwitch -LoggerName "MyLogger"
        Retrieves the current logging level of the level switch for the logger named "MyLogger".
    .EXAMPLE
        PS> Get-KrLoggerLevelSwitch -Logger $myLogger
        Retrieves the current logging level of the level switch for the specified logger instance.
#>
function Get-KrLoggerLevelSwitch {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding(DefaultParameterSetName = 'LoggerName')]
    [OutputType([Serilog.Events.LogEventLevel])]
    param(
        [Parameter(Mandatory = $false, ParameterSetName = 'LoggerName')]
        [string]$LoggerName,
        [Parameter(Mandatory = $true, ParameterSetName = 'Logger')]
        [Serilog.Core.Logger]$Logger
    )

    if ([string]::IsNullOrEmpty($LoggerName)) {
        $LoggerName = [Kestrun.Logging.LoggerManager]::GetName($Logger)
    }
    if ([string]::IsNullOrEmpty($LoggerName)) {
        throw [System.ArgumentException]::new("LoggerName cannot be null or empty.")
    }

    $levelSwitch = [Kestrun.Logging.LoggerManager]::GetLevelSwitch($LoggerName)
    if ($null -eq $levelSwitch) {
        throw [System.InvalidOperationException]::new("Level switch not found for logger '$LoggerName'. Ensure that the logger is configured with a level switch.")
    }
    return $levelSwitch.MinimumLevel
}

