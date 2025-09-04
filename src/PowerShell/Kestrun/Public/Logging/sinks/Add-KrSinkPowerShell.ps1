<#
    .SYNOPSIS
        Adds a PowerShell sink to the logger configuration.
    .DESCRIPTION
        The Add-KrSinkPowerShell function configures a logging sink that outputs log events to the PowerShell console. It allows customization of log formatting and filtering based on log levels.
    .PARAMETER LoggerConfig
        The Serilog LoggerConfiguration object to which the PowerShell sink will be added.
    .PARAMETER RestrictedToMinimumLevel
        The minimum log event level required to write to the PowerShell sink. Defaults to Verbose.
    .PARAMETER OutputTemplate
        The output template string for formatting log messages. Defaults to '{Message:lj}{ErrorRecord}'.
    .PARAMETER LevelSwitch
        An optional LoggingLevelSwitch to dynamically control the logging level.
    .EXAMPLE
        Add-KrSinkPowerShell -LoggerConfig $config

        Adds a PowerShell sink to the logging system, allowing log messages to be output to the PowerShell console.
    .EXAMPLE
        Add-KrSinkPowerShell -LoggerConfig $config -RestrictedToMinimumLevel Information

        Adds a PowerShell sink that only outputs log events at Information level or higher.
    .EXAMPLE
        Add-KrSinkPowerShell -LoggerConfig $config -OutputTemplate '{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{ErrorRecord}{Exception}'
        Customizes the output template for PowerShell log messages.
    .EXAMPLE
        Add-KrSinkPowerShell -LoggerConfig $config -LevelSwitch $myLevelSwitch

        Uses a custom LoggingLevelSwitch to control the logging level dynamically.
    .NOTES
        This function is part of the Kestrun logging infrastructure and should be used to enable PowerShell console logging.
#>
function Add-KrSinkPowerShell {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Serilog.LoggerConfiguration])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$LoggerConfig,

        [Parameter(Mandatory = $false)]
        [Serilog.Events.LogEventLevel]$RestrictedToMinimumLevel = [Serilog.Events.LogEventLevel]::Verbose,

        [Parameter(Mandatory = $false)]
        [string]$OutputTemplate = '{Message:lj}{ErrorRecord}',

        [Parameter(Mandatory = $false)]
        [Serilog.Core.LoggingLevelSwitch]$LevelSwitch = $null
    )

    process {
        $LoggerConfig = [Kestrun.Logging.Sinks.Extensions.PowerShellSinkExtensions]::PowerShell($LoggerConfig.WriteTo,
            { param([Serilog.Events.LogEvent]$logEvent, [string]$renderedMessage) Write-KrSinkPowerShell -LogEvent $logEvent -RenderedMessage $renderedMessage },
            $RestrictedToMinimumLevel,
            $OutputTemplate,
            $LevelSwitch
        )

        return $LoggerConfig
    }
}

