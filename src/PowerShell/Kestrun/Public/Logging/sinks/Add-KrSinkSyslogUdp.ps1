<#
    .SYNOPSIS
        Adds a Syslog UDP sink to the Serilog logger configuration.
    .DESCRIPTION
        The Add-KrSinkSyslogUdp function configures a logging sink that sends log events to a Syslog server over UDP.
        It allows customization of the Syslog server hostname, port, application name, format, facility, output template, and minimum log level.
    .PARAMETER LoggerConfig
        The Serilog LoggerConfiguration object to which the Syslog UDP sink will be added.
    .PARAMETER Hostname
        The hostname or IP address of the Syslog server to which log events will be sent.
    .PARAMETER Port
        The port number on which the Syslog server is listening. Defaults to 514.
    .PARAMETER AppName
        The application name to be included in the Syslog messages. If not specified, defaults to null.
    .PARAMETER Format
        The Syslog message format to use. Defaults to RFC3164.
    .PARAMETER Facility
        The Syslog facility to use for the log messages. Defaults to Local0.
    .PARAMETER OutputTemplate
        The output template string for formatting log messages. Defaults to '{Message}{NewLine}{Exception}{ErrorRecord}'.
    .PARAMETER RestrictedToMinimumLevel
        The minimum log event level required to write to the Syslog sink. Defaults to Verbose.
    .EXAMPLE
        Add-KrSinkSyslogUdp -LoggerConfig $config -Hostname "syslog.example.com"
        Adds a Syslog UDP sink to the logging system that sends log events to "syslog.example.com" on the default port 514.
    .EXAMPLE
        Add-KrSinkSyslogUdp -LoggerConfig $config -Hostname "syslog.example.com" -Port 514 -AppName "MyApp"
        Adds a Syslog UDP sink that sends log events to "syslog.example.com" with the application name "MyApp".
    .EXAMPLE
        Add-KrSinkSyslogUdp -LoggerConfig $config -Hostname "syslog.example.com" -Port 514 -Format RFC5424 -Facility Local1
        Adds a Syslog UDP sink that sends log events to "syslog.example.com" with the RFC5424 format and Local1 facility.
    .NOTES
        This function is part of the Kestrun logging infrastructure and should be used to enable Syslog UDP logging.
#>
function Add-KrSinkSyslogUdp {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Serilog.LoggerConfiguration])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$LoggerConfig,

        [Parameter(Mandatory = $true)]
        [string]$Hostname,

        [Parameter(Mandatory = $false)]
        [int]$Port = 514,

        [Parameter(Mandatory = $false)]
        [string]$AppName = $null,

        [Parameter(Mandatory = $false)]
        [Serilog.Sinks.Syslog.SyslogFormat]$Format = [Serilog.Sinks.Syslog.SyslogFormat]::RFC3164,

        [Parameter(Mandatory = $false)]
        [Serilog.Sinks.Syslog.Facility]$Facility = [Serilog.Sinks.Syslog.Facility]::Local0,

        [Parameter(Mandatory = $false)]
        [string]$OutputTemplate = '{Message}{NewLine}{Exception}{ErrorRecord}',

        [Parameter(Mandatory = $false)]
        [Serilog.Events.LogEventLevel]$RestrictedToMinimumLevel = [Serilog.Events.LogEventLevel]::Verbose
    )

    process {
        return [Serilog.SyslogLoggerConfigurationExtensions]::UdpSyslog($LoggerConfig.WriteTo,
            $Hostname,
            $Port,
            $AppName,
            $Format,
            $Facility,
            $OutputTemplate,
            $RestrictedToMinimumLevel
        )
    }
}

