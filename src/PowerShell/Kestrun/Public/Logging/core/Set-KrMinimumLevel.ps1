<#
    .SYNOPSIS
        Sets the minimum log level for the logger configuration.
    .DESCRIPTION
        Sets the minimum log level for the logger configuration. This cmdlet can be used to
        set the minimum log level to a specific level or to the user's preference.
    .PARAMETER LoggerConfig
        Instance of Serilog.LoggerConfiguration to set the minimum level for.
    .PARAMETER Value
        The minimum log level to set for the logger configuration.
    .PARAMETER ToPreference
        If specified, sets the minimum level to the user's preference.
    .PARAMETER ControlledBy
        Instance of Serilog.Core.LoggingLevelSwitch to control the minimum level.
    .PARAMETER FromPreference
        If specified, sets the minimum level from the user's preference.
    .INPUTS
        Instance of Serilog.LoggerConfiguration
    .OUTPUTS
        Instance of Serilog.LoggerConfiguration if the PassThru parameter is specified.
    .EXAMPLE
        PS> Set-KrMinimumLevel -LoggerConfig $myLoggerConfig -Value Warning
        Sets the minimum log level of the specified logger configuration to Warning.
    .EXAMPLE
        PS> Set-KrMinimumLevel -LoggerConfig $myLoggerConfig -Value Debug -ToPreference
        Sets the minimum log level of the specified logger configuration to Debug and updates the user's logging preferences.
    .EXAMPLE
        PS> Set-KrMinimumLevel -LoggerConfig $myLoggerConfig -ControlledBy $myLevelSwitch
        Sets the minimum log level of the specified logger configuration to be controlled by the specified level switch.
    .EXAMPLE
        PS> Set-KrMinimumLevel -LoggerConfig $myLoggerConfig -FromPreference
        Sets the minimum log level of the specified logger configuration from the user's logging preferences.
    .EXAMPLE
        PS> $myLoggerConfig | Set-KrMinimumLevel -Value Information -PassThru
        Sets the minimum log level of the specified logger configuration to Information and outputs the LoggerConfiguration object into the pipeline.
#>
function Set-KrMinimumLevel {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$LoggerConfig,
        [Parameter(Mandatory = $true, ParameterSetName = 'Level')]
        [Serilog.Events.LogEventLevel]$Value,
        [Parameter(Mandatory = $false, ParameterSetName = 'Level')]
        [switch]$ToPreference,
        [Parameter(Mandatory = $true, ParameterSetName = 'Switch')]
        [Serilog.Core.LoggingLevelSwitch]$ControlledBy,
        [Parameter(Mandatory = $true, ParameterSetName = 'Preference')]
        [switch]$FromPreference
    )

    process {
        switch ($PsCmdlet.ParameterSetName) {
            'Level' {
                switch ($Value) {
                    Verbose {
                        $LoggerConfig.MinimumLevel.Verbose()
                    }
                    Debug {
                        $LoggerConfig.MinimumLevel.Debug()
                    }
                    Information {
                        $LoggerConfig.MinimumLevel.Information()
                    }
                    Warning {
                        $LoggerConfig.MinimumLevel.Warning()
                    }
                    Error { $LoggerConfig.MinimumLevel.Error() }
                    Fatal { $LoggerConfig.MinimumLevel.Fatal() }
                    default { $LoggerConfig.MinimumLevel.Information() }
                }

                if ($ToPreference) {
                    Set-KrLogLevelToPreference -Level $Value
                }
            }
            'Switch' {
                $LoggerConfig.MinimumLevel.ControlledBy($ControlledBy)
            }
            'Preference' {
                if ($FromPreference) {
                    if ($VerbosePreference -eq 'Continue') {
                        $LoggerConfig | Set-KrMinimumLevel -Value Verbose
                    } elseif ($DebugPreference -eq 'Continue') {
                        $LoggerConfig | Set-KrMinimumLevel -Value Debug
                    } elseif ($InformationPreference -eq 'Continue') {
                        $LoggerConfig | Set-KrMinimumLevel -Value Information
                    } elseif ($WarningPreference -eq 'Continue') {
                        $LoggerConfig | Set-KrMinimumLevel -Value Warning
                    } else {
                        $LoggerConfig | Set-KrMinimumLevel -Value Error
                    }
                }
            }
        }
    }
}

