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
    .EXAMPLE
        Add-KrSinkSyslogLocal -LoggerConfig $config -AppName "MyApp" -Facility Local1 -OutputTemplate "{Message}{NewLine}{Exception}{ErrorRecord}" -RestrictedToMinimumLevel Information
        Adds a Syslog Local sink to the logging system that sends log events with the specified application name, facility, output template, and minimum log level.
    .EXAMPLE
        Add-KrSinkSyslogLocal -LoggerConfig $config
        Adds a Syslog Local sink to the logging system with default parameters.
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
        [string]$OutputTemplate = '{Message}{NewLine}{Exception}{ErrorRecord}',

        [Parameter(Mandatory = $false)]
        [Serilog.Events.LogEventLevel]$RestrictedToMinimumLevel = [Serilog.Events.LogEventLevel]::Verbose
    )

    process {
        $LoggerConfig = [Serilog.SyslogLoggerConfigurationExtensions]::LocalSyslog($LoggerConfig.WriteTo,
            $AppName,
            $Facility,
            $OutputTemplate,
            $RestrictedToMinimumLevel
        )

        return $LoggerConfig
    }
}
