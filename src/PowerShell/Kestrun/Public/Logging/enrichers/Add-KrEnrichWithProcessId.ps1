<#
    .SYNOPSIS
        Adds the process ID to the log context.
    .DESCRIPTION
        Adds the process ID to the log context, allowing it to be included in log events.
    .PARAMETER LoggerConfig
        Instance of LoggerConfiguration
    .INPUTS
        None
    .OUTPUTS
        LoggerConfiguration object allowing method chaining
    .EXAMPLE
        PS> New-KrLogger | Add-KrEnrichWithProcessId | Register-KrLogger
    .NOTES
        This function is part of the Kestrun logging infrastructure and should be used to enrich log events with process information.
        https://github.com/serilog/serilog-enrichers-process
#>
function Add-KrEnrichWithProcessId {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$loggerConfig
    )

    process {
        $loggerConfig = [Serilog.ProcessLoggerConfigurationExtensions]::WithProcessId($loggerConfig.Enrich)

        $loggerConfig
    }
}

