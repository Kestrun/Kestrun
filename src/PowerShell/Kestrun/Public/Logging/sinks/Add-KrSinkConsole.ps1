<#
    .SYNOPSIS
        Adds a console logging sink to the Kestrun logging system.
    .DESCRIPTION
        The Add-KrSinkConsole function configures and adds a console output sink for logging messages within the Kestrun framework. This enables log messages to be displayed directly in the PowerShell console.
    .PARAMETER LoggerConfig
        The Serilog LoggerConfiguration object to which the console sink will be added.
    .PARAMETER RestrictedToMinimumLevel
        The minimum log event level required to write to the console sink. Defaults to Verbose.
    .PARAMETER OutputTemplate
        The output template string for formatting log messages. Defaults to '[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{ErrorRecord}{Exception}'.
    .PARAMETER FormatProvider
        An optional format provider for customizing message formatting.
    .PARAMETER LevelSwitch
        An optional LoggingLevelSwitch to dynamically control the logging level.
    .PARAMETER StandardErrorFromLevel
        An optional log event level at which messages are written to standard error.
    .PARAMETER Theme
        An optional console theme for customizing log output appearance.
    .PARAMETER Formatter
        An optional text formatter for custom log message formatting (used in 'Formatter' parameter set).
    .EXAMPLE
        Add-KrSinkConsole -LoggerConfig $config
        Adds a console sink to the logging system, allowing log messages to be output to the console.
    .EXAMPLE
        Add-KrSinkConsole -LoggerConfig $config -RestrictedToMinimumLevel Information
        Adds a console sink that only outputs log events at Information level or higher.
    .EXAMPLE
        Add-KrSinkConsole -LoggerConfig $config -OutputTemplate '[{Level}] {Message}{NewLine}'
        Customizes the output template for console log messages.
    .EXAMPLE
        Add-KrSinkConsole -LoggerConfig $config -Formatter $customFormatter
        Uses a custom text formatter for console log output.
    .NOTES
        This function is part of the Kestrun logging infrastructure and should be used to enable console logging.
#>
function Add-KrSinkConsole {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'Default')]
    [OutputType([Serilog.LoggerConfiguration])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$LoggerConfig,
        [Parameter(Mandatory = $false)]
        [Serilog.Events.LogEventLevel]$RestrictedToMinimumLevel = [Serilog.Events.LogEventLevel]::Verbose,
        [Parameter(Mandatory = $false)]
        [string]$OutputTemplate = '[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{ErrorRecord}{Exception}',
        [Parameter(Mandatory = $false)]
        [System.IFormatProvider]$FormatProvider = $null,
        [Parameter(Mandatory = $false)]
        [Serilog.Core.LoggingLevelSwitch]$LevelSwitch = $null,
        [Parameter(Mandatory = $false)]
        [Nullable[Serilog.Events.LogEventLevel]]$StandardErrorFromLevel = $null,
        [Parameter(Mandatory = $false)]
        [Serilog.Sinks.SystemConsole.Themes.ConsoleTheme]$Theme,
        [Parameter(Mandatory = $false, ParameterSetName = 'Formatter')]
        [Serilog.Formatting.ITextFormatter]$Formatter

    )

    process {
        switch ($PSCmdlet.ParameterSetName) {
            'Default' {
                $LoggerConfig = [Serilog.ConsoleLoggerConfigurationExtensions]::Console($LoggerConfig.WriteTo,
                    $RestrictedToMinimumLevel,
                    $OutputTemplate,
                    $FormatProvider,
                    $LevelSwitch,
                    $StandardErrorFromLevel,
                    $Theme
                )
            }
            'Formatter' {
                $LoggerConfig = [Serilog.ConsoleLoggerConfigurationExtensions]::Console($LoggerConfig.WriteTo,
                    $Formatter,
                    $RestrictedToMinimumLevel,
                    $LevelSwitch,
                    $StandardErrorFromLevel
                )
            }
        }

        return $LoggerConfig
    }
}
