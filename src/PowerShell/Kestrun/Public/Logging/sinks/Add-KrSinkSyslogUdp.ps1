<#
.SYNOPSIS
    Adds a Syslog UDP sink to the Serilog logger configuration.

.DESCRIPTION
    Configures a Serilog sink that sends log events to a Syslog server over UDP.
    Supports hostname, port, app name, format, facility, output template, minimum level,
    batching, message-id property name (RFC5424), source host, custom severity mapping,
    custom formatter, dynamic level switch, and structured data ID.

.PARAMETER LoggerConfig
    The Serilog LoggerConfiguration object to which the Syslog UDP sink will be added.

.PARAMETER Hostname
    The hostname or IP address of the Syslog server to which log events will be sent.

.PARAMETER Port
    The port number on which the Syslog server is listening. Defaults to 514.

.PARAMETER AppName
    The application name to be included in the Syslog messages. If not specified, defaults to the process name.

.PARAMETER Format
    The Syslog message format to use. Defaults to RFC3164.

.PARAMETER Facility
    The Syslog facility to use for the log messages. Defaults to Local0.

.PARAMETER OutputTemplate
    The output template string for formatting log messages (used by the sink’s formatter).
    If omitted, the sink’s default template/formatter is used.

.PARAMETER RestrictedToMinimumLevel
    The minimum log event level required to write to the Syslog sink. Defaults to Minimum.

.PARAMETER MessageIdPropertyName
    For RFC5424 only: property name used to derive the Message ID (default is the sink’s constant).

.PARAMETER SourceHost
    Optional override for the source host field written by the formatter.

.PARAMETER SeverityMapping
    Custom delegate to map Serilog levels to syslog severities:
    [System.Func``2[Serilog.Events.LogEventLevel,Serilog.Sinks.Syslog.Severity]]

.PARAMETER Formatter
    Optional custom ITextFormatter for full control over message formatting.

.PARAMETER LevelSwitch
    Optional LoggingLevelSwitch to dynamically control the level.

# ---- Batching (optional; created only if you set any of these) ----
.PARAMETER BatchSizeLimit
    Maximum number of events per batch.

.PARAMETER PeriodSeconds
    Flush period in seconds.

.PARAMETER QueueLimit
    Maximum queued events before dropping.

.PARAMETER EagerlyEmitFirstEvent
    If specified, the first event is emitted immediately (no waiting for the first period).

.EXAMPLE
    # simplest: send logs over UDP with defaults
    Add-KrSinkSyslogUdp -LoggerConfig $config -Hostname "syslog.example.com"

.EXAMPLE
    # RFC5424 with Local1 facility and custom app name
    Add-KrSinkSyslogUdp -LoggerConfig $config -Hostname "syslog.example.com" `
        -Format RFC5424 -Facility Local1 -AppName "Kestrun"

.EXAMPLE
    # batching: 50 events, flush every 2s, queue up to 5000, emit first immediately
    Add-KrSinkSyslogUdp -LoggerConfig $config -Hostname "syslog.example.com" `
        -BatchSizeLimit 50 -PeriodSeconds 2 -QueueLimit 5000 -EagerlyEmitFirstEvent

.EXAMPLE
    # custom severity mapping (Information→Notice, Fatal→Emergency)
    $map = [System.Func[Serilog.Events.LogEventLevel,Serilog.Sinks.Syslog.Severity]]{
        param($level)
        switch ($level) {
            'Information' { [Serilog.Sinks.Syslog.Severity]::Notice }
            'Fatal'       { [Serilog.Sinks.Syslog.Severity]::Emergency }
            'Warning'     { [Serilog.Sinks.Syslog.Severity]::Warning }
            'Error'       { [Serilog.Sinks.Syslog.Severity]::Error }
            'Debug'       { [Serilog.Sinks.Syslog.Severity]::Debug }
            'Verbose'     { [Serilog.Sinks.Syslog.Severity]::Debug }
            default       { [Serilog.Sinks.Syslog.Severity]::Informational }
        }
    }
    Add-KrSinkSyslogUdp -LoggerConfig $config -Hostname "syslog.example.com" -SeverityMapping $map

.EXAMPLE
    # advanced: override message-id property name and source host (RFC5424)
    Add-KrSinkSyslogUdp -LoggerConfig $config -Hostname "syslog.example.com" `
        -Format RFC5424 -MessageIdPropertyName "SourceContext" -SourceHost "api01"

.NOTES
    This function is part of the Kestrun logging infrastructure and enables Syslog UDP logging.
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

        [int]$Port = 514,
        [string]$AppName = $null,

        [Serilog.Sinks.Syslog.SyslogFormat]$Format = [Serilog.Sinks.Syslog.SyslogFormat]::RFC3164,
        [Serilog.Sinks.Syslog.Facility]$Facility = [Serilog.Sinks.Syslog.Facility]::Local0,

        [string]$OutputTemplate = $null,
        [Serilog.Events.LogEventLevel]$RestrictedToMinimumLevel = [Serilog.Events.LevelAlias]::Minimum,

        # extras supported by the sink:
        [string]$MessageIdPropertyName = [Serilog.Sinks.Syslog.Rfc5424Formatter]::DefaultMessageIdPropertyName,
        [string]$SourceHost = $null,
        [System.Func``2[Serilog.Events.LogEventLevel, Serilog.Sinks.Syslog.Severity]]$SeverityMapping = $null,
        [Serilog.Formatting.ITextFormatter]$Formatter = $null,
        [Serilog.Core.LoggingLevelSwitch]$LevelSwitch = $null,

        # ---- Optional batching knobs ----
        [int]$BatchSizeLimit,
        [int]$PeriodSeconds,
        [int]$QueueLimit,
        [switch]$EagerlyEmitFirstEvent
    )

    process {
        # Build PeriodicBatchingSinkOptions only if any batching parameter is supplied
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

        # Call must be strictly positional to match the .NET signature
        return [Serilog.SyslogLoggerConfigurationExtensions]::UdpSyslog(
            $LoggerConfig.WriteTo,         # 1 loggerSinkConfig (this)
            $Hostname,                     # 2 host
            $Port,                         # 3 port
            $AppName,                      # 4 appName
            $Format,                       # 5 format
            $Facility,                     # 6 facility
            $batchConfig,                  # 7 batchConfig
            $OutputTemplate,               # 8 outputTemplate
            $RestrictedToMinimumLevel,     # 9 restrictedToMinimumLevel
            $MessageIdPropertyName,        # 10 messageIdPropertyName
            $SourceHost,                   # 11 sourceHost
            $SeverityMapping,              # 12 severityMapping
            $Formatter,                    # 13 formatter
            $LevelSwitch                   # 14 levelSwitch
        )
    }
}
