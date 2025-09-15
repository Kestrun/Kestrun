<#
    .SYNOPSIS
        Merges two hashtables.
    .DESCRIPTION
        This function takes two hashtables and merges them into a single hashtable.
        If a key exists in both hashtables, the value from the second hashtable will be used.
    .PARAMETER a
        The first hashtable to merge.
    .PARAMETER b
        The second hashtable to merge.
    .OUTPUTS
        Hashtable
#>
function _KrMerge-Args {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '')]
    param([hashtable]$a, [hashtable]$b)

    if (-not $a) { return $b }
    if (-not $b) { return $a }
    $m = @{}
    foreach ($k in $a.Keys) { $m[$k] = $a[$k] }
    foreach ($k in $b.Keys) { $m[$k] = $b[$k] } # child overrides
    $m
}
