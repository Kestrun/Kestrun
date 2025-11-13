<#
.SYNOPSIS
    Converts collections to thread-safe equivalents.
.DESCRIPTION
    This function takes various collection types (hashtables, arrays, dictionaries)
    and converts them into thread-safe versions suitable for use in multi-threaded
    or multi-runspace scenarios.
.PARAMETER Value
    The input collection to convert.
.EXAMPLE
    # Convert a hashtable to a thread-safe hashtable
    $ht = @{ Key1 = 'Value1'; Key2 = 'Value2' }
    $threadSafeHt = ConvertTo-KrThreadSafeValue -Value $ht
.EXAMPLE
    # Convert an ArrayList to a thread-safe ArrayList
    $arrayList = [System.Collections.ArrayList]::new()
    $threadSafeArrayList = ConvertTo-KrThreadSafeValue -Value $arrayList
.OUTPUTS
    Thread-safe collection equivalent of the input. If the input is not a collection, returns it unchanged.
#>
function ConvertTo-KrThreadSafeValue {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseOutputTypeCorrectly', '')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object]$Value
    )

    if ($null -eq $Value) {
        return $null
    }

    # --- Hashtable (@{}) ---
    if ($Value -is [hashtable]) {
        return [hashtable]::Synchronized($Value)
    }

    # --- OrderedDictionary ([ordered]@{}) ---
    if ($Value -is [System.Collections.Specialized.OrderedDictionary]) {
        # Copy into a normal hashtable and wrap
        $ht = @{}
        foreach ($entry in $Value.GetEnumerator()) {
            $ht[$entry.Key] = $entry.Value
        }
        return [hashtable]::Synchronized($ht)
    }

    # --- ArrayList ---
    if ($Value -is [System.Collections.ArrayList]) {
        return [System.Collections.ArrayList]::Synchronized($Value)
    }

    # --- Any other IDictionary (generic or not, but not handled above) ---
    if ($Value -is [System.Collections.IDictionary]) {
        $dict = [System.Collections.Concurrent.ConcurrentDictionary[object, object]]::new()
        foreach ($entry in $Value.GetEnumerator()) {
            $null = $dict.TryAdd($entry.Key, $entry.Value)
        }
        return $dict
    }

    # --- Arrays: treat as immutable snapshots ---
    if ($Value -is [Array]) {
        return $Value
    }

    # --- PSCustomObject, scalars, etc.: just return as-is ---
    return $Value
}
