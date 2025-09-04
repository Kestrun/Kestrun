<#
    .SYNOPSIS
        Adds an Event Log sink to the Serilog logger configuration.
    .DESCRIPTION
        The Add-KrSinkEventLog function configures a logging sink that writes log events to the Windows Event Log. It allows customization of log source, log name, output template, and other parameters.
    .PARAMETER LoggerConfig
        The Serilog LoggerConfiguration object to which the Event Log sink will be added.
    .PARAMETER Source
        The source name for the Event Log. This is used to identify the application or service that is writing to the log.
    .PARAMETER LogName
        The name of the Event Log to write to. If not specified, defaults to 'Application'.
    .PARAMETER MachineName
        The name of the machine hosting the Event Log. The local machine by default.
    .PARAMETER ManageEventSource
        If set to true, the function will attempt to create the event source if it does not exist. Defaults to false.
    .PARAMETER OutputTemplate
        The output template string for formatting log messages. Defaults to '{Message}{NewLine}{Exception}{ErrorRecord}'.
    .PARAMETER FormatProvider
        An optional format provider for customizing message formatting.
    .PARAMETER RestrictedToMinimumLevel
        The minimum log event level required to write to the Event Log sink. Defaults to Verbose.
    .PARAMETER EventIdProvider
        An optional IEventIdProvider to provide custom event IDs for log events.
    .EXAMPLE
        Add-KrSinkEventLog -LoggerConfig $config -Source "MyApp" -LogName "Application"
        Adds an Event Log sink to the logging system that writes log events to the 'Application' log with the source 'MyApp'.
    .EXAMPLE
        Add-KrSinkEventLog -LoggerConfig $config -Source "MyApp" -LogName "CustomLog"
        Adds an Event Log sink to the logging system that writes log events to the 'CustomLog' log with the source 'MyApp'.
    .EXAMPLE
        Add-KrSinkEventLog -LoggerConfig $config -Source "MyApp" -LogName "Application" -ManageEventSource $true
        Adds an Event Log sink that manages the event source, creating it if it does not exist.
    .NOTES
        This function is part of the Kestrun logging infrastructure and should be used to enable Event Log logging.
    #>
function Add-KrSinkEventLog {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Serilog.LoggerConfiguration])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$LoggerConfig,
        [Parameter(Mandatory = $true)]
        [string]$Source,
        [Parameter(Mandatory = $false)]
        [string]$LogName = $null,
        [Parameter(Mandatory = $false)]
        [string]$MachineName = '.',
        [Parameter(Mandatory = $false)]
        [switch]$ManageEventSource,
        [Parameter(Mandatory = $false)]
        [string]$OutputTemplate = '{Message}{NewLine}{Exception}{ErrorRecord}',
        [Parameter(Mandatory = $false)]
        [System.IFormatProvider]$FormatProvider = $null ,
        [Parameter(Mandatory = $false)]
        [Serilog.Events.LogEventLevel]$RestrictedToMinimumLevel = [Serilog.Events.LogEventLevel]::Verbose,
        [Parameter(Mandatory = $false)]
        [Serilog.Sinks.EventLog.IEventIdProvider]$EventIdProvider = $null
    )

    process {
        if (-not [System.Diagnostics.EventLog]::SourceExists('Kestrun')) {
            [System.Diagnostics.EventLog]::CreateEventSource('Kestrun', 'Application')
        }
        $LoggerConfig = [Serilog.LoggerConfigurationEventLogExtensions]::EventLog(
            $LoggerConfig.WriteTo,
            $Source,
            $LogName,
            $MachineName,
            $ManageEventSource.IsPresent,
            $OutputTemplate,
            $FormatProvider,
            $RestrictedToMinimumLevel,
            $EventIdProvider
        )

        return $LoggerConfig
    }
}

