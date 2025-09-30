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
    .PARAMETER Dynamic
        If specified, the minimum log level will be controlled by a level switch.
    .INPUTS
        Instance of Serilog.LoggerConfiguration
    .OUTPUTS
        Instance of Serilog.LoggerConfiguration if the PassThru parameter is specified.
    .EXAMPLE
        PS> Set-KrLoggerMinimumLevel -LoggerConfig $myLoggerConfig -Values Warning
        Sets the minimum log level of the specified logger configuration to Warning.
        .EXAMPLE
        PS> Set-KrLoggerMinimumLevel -LoggerConfig $myLoggerConfig -ControlledBy $myLevelSwitch
        Sets the minimum log level of the specified logger configuration to be controlled by the specified level switch.
    .EXAMPLE
        PS> $myLoggerConfig | Set-KrLoggerMinimumLevel -Value Information -PassThru
        Sets the minimum log level of the specified logger configuration to Information and outputs the LoggerConfiguration object into the pipeline.
#>
function Set-KrLoggerMinimumLevel {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [OutputType([Serilog.LoggerConfiguration])]
    [CmdletBinding(DefaultParameterSetName = 'Static')]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$LoggerConfig,
        [Parameter(Mandatory = $true, ParameterSetName = 'Static')]
        [Serilog.Events.LogEventLevel]$Value,
        [Parameter(Mandatory = $true, ParameterSetName = 'Dynamic')]
        [Serilog.Events.LogEventLevel]$Dynamic
    )

    process {
        if ($PsCmdlet.ParameterSetName -eq 'Dynamic') {
            return [Kestrun.Logging.LoggerConfigurationExtensions]::EnsureSwitch($LoggerConfig, $Dynamic)
        } else {
            switch ($Value) {
                Verbose { return $LoggerConfig.MinimumLevel.Verbose() }
                Debug { return $LoggerConfig.MinimumLevel.Debug() }
                Information { return $LoggerConfig.MinimumLevel.Information() }
                Warning { return $LoggerConfig.MinimumLevel.Warning() }
                Error { return $LoggerConfig.MinimumLevel.Error() }
                Fatal { return $LoggerConfig.MinimumLevel.Fatal() }
                default { return $LoggerConfig.MinimumLevel.Information() }
            }
        }
    }
}

