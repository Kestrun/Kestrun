<#
    .SYNOPSIS
        Sets the logger for the current session.
    .DESCRIPTION
        Sets the specified logger as the current logger for the session.
    .PARAMETER  Name
        The name of the logger to set as the default logger.
    .INPUTS
        Instance of Serilog.ILogger
    .OUTPUTS
        None. This cmdlet does not return any output.
    .EXAMPLE
        PS> Set-KrDefaultLogger -Logger $myLogger
        PS> $myLogger | Set-KrDefaultLogger
        Sets the specified logger as the current logger for the session.
    .EXAMPLE
        PS> $myLogger | Set-KrDefaultLogger
        Sets the specified logger as the current logger for the session.
#>
function Set-KrDefaultLogger {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [string]$Name
    )

    process {
        [Kestrun.Logging.LoggerManager]::DefaultLogger = [Kestrun.Logging.LoggerManager]::Get($Name)
    }
}
