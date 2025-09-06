<#
.SYNOPSIS
    Adds a Syslog TCP sink to the logging system.

.DESCRIPTION
    Configures a Serilog sink that sends log events to a Syslog server over TCP.
    Supports hostname, port, app name, framing, format, facility, TLS, certificate options,
    output template, minimum level, batching, severity mapping, and advanced syslog parameters.

.PARAMETER LoggerConfig
    The Serilog LoggerConfiguration object to which the Syslog TCP sink will be added.

.PARAMETER Hostname
    The hostname or IP address of the Syslog server to which log events will be sent.

.PARAMETER Port
    The port number on which the Syslog server is listening. Defaults to 514.

.PARAMETER AppName
    The application name to be included in the Syslog messages. If not specified, defaults to null.

.PARAMETER FramingType
    The framing type to use for the Syslog messages. Defaults to OCTET_COUNTING.

.PARAMETER Format
    The Syslog message format to use. Defaults to RFC5424.

.PARAMETER Facility
    The Syslog facility to use for the log messages. Defaults to Local0.

.PARAMETER UseTls
    Switch to enable TLS encryption for the TCP connection. Defaults to false.

.PARAMETER CertProvider
    An optional certificate provider for secure connections.

.PARAMETER CertValidationCallback
    An optional callback for validating server certificates.

.PARAMETER OutputTemplate
    The output template string for formatting log messages.

.PARAMETER RestrictedToMinimumLevel
    The minimum log event level required to write to the Syslog sink. Defaults to Verbose.

.PARAMETER MessageIdPropertyName
    The property name used for RFC5424 message ID. Defaults to the sink’s built-in constant.

.PARAMETER BatchSizeLimit
    Maximum number of events per batch (optional).

.PARAMETER PeriodSeconds
    Flush period for batches in seconds (optional).

.PARAMETER QueueLimit
    Maximum number of buffered events (optional).

.PARAMETER EagerlyEmitFirstEvent
    If specified, the first event is sent immediately without waiting for the batch period.

.PARAMETER SourceHost
    Optional value for the `sourceHost` field in syslog messages.

.PARAMETER SeverityMapping
    Custom delegate to map Serilog log levels to syslog severities.

.PARAMETER Formatter
    Optional custom ITextFormatter for full control over message formatting.

.PARAMETER LevelSwitch
    Optional LoggingLevelSwitch to dynamically control the log level.

.EXAMPLE
    # simplest: send logs over tcp with defaults
    Add-KrSinkSyslogTcp -LoggerConfig $config -Hostname "syslog.example.com"

.EXAMPLE
    # custom port, app name, and TLS enabled
    Add-KrSinkSyslogTcp -LoggerConfig $config -Hostname "syslog.example.com" -Port 6514 -AppName "MyApp" -UseTls

.EXAMPLE
    # use RFC3164 format and Local1 facility
    Add-KrSinkSyslogTcp -LoggerConfig $config -Hostname "syslog.example.com" -Format RFC3164 -Facility Local1

