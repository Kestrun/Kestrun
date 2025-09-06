<#
    .SYNOPSIS
        Adds a Syslog TCP sink to the logging system.
    .DESCRIPTION
        The Add-KrSinkSyslogTcp function configures a logging sink that sends log events to a Syslog server over TCP.
        It allows customization of the Syslog server hostname, port, application name, format, facility, output template, and minimum log level.
    .PARAMETER LoggerConfig
        The Serilog LoggerConfiguration object to which the Syslog TCP sink will be added.
    .PARAMETER Hostname
        The hostname or IP address of the Syslog server to which log events will be sent.
    .PARAMETER Port
        The port number on which the Syslog server is listening. Defaults to 1468.
    .PARAMETER AppName
        The application name to be included in the Syslog messages. If not specified, defaults to null.
    .PARAMETER FramingType
        The framing type to use for the Syslog messages. Defaults to OCTET_COUNTING.
    .PARAMETER Format
        The Syslog message format to use. Defaults to RFC5424.
    .PARAMETER Facility
        The Syslog facility to use for the log messages. Defaults to Local0.
    .PARAMETER SecureProtocols
        The SSL/TLS protocols to use for secure connections. Defaults to Tls12.
    .PARAMETER CertProvider
        An optional certificate provider for secure connections.
    .PARAMETER CertValidationCallback
        An optional callback for validating server certificates.
    .PARAMETER OutputTemplate
        The output template string for formatting log messages. Defaults to '{Message}{NewLine}{Exception}{ErrorRecord}'.
    .PARAMETER RestrictedToMinimumLevel
        The minimum log event level required to write to the Syslog sink. Defaults to Verbose.
    .EXAMPLE
        Add-KrSinkSyslogTcp -LoggerConfig $config -Hostname "syslog.example.com"
        Adds a Syslog TCP sink to the logging system that sends log events to "syslog.example.com" on the default port 1468.
    .EXAMPLE
        Add-KrSinkSyslogTcp -LoggerConfig $config -Hostname "syslog.example.com" -Port 1468 -AppName "MyApp"
        Adds a Syslog TCP sink that sends log events to "syslog.example.com" with the application name "MyApp".
    .EXAMPLE
        Add-KrSinkSyslogTcp -LoggerConfig $config -Hostname "syslog.example.com" -Port 1468 -Format RFC5424 -Facility Local1
        Adds a Syslog TCP sink that sends log events to "syslog.example.com" with the RFC5424 format and Local1 facility.
    .NOTES
        This function is part of the Kestrun logging infrastructure and should be used to enable Syslog TCP logging.
    #>
function Add-KrSinkSyslogTcp {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Serilog.LoggerConfiguration])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$LoggerConfig,

        [Parameter(Mandatory = $true)]
        [string]$Hostname,

        [Parameter(Mandatory = $false)]
        [int]$Port = 1468,

        [Parameter(Mandatory = $false)]
        [string]$AppName = $null,

        [Parameter(Mandatory = $false)]
        [Serilog.Sinks.Syslog.FramingType]$FramingType = [Serilog.Sinks.Syslog.FramingType]::OCTET_COUNTING,

        [Parameter(Mandatory = $false)]
        [Serilog.Sinks.Syslog.SyslogFormat]$Format = [Serilog.Sinks.Syslog.SyslogFormat]::RFC5424,

        [Parameter(Mandatory = $false)]
        [Serilog.Sinks.Syslog.Facility]$Facility = [Serilog.Sinks.Syslog.Facility]::Local0,

        [Parameter(Mandatory = $false)]
        [System.Security.Authentication.SslProtocols]$SecureProtocols = [System.Security.Authentication.SslProtocols]::Tls12,

        [Parameter(Mandatory = $false)]
        [Serilog.Sinks.Syslog.ICertificateProvider]$CertProvider = $null,

        [Parameter(Mandatory = $false)]
        [System.Net.Security.RemoteCertificateValidationCallback]$CertValidationCallback = $null,

        [Parameter(Mandatory = $false)]
        [string]$OutputTemplate = '{Message}{NewLine}{Exception}{ErrorRecord}',

        [Parameter(Mandatory = $false)]
        [Serilog.Events.LogEventLevel]$RestrictedToMinimumLevel = [Serilog.Events.LogEventLevel]::Verbose
    )

    process {
        return [Serilog.SyslogLoggerConfigurationExtensions]::TcpSyslog($LoggerConfig.WriteTo,
            $Hostname,
            $Port,
            $AppName,
            $FramingType,
            $Format,
            $Facility,
            $SecureProtocols,
            $CertProvider,
            $CertValidationCallback,
            $OutputTemplate,
            $RestrictedToMinimumLevel
        )
    }
}

