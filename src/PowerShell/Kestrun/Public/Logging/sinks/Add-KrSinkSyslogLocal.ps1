<#
    .SYNOPSIS
        Adds a Syslog Local sink to the Serilog logger configuration.
    .DESCRIPTION
        The Add-KrSinkSyslogLocal function configures a logging sink that sends log events to the local Syslog server.
        It allows customization of the application name, Syslog facility, output template, and minimum log level.
    .PARAMETER LoggerConfig
        The Serilog LoggerConfiguration object to which the Syslog Local sink will be added.
    .PARAMETER AppName
        The application name to be included in the Syslog messages. If not specified, defaults to null.
    .PARAMETER Facility
        The Syslog facility to use for the log messages. Defaults to Local0.
    .PARAMETER OutputTemplate
        The output template string for formatting log messages. Defaults to '{Message}{NewLine}{Exception}{ErrorRecord}'.
    .PARAMETER RestrictedToMinimumLevel
        The minimum log event level required to write to the Syslog sink. Defaults to Verbose.
    .PARAMETER SeverityMapping
        An optional function to map Serilog log levels to Syslog severity levels.
    .PARAMETER Formatter
        An optional ITextFormatter for custom message formatting.
    .PARAMETER LevelSwitch
        An optional LoggingLevelSwitch to dynamically control the minimum log level.
    .EXAMPLE
        Add-KrSinkSyslogLocal -LoggerConfig $config -AppName "MyApp" -Facility Local1 -OutputTemplate "{Message}{NewLine}{Exception}{ErrorRecord}" -RestrictedToMinimumLevel Information
        Adds a Syslog Local sink to the logging system that sends log events with the specified application name, facility, output template, and minimum log level.
    .EXAMPLE
        Add-KrSinkSyslogLocal -LoggerConfig $config
        Adds a Syslog Local sink to the logging system with default parameters.
    .EXAMPLE
        Add-KrSinkSyslogLocal -LoggerConfig $config -AppName "MyApp" -SeverityMapping { param($level) if ($level -eq 'Error') { return 'Alert' } else { return 'Info' } }
        Adds a Syslog Local sink with a custom severity mapping function.
    .EXAMPLE
        Add-KrSinkSyslogLocal -LoggerConfig $config -LevelSwitch $levelSwitch
        Adds a Syslog Local sink with a dynamic level switch to control the minimum log level.
    .NOTES
        This function is part of the Kestrun logging infrastructure and should be used to enable Syslog Local logging.
#>
function Add-KrSinkSyslogLocal {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Serilog.LoggerConfiguration])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$LoggerConfig,

        [Parameter(Mandatory = $false)]
        [string]$AppName = $null,

        [Parameter(Mandatory = $false)]
        [Serilog.Sinks.Syslog.Facility]$Facility = [Serilog.Sinks.Syslog.Facility]::Local0,

        [Parameter(Mandatory = $false)]
        [string]$OutputTemplate,

        [Parameter(Mandatory = $false)]
        [Serilog.Events.LogEventLevel]$RestrictedToMinimumLevel = [Serilog.Events.LevelAlias]::Minimum,
        [System.Func``2[Serilog.Events.LogEventLevel, Serilog.Sinks.Syslog.Severity]]$SeverityMapping = $null,
        [Serilog.Formatting.ITextFormatter]$Formatter = $null,
        [Serilog.Core.LoggingLevelSwitch]$LevelSwitch = $null
    )

    process {
        return [Serilog.SyslogLoggerConfigurationExtensions]::LocalSyslog(
            $LoggerConfig.WriteTo,       # 1 loggerSinkConfig
            $AppName,                    # 2 appName
            $Facility,                   # 3 facility
            $OutputTemplate,             # 4 outputTemplate
            $RestrictedToMinimumLevel,   # 5 restrictedToMinimumLevel
            $SeverityMapping,            # 6 severityMapping
            $Formatter,                  # 7 formatter
            $LevelSwitch                 # 8 levelSwitch
        )
    }
}
