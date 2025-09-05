<#
    .SYNOPSIS
        Sets the minimum logging level for a level switch.
    .DESCRIPTION
        Sets the minimum logging level for a specified level switch. If ToPreference is specified,
        the logging level will be set to the user's preference.
    .PARAMETER LevelSwitch
        Instance of Serilog.Core.LoggingLevelSwitch to set the minimum level for.
    .PARAMETER MinimumLevel
        The minimum logging level to set for the switch.
    .PARAMETER ToPreference
        If specified, sets the minimum level to the user's preference.
    .PARAMETER PassThru
        If specified, outputs the LevelSwitch object into the pipeline.
    .INPUTS
        Instance of Serilog.Core.LoggingLevelSwitch
    .OUTPUTS
        Instance of Serilog.Core.LoggingLevelSwitch if PassThru is specified.
    .EXAMPLE
        PS> Set-KrLevelSwitch -LevelSwitch $myLevelSwitch -MinimumLevel Warning
        Sets the minimum logging level of the specified level switch to Warning.
    .EXAMPLE
        PS> Set-KrLevelSwitch -LevelSwitch $myLevelSwitch -MinimumLevel Debug -ToPreference
        Sets the minimum logging level of the specified level switch to Debug and updates the user's logging preferences.
    .EXAMPLE
        PS> $levelSwitch = Set-KrLevelSwitch -LevelSwitch $myLevelSwitch -MinimumLevel Information -PassThru
        Sets the minimum logging level of the specified level switch to Information and outputs the LevelSwitch object into the pipeline.
#>
function Set-KrLevelSwitch {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([Serilog.Core.LoggingLevelSwitch])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.Core.LoggingLevelSwitch]$LevelSwitch,
        [Parameter(Mandatory = $true)]
        [Serilog.Events.LogEventLevel]$MinimumLevel,
        [Parameter(Mandatory = $false)]
        [switch]$ToPreference,
        [Parameter(Mandatory = $false)]
        [switch]$PassThru
    )

    process {
        $LevelSwitch.MinimumLevel = $MinimumLevel

        if ($ToPreference) {
            Set-KrLogLevelToPreference -Level $MinimumLevel
        }
        if ($ToPreference) {
            Set-KrLogLevelToPreference -Level $MinimumLevel
        }

        if ($PassThru) {
            return $LevelSwitch
        }
    }
}

