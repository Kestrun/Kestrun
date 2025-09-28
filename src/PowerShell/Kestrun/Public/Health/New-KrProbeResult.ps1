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
        function _NormalizeValue([object]$v, [int]$depth) {
            if ($null -eq $v) { return $null }
            if ($depth -gt 8) { return ($v.ToString()) }

            # Unwrap PSObject shell
            if ($v -is [System.Management.Automation.PSObject]) {
                $base = $v.BaseObject
                if ($null -eq $base -or $base -eq $v) { return $v.ToString() }
                return _NormalizeValue $base ($depth + 1)
            }

            # Hashtable / IDictionary → new Dictionary[string, object]
            if ($v -is [System.Collections.IDictionary]) {
                $out = [System.Collections.Generic.Dictionary[string, object]]::new()
                foreach ($key in $v.Keys) {
                    if ([string]::IsNullOrWhiteSpace([string]$key)) { continue }
                    $nv = _NormalizeValue $v[$key] ($depth + 1)
                    if ($null -ne $nv) { $out[[string]$key] = $nv }
                }
                return $out
            }

            # Enumerable (but not string) → List<object>
            if ($v -is [System.Collections.IEnumerable] -and -not ($v -is [string])) {
                $list = New-Object System.Collections.Generic.List[object]
                foreach ($item in $v) { $list.Add((_NormalizeValue $item ($depth + 1))) }
                return $list
            }

            return $v  # primitive / POCO
        }

        $dict = [System.Collections.Generic.Dictionary[string, object]]::new()
        foreach ($k in $Data.Keys) {
            if ([string]::IsNullOrWhiteSpace([string]$k)) { continue }
            $nv = _NormalizeValue $Data[$k] 0
            if ($null -ne $nv) { $dict[$k] = $nv }
        }
    }

    # Create and return the ProbeResult
    return [Kestrun.Health.ProbeResult]::new($enumStatus, $Description, $dict)
}
