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
        PS> New-KrLogger | Add-KrEnrichWithExceptionDetail | Register-KrLogger
#>
function Add-KrEnrichWithExceptionDetail {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Serilog.LoggerConfiguration])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$LoggerConfig
    )

    process {
        $LoggerConfig = [Serilog.Exceptions.LoggerEnrichmentConfigurationExtensions]::WithExceptionDetails($LoggerConfig.Enrich)

        return $LoggerConfig
    }
}

