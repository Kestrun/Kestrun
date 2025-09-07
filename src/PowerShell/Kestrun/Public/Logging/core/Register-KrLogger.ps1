<#
    .SYNOPSIS
        Starts the Kestrun logger.
    .DESCRIPTION
        This function initializes the Kestrun logger with specified configurations.
    .PARAMETER Name
        The name of the logger instance. This is mandatory.
    .PARAMETER LoggerConfig
        A Serilog logger configuration object to set up the logger.
    .PARAMETER MinimumLevel
        The minimum log level for the logger. Default is Information.
    .PARAMETER Console
        If specified, adds a console sink to the logger.
    .PARAMETER PowerShell
        If specified, adds a PowerShell sink to the logger.
    .PARAMETER FilePath
        The file path where logs will be written. If not specified, defaults to a predefined path
    .PARAMETER FileRollingInterval
        The rolling interval for the log file. Default is Infinite.
    .PARAMETER SetAsDefault
        If specified, sets the created logger as the default logger for Serilog.
    .PARAMETER PassThru
        If specified, returns the created logger object.
    .EXAMPLE
        Register-KrLogger -Name "MyLogger" -MinimumLevel Debug -Console -FilePath "C:\Logs\kestrun.log" -FileRollingInterval Day -SetAsDefault
        Initializes the Kestrun logger with Debug level, adds console and file sinks, sets the logger as default, and returns the logger object.
    .EXAMPLE
        Register-KrLogger -Name "MyLogger" -LoggerConfig $myLoggerConfig -SetAsDefault
        Initializes the Kestrun logger using a pre-configured Serilog logger configuration object and sets it as the default logger.
    .EXAMPLE
        Register-KrLogger -Name "MyLogger" -MinimumLevel Debug -Console -FilePath "C:\Logs\kestrun.log" -FileRollingInterval Day -SetAsDefault
        Initializes the Kestrun logger with Debug level, adds console and file sinks, sets the logger as default, and returns the logger object.
    .EXAMPLE
        Register-KrLogger -Name "MyLogger" -MinimumLevel Debug -Console -FilePath "C:\Logs\kestrun.log" -FileRollingInterval Day -SetAsDefault
        Initializes the Kestrun logger with Debug level, adds console and file sinks, sets the logger as default, and returns the logger object.
#>
function Register-KrLogger {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([Serilog.ILogger])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$LoggerConfig,

        [Parameter(Mandatory = $false)]
        [switch]$SetAsDefault,

        [Parameter(Mandatory = $false)]
        [switch]$PassThru
    )

    process {
        $logger = [Kestrun.Logging.LoggerConfigurationExtensions]::Register($LoggerConfig, $Name, $SetAsDefault.IsPresent)
        if ($PassThru) {
            return $logger
        }
    }
}

