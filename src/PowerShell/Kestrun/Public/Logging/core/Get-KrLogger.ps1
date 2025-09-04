<#
    .SYNOPSIS
        Gets the logger for the current session.
    .DESCRIPTION
        Gets the specified logger as the current logger for the session.
    .PARAMETER Name
        The name of the logger to get as the default logger.
    .OUTPUTS
        Returns the current default logger instance for the session.
        When the Name parameter is specified, it returns the name of the default logger.
        When the Name parameter is not specified, it returns the default logger instance.
    .EXAMPLE
        PS> $logger = Get-KrDefaultLogger
        Retrieves the current default logger instance for the session.
    .EXAMPLE
        PS> $logger = Get-KrDefaultLogger | Write-Host
        Retrieves the current default logger instance and outputs it to the console.
    .NOTES
        This function is part of the Kestrun logging framework and is used to retrieve the current default logger instance for the session.
        It can be used in scripts and modules that utilize Kestrun for logging.
#>
function Get-KrDefaultLogger {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Serilog.ILogger])]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $false)]
        [switch]$Name
    )
    if ($Name) {
        return [Kestrun.Logging.LoggerManager]::DefaultLoggerName
    }
    return [Kestrun.Logging.LoggerManager]::DefaultLogger
}

