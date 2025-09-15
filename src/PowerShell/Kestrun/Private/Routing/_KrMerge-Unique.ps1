<#
    .SYNOPSIS
        Merges two arrays, preserving unique values.
    .DESCRIPTION
        This function takes two arrays and merges them into a single array,
        preserving only unique values.
    .PARAMETER a
        The first array to merge.
    .PARAMETER b
        The second array to merge.
    .OUTPUTS
        Array
#>
function _KrMerge-Unique {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '')]
    param([string[]]$a, [string[]]$b)
    @(($a + $b | Where-Object { $_ -ne $null } | Select-Object -Unique))
}
