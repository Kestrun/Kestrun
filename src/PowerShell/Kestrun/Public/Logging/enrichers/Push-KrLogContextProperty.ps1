<#!
    .SYNOPSIS
        Push a property into Serilog's LogContext for the current scope.
    .DESCRIPTION
        Adds a property to Serilog's ambient LogContext so all log events written within the scope
        include the property. Returns an IDisposable; dispose it to remove the property.

        Requires the logger to be configured with Add-KrEnrichFromLogContext.
    .PARAMETER Name
        Property name to attach.
    .PARAMETER Value
        Property value to attach.
    .PARAMETER Destructure
        If set, complex objects will be destructured into structured properties.
    .OUTPUTS
        System.IDisposable
    .EXAMPLE
        PS> $d = Push-KrLogContextProperty -Name CorrelationId -Value $corr
        PS> try { Write-KrLog -Level Information -Message 'Hello' } finally { $d.Dispose() }
#>
function Push-KrLogContextProperty {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([System.IDisposable])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [object]$Value,
        [Parameter(Mandatory = $false)]
        [switch]$Destructure
    )

    process {
        if ($Destructure) {
            return [Serilog.Context.LogContext]::PushProperty($Name, $Value, $true)
        }
        return [Serilog.Context.LogContext]::PushProperty($Name, $Value)
    }
}
