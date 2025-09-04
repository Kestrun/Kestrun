<#
    .SYNOPSIS
        Creates a new instance of Serilog.LoggerConfiguration.
    .DESCRIPTION
        Creates a new instance of Serilog.LoggerConfiguration that can be used to configure logging sinks and enrichers.
    .INPUTS
        None. You cannot pipe objects to New-KrLogger.
    .OUTPUTS
        Instance of Serilog.LoggerConfiguration.
    .EXAMPLE
        PS> $loggerConfig = New-KrLogger
        Creates a new logger configuration instance that can be used to add sinks and enrichers.
    .EXAMPLE
        PS> $loggerConfig = New-KrLogger | Add-KrSinkConsole | Add-KrEnrichWithProperty -Name 'ScriptName' -Value 'Test'
        Creates a new logger configuration instance, adds a console sink, and enriches logs with a property.
#>
function New-KrLogger {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([Serilog.LoggerConfiguration])]
    param()
    return [Serilog.LoggerConfiguration]::New()
}
