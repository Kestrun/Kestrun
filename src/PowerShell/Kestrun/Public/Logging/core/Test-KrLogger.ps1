<#
    .SYNOPSIS
        Tests if a logger exists or returns the current default logger for the session.
    .DESCRIPTION
        Gets the specified logger as the current logger for the session, or tests if a named logger exists.
    .PARAMETER Name
        The name of the logger to test for existence or get as the default logger.
    .OUTPUTS
        When the Name parameter is specified, it returns a boolean indicating whether the named logger exists.
        When the Name parameter is not specified, it returns the default logger instance.
    .EXAMPLE
        PS> $logger = Test-KrLogger
        Retrieves the current default logger instance for the session.
    .EXAMPLE
        PS> $logger = Test-KrLogger | Write-Host
        Retrieves the current default logger instance and outputs it to the console.
  .NOTES
        This function is part of the Kestrun logging framework and is used to retrieve the current default logger instance for the session.
        It can be used in scripts and modules that utilize Kestrun for logging.
#>
function Test-KrLogger {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Serilog.ILogger])]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $false)]
        [switch]$Name
    )
    if ($Name) {
        return ([Kestrun.Logging.LoggerManager]::Contains($Name))
    }

    return [Kestrun.Logging.LoggerManager]::DefaultLogger.ToString() -ne "Serilog.Core.Pipeline.SilentLogger"
}

