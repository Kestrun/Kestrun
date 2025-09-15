<#
    .SYNOPSIS
        Adds the process name to the log context.
    .DESCRIPTION
        Adds the process name to the log context, allowing it to be included in log events.
    .PARAMETER LoggerConfig
        Instance of LoggerConfiguration
    .INPUTS
        None
    .OUTPUTS
        LoggerConfiguration object allowing method chaining
    .EXAMPLE
        PS> New-KrLogger | Add-KrEnrichProcessName | Register-KrLogger
#>
function Add-KrEnrichProcessName {
    [KestrunRuntimeApi('Everywhere')]
    [OutputType([Serilog.LoggerConfiguration])]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$loggerConfig
    )

    process {
        return [Serilog.ProcessLoggerConfigurationExtensions]::WithProcessName($loggerConfig.Enrich)
    }
}