.EXAMPLE
    # add batching configuration
    Add-KrSinkSyslogTcp -LoggerConfig $config -Hostname "syslog.example.com" `
        -BatchSizeLimit 50 -PeriodSeconds 1 -QueueLimit 5000 -EagerlyEmitFirstEvent

.EXAMPLE
    # apply a custom severity mapping
    $map = [System.Func[Serilog.Events.LogEventLevel,Serilog.Sinks.Syslog.Severity]]{
        param($level)
        switch ($level) {
            'Information' { [Serilog.Sinks.Syslog.Severity]::Notice }
            'Fatal'       { [Serilog.Sinks.Syslog.Severity]::Emergency }
            default       { [Serilog.Sinks.Syslog.Severity]::Informational }
        }
    }
    Add-KrSinkSyslogTcp -LoggerConfig $config -Hostname "syslog.example.com" -SeverityMapping $map

.EXAMPLE
    # advanced: secure connection with certificate validation
    $callback = [System.Net.Security.RemoteCertificateValidationCallback]{
        param($sender, $cert, $chain, $errors) $true
    }
    Add-KrSinkSyslogTcp -LoggerConfig $config -Hostname "syslog.example.com" -UseTls `
        -CertValidationCallback $callback -AppName "SecureApp"

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

        [int]$Port = 514,
        [string]$AppName = $null,

        [Serilog.Sinks.Syslog.FramingType]$FramingType = [Serilog.Sinks.Syslog.FramingType]::OCTET_COUNTING,
        [Serilog.Sinks.Syslog.SyslogFormat]$Format = [Serilog.Sinks.Syslog.SyslogFormat]::RFC5424,
        [Serilog.Sinks.Syslog.Facility]$Facility = [Serilog.Sinks.Syslog.Facility]::Local0,

        [switch]$UseTls,

        [Serilog.Sinks.Syslog.ICertificateProvider]$CertProvider = $null,
        [System.Net.Security.RemoteCertificateValidationCallback]$CertValidationCallback = $null,

        [string]$OutputTemplate,
        [Serilog.Events.LogEventLevel]$RestrictedToMinimumLevel = [Serilog.Events.LogEventLevel]::Verbose,

        [string]$MessageIdPropertyName = [Serilog.Sinks.Syslog.Rfc5424Formatter]::DefaultMessageIdPropertyName,

        [int]$BatchSizeLimit,
        [int]$PeriodSeconds,
        [int]$QueueLimit,
        [switch]$EagerlyEmitFirstEvent,

        [string]$SourceHost = $null,
        [System.Func``2[Serilog.Events.LogEventLevel, Serilog.Sinks.Syslog.Severity]]$SeverityMapping = $null,
        [Serilog.Formatting.ITextFormatter]$Formatter = $null,
        [Serilog.Core.LoggingLevelSwitch]$LevelSwitch = $null
    )

    process {
        # Build PeriodicBatchingSinkOptions if batching args provided
        $batchConfig = $null
        if ($PSBoundParameters.ContainsKey('BatchSizeLimit') -or
            $PSBoundParameters.ContainsKey('PeriodSeconds') -or
            $PSBoundParameters.ContainsKey('QueueLimit') -or
            $EagerlyEmitFirstEvent.IsPresent) {

            $batchConfig = [Serilog.Sinks.PeriodicBatching.PeriodicBatchingSinkOptions]::new()
            if ($PSBoundParameters.ContainsKey('BatchSizeLimit')) { $batchConfig.BatchSizeLimit = $BatchSizeLimit }
            if ($PSBoundParameters.ContainsKey('PeriodSeconds')) { $batchConfig.Period = [TimeSpan]::FromSeconds($PeriodSeconds) }
            if ($PSBoundParameters.ContainsKey('QueueLimit')) { $batchConfig.QueueLimit = $QueueLimit }
            if ($EagerlyEmitFirstEvent.IsPresent) { $batchConfig.EagerlyEmitFirstEvent = $true }
        }

        return [Serilog.SyslogLoggerConfigurationExtensions]::TcpSyslog(
            $LoggerConfig.WriteTo,     # 1 loggerSinkConfig
            $Hostname,                 # 2 host
            $Port,                     # 3 port
            $AppName,                  # 4 appName
            $FramingType,              # 5 framingType
            $Format,                   # 6 format
            $Facility,                 # 7 facility
            $UseTls.IsPresent,         # 8 useTls (bool)
            $CertProvider,             # 9 certProvider
            $CertValidationCallback,   # 10 certValidationCallback
            $OutputTemplate,           # 11 outputTemplate
            $RestrictedToMinimumLevel, # 12 restrictedToMinimumLevel
            $MessageIdPropertyName,    # 13 messageIdPropertyName
            $batchConfig,              # 14 batchConfig
            $SourceHost,               # 15 sourceHost
            $SeverityMapping,          # 16 severityMapping
            $Formatter,                # 17 formatter
            $LevelSwitch               # 18 levelSwitch
        )
    }
}
