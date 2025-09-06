<#
    .SYNOPSIS
        Enriches log events with custom property.
    .DESCRIPTION
        Enriches log events with custom property. For example script name.
    .PARAMETER LoggerConfig
        Instance of LoggerConfiguration
    .PARAMETER Name
        The name of the property
    .PARAMETER Value
        The value of the property
    .PARAMETER DestructureObjects
        If present, and the value is a non-primitive, non-array type, then the value will be converted to a structure; otherwise, unknown types will be converted to scalars, which are generally stored as strings.
    .INPUTS
        None
    .OUTPUTS
        LoggerConfiguration object allowing method chaining
    .EXAMPLE
        PS> New-KrLogger | Add-KrEnrichProperty -Name ScriptName -Value 'Test' | Add-KrSinkConsole | Register-KrLogger
    #>
function Add-KrEnrichProperty {
    [KestrunRuntimeApi('Everywhere')]
    [OutputType([Serilog.LoggerConfiguration])]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$LoggerConfig,
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [object]$Value,
        [Parameter()]
        [switch]$DestructureObjects
    )

    process {
        return $LoggerConfig.Enrich.WithProperty($Name, $Value, $DestructureObjects.IsPresent)
    }
}

