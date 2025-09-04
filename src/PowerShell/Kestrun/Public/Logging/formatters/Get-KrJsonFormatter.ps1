<#
    .SYNOPSIS
        Returns new instance of Serilog.Formatting.Json.JsonFormatter.
    .DESCRIPTION
        Returns new instance of Serilog.Formatting.Json.JsonFormatter that can be used with File or Console sink.
    .INPUTS
        None
    .OUTPUTS
        Instance of Serilog.Formatting.Json.JsonFormatter
    .EXAMPLE
        PS> New-KrLogger | Add-KrSinkFile -Path 'C:\Data\Log\test.log' -Formatter (Get-KrJsonFormatter) | Register-KrLogger
#>
function Get-KrJsonFormatter {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Serilog.Formatting.Json.JsonFormatter])]
    param()
    [Serilog.Formatting.Json.JsonFormatter]::new()
}
