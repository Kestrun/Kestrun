<#
    .SYNOPSIS
        Adds exception details to the log context.
    .DESCRIPTION
        Adds exception details to the log context, allowing them to be included in log events.
    .PARAMETER LoggerConfig
        Instance of LoggerConfiguration
    .INPUTS
        None
    .OUTPUTS
        LoggerConfiguration object allowing method chaining
    .EXAMPLE
        PS> New-KrLogger | Add-KrEnrichExceptionDetail | Register-KrLogger
#>
function Add-KrEnrichExceptionDetail {
    [KestrunRuntimeApi('Everywhere')]
    [OutputType([Serilog.LoggerConfiguration])]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$LoggerConfig
    )

    process {
        return [Serilog.Exceptions.LoggerEnrichmentConfigurationExtensions]::WithExceptionDetails($LoggerConfig.Enrich)
    }
}

