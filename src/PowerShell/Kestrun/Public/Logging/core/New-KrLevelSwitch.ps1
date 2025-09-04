<#
    .SYNOPSIS
        Creates a new level switch for controlling the minimum logging level.
    .DESCRIPTION
        Creates a new instance of the LoggingLevelSwitch class, which is used to control the minimum logging level for a logger.
    .PARAMETER MinimumLevel
        The minimum logging level for the switch. Default is Information.
    .PARAMETER ToPreference
        If specified, sets the minimum level to the user's preference.
    .INPUTS
        None. You cannot pipe objects to New-KrLevelSwitch.
    .OUTPUTS
        Instance of Serilog.Core.LoggingLevelSwitch.
    .EXAMPLE
        PS> $levelSwitch = New-KrLevelSwitch -MinimumLevel Warning
        Creates a new level switch with the minimum level set to Warning.
    .EXAMPLE
        PS> $levelSwitch = New-KrLevelSwitch -MinimumLevel Debug -ToPreference
        Creates a new level switch with the minimum level set to Debug and updates the user's logging preference.
#>
function New-KrLevelSwitch {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([Serilog.Core.LoggingLevelSwitch])]
    param(
        [Parameter(Mandatory = $false)]
        [Serilog.Events.LogEventLevel]$MinimumLevel = [Serilog.Events.LogEventLevel]::Information,
        [Parameter(Mandatory = $false)]
        [switch]$ToPreference
    )

    $levelSwitch = [Serilog.Core.LoggingLevelSwitch]::new()
    $levelSwitch.MinimumLevel = $MinimumLevel

    # If ToPreference is specified, set the minimum level to the user's preference
    if ($ToPreference) {
        Set-KrLogLevelToPreference -LogLevel $MinimumLevel
    }

    return $levelSwitch
}

