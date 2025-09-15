<#
    .SYNOPSIS
        Closes the logger and flushes all logs.
    .DESCRIPTION
        Closes the logger and flushes all logs. If no logger is specified, it will close the default logger.
    .PARAMETER Logger
        Instance of Serilog.Logger to close. If not specified, the default logger will be closed.
    .PARAMETER LoggerName
        Name of the logger to close. If specified, the logger with this name will be closed
    .PARAMETER DefaultLogger
        If specified, closes the default logger.
    .INPUTS
        Instance of Serilog.Logger
    .OUTPUTS
        None. This cmdlet does not return any output.
    .EXAMPLE
        PS> Close-KrLogger -Logger $myLogger
        Closes the specified logger and flushes all logs.
    .EXAMPLE
        PS> Close-KrLogger
        Closes all active loggers and flushes any remaining logs.
    .EXAMPLE
        PS> Close-KrLogger -LoggerName 'MyLogger'
        Closes the logger with the specified name and any remaining logs.
    .EXAMPLE
        PS> Close-KrLogger -DefaultLogger
        Closes the default logger and flushes any remaining logs.
#>
function Close-KrLogger {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'AllLogs')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true, ParameterSetName = 'Logger')]
        [Serilog.ILogger]$Logger,

        [Parameter(Mandatory = $false, ParameterSetName = 'LoggerName')]
        [string]$LoggerName,

        [Parameter(Mandatory = $false, ParameterSetName = 'Default')]
        [switch]$DefaultLogger
    )

    process {
        if ($DefaultLogger) {
            $Logger = [Kestrun.Logging.LoggerManager]::GetDefault()
        } elseif ($Null -eq $Logger) {
            if (-not [string]::IsNullOrEmpty($LoggerName)) {
                # If LoggerName is specified, get the logger with that name
                $Logger = [Kestrun.Logging.LoggerManager]::Get($LoggerName)
            }
        }
        if ($null -ne $Logger) {
            # Close the specified logger
            $null = [Kestrun.Logging.LoggerManager]::CloseAndFlush($Logger)
        } else {
            # Close all loggers
            [Kestrun.Logging.LoggerManager]::Clear()
        }
    }
}

