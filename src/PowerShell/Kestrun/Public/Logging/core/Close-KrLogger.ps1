<#
    .SYNOPSIS
        Closes the logger and flushes all logs.
    .DESCRIPTION
        Closes the logger and flushes all logs. If no logger is specified, it will close the default logger.
    .PARAMETER Logger
        Instance of Serilog.Logger to close. If not specified, the default logger will be closed.
    .INPUTS
        Instance of Serilog.Logger
    .OUTPUTS
        None. This cmdlet does not return any output.
    .EXAMPLE
        PS> Close-KrLogger -Logger $myLogger
        Closes the specified logger and flushes all logs.
    .EXAMPLE
        PS> Close-KrLogger
        Closes the default logger and flushes all logs.
#>
function Close-KrLogger {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Serilog.ILogger]$Logger
    )

    process {
        if ($PSBoundParameters.ContainsKey('Logger')) {
            $Logger.Dispose()
        } else {
            [Serilog.Log]::CloseAndFlush()
        }
    }
}

