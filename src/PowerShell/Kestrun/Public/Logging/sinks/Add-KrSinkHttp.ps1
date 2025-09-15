<#
    .SYNOPSIS
        Adds an HTTP sink to the Serilog logger configuration.
    .DESCRIPTION
        The Add-SinkHttp function configures a logging sink that sends log events to a specified HTTP endpoint.
        It allows customization of the request URI, batch posting limit, queue limit, period, formatter, batch formatter, minimum log level, HTTP client, and configuration.
    .PARAMETER LoggerConfig
        The Serilog LoggerConfiguration object to which the HTTP sink will be added.
    .PARAMETER RequestUri
        The URI of the HTTP endpoint to which log events will be sent.
    .PARAMETER BatchPostingLimit
        The maximum number of log events to batch together before sending. Defaults to 1000.
    .PARAMETER QueueLimit
        The maximum number of log events to keep in the queue before dropping new events. Defaults to unlimited.
    .PARAMETER Period
        The time interval at which to send batched log events. Defaults to 2 seconds.
    .PARAMETER Formatter
        The formatter to use for individual log events. Defaults to the JSON formatter.
    .PARAMETER BatchFormatter
        The formatter to use for the entire batch of log events. Defaults to the JSON formatter.
    .PARAMETER RestrictedToMinimumLevel
        The minimum log level required for events to be sent to the HTTP sink. Defaults to Verbose.
    .PARAMETER HttpClient
        The HTTP client to use for sending log events. Defaults to a new instance of HttpClient.
    .PARAMETER Configuration
        The configuration to use for the HTTP sink. Defaults to the global configuration.
    .EXAMPLE
        Add-SinkHttp -LoggerConfig $config -RequestUri "http://example.com/log" -BatchPostingLimit 500 -QueueLimit 100 -Period 1 -Formatter $formatter -BatchFormatter $batchFormatter
        Adds an HTTP sink to the logging system that sends log events to "http://example.com/log" with specified batch settings and formatters.
    .EXAMPLE
        Add-SinkHttp -LoggerConfig $config -RequestUri "http://example.com/log"
        Adds an HTTP sink to the logging system that sends log events to "http://example.com/log" with default settings.
    .NOTES
        This function is part of the Kestrun logging infrastructure and should be used to enable HTTP logging.
#>
function Add-KrSinkHttp {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Serilog.LoggerConfiguration])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$LoggerConfig,

        [Parameter(Mandatory = $true)]
        [string]$RequestUri,

        [Parameter(Mandatory = $false)]
        [int]$BatchPostingLimit = 1000,

        [Parameter(Mandatory = $false)]
        [Nullable[System.Int32]]$QueueLimit = $null,

        [Parameter(Mandatory = $false)]
        [Nullable[System.TimeSpan]]$Period = $null,

        [Parameter(Mandatory = $false)]
        [Serilog.Formatting.ITextFormatter]$Formatter = $null,

        [Parameter(Mandatory = $false)]
        [Serilog.Sinks.Http.IBatchFormatter]$BatchFormatter = $null,

        [Serilog.Events.LogEventLevel]$RestrictedToMinimumLevel = [Serilog.Events.LogEventLevel]::Verbose,

        [Parameter(Mandatory = $false)]
        [Serilog.Sinks.Http.IHttpClient]$HttpClient = $null,

        [Parameter(Mandatory = $false)]
        [Microsoft.Extensions.Configuration.IConfiguration]$Configuration = $null
    )
    process {
        return [Serilog.LoggerSinkConfigurationExtensions]::Http($LoggerConfig.WriteTo,
            $RequestUri,
            $BatchPostingLimit,
            $QueueLimit,
            $Period,
            $Formatter,
            $BatchFormatter,
            $RestrictedToMinimumLevel,
            $HttpClient,
            $Configuration
        )
    }
}

