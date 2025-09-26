<#
.SYNOPSIS
    Creates a new Kestrun health ProbeResult object.
.DESCRIPTION
    Simplifies constructing a [Kestrun.Health.ProbeResult] from PowerShell without using the
    raw static ::new() syntax. Accepts status, description, and an optional hashtable of data
    which is converted to a strongly typed Dictionary[string, object]. Returns the created
    ProbeResult for piping back to Add-KrHealthProbe script blocks or custom logic.
.PARAMETER Status
    Health status. Accepts Healthy, Degraded, or Unhealthy (case-insensitive).
.PARAMETER Description
    Short human readable description for diagnostics.
.PARAMETER Data
    Optional hashtable of additional metrics/values (serialized into response JSON).
.EXAMPLE
    New-KrProbeResult -Status Healthy -Description 'Cache OK'
.EXAMPLE
    New-KrProbeResult Degraded 'Latency high' -Data @{ p95 = 180; threshold = 150 }
.NOTES
    Intended for use inside -ScriptBlock probes: `return New-KrProbeResult Healthy 'Ready'`.
#>
function New-KrProbeResult {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [KestrunRuntimeApi('Runtime')]
    [CmdletBinding(PositionalBinding = $true)]
    [OutputType([Kestrun.Health.ProbeResult])]
    param(
        [Parameter(Mandatory, Position = 0)]
        [ValidateSet('Healthy', 'Degraded', 'Unhealthy')]
        [string]$Status,

        [Parameter(Mandatory, Position = 1)]
        [string]$Description,

        [Parameter(Position = 2)]
        [hashtable]$Data
    )

    # Map string to enum
    $enumStatus = [Kestrun.Health.ProbeStatus]::$Status

    $dict = $null
    if ($PSBoundParameters.ContainsKey('Data') -and $Data) {
        $dict = [System.Collections.Generic.Dictionary[string, object]]::new()
        foreach ($k in $Data.Keys) {
            $dict[$k] = $Data[$k]
        }
    }

    # Create and return the ProbeResult
    return [Kestrun.Health.ProbeResult]::new($enumStatus, $Description, $dict)
}
