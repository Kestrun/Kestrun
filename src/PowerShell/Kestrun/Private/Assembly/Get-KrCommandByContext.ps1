<#
    .SYNOPSIS
      Retrieves Kestrun commands based on their context.
    .DESCRIPTION
      This function retrieves Kestrun commands based on their context, allowing for filtering by command type and context.
      It supports both inclusive and exclusive filtering.
    .PARAMETER AnyOf
      Specifies the contexts that the commands must match at least one of.
    .PARAMETER AllOf
      Specifies the contexts that the commands must match all of.
    .PARAMETER Not
      Specifies the contexts that the commands must not match.
    .PARAMETER Module
      The name of the module to search for commands.
    .PARAMETER IncludeNonExported
      Whether to include non-exported commands in the search.
    .PARAMETER Exact
      Whether to match the contexts exactly or allow for partial matches.
    .PARAMETER Functions
      An array of functions to filter by context. If not provided, all functions in the specified module are considered.
    .OUTPUTS
      [System.Management.Automation.CommandInfo[]]
      An array of command information objects that match the specified context.
#>
function Get-KrCommandsByContext {
    [CmdletBinding(DefaultParameterSetName = 'AnyOf')]
    param(
        [Parameter(Mandatory, ParameterSetName = 'AnyOf')]
        [ValidateSet('Definition', 'Route', 'Schedule', 'ScheduleAndDefinition', 'Runtime', 'Everywhere')]
        [string[]]$AnyOf,

        [Parameter(Mandatory, ParameterSetName = 'AllOf')]
        [ValidateSet('Definition', 'Route', 'Schedule')]
        [string[]]$AllOf,

        [ValidateSet('Definition', 'Route', 'Schedule', 'ScheduleAndDefinition', 'Runtime', 'Everywhere')]
        [string[]]$Not,

        [string]$Module = 'Kestrun',
        [switch]$IncludeNonExported,
        [switch]$Exact,

        [object[]]$Functions
    )
    function _KrNameToMask {
        <#
        .SYNOPSIS
            Converts a context name to its corresponding bitmask.
        .DESCRIPTION
            This function takes a context name (e.g., Definition, Route, Schedule) and converts it to its
            corresponding bitmask value.
        .PARAMETER Name
            The name of the context to convert.
        .OUTPUTS
            [int]
            The bitmask value for the specified context name.
        #>
        param([Parameter(Mandatory)][string]$Name)
        switch ($Name) {
            'Definition' { 1 }
            'Route' { 2 }
            'Schedule' { 4 }
            'ScheduleAndDefinition' { 5 }
            'Runtime' { 6 }
            'Everywhere' { 7 }
            default { throw "Unknown context '$Name'." }
        }
    }

    $cmds = if ($Functions) {
        $Functions
    } else {
        if ($IncludeNonExported) { Get-Command -Module $Module -All } else { Get-Command -Module $Module }
    }

    $target = 0
    if ($PSCmdlet.ParameterSetName -eq 'AnyOf') { foreach ($n in $AnyOf) { $target = $target -bor (_KrNameToMask $n) } }
    else { foreach ($n in $AllOf) { $target = $target -bor (_KrNameToMask $n) } }

    $notMask = 0
    foreach ($n in ($Not | ForEach-Object { $_ })) { $notMask = $notMask -bor (_KrNameToMask $n) }

    $match = if ($PSCmdlet.ParameterSetName -eq 'AnyOf') {
        if ($Exact) { { param($m) $m -eq $target } } else { { param($m) ($m -band $target) -ne 0 } }
    } else {
        if ($Exact) { { param($m) $m -eq $target } } else { { param($m) ($m -band $target) -eq $target } }
    }

    foreach ($c in $cmds) {
        $m = 0
        if ($c.CommandType -eq 'Function') {
            $m = Get-KrFunctionContextMask -Function $c
        } elseif ($c.CommandType -eq 'Cmdlet' -and $c.ImplementingType) {
            $a = $c.ImplementingType.GetCustomAttributes($true) |
                Where-Object { $_.GetType().Name -eq 'KestrunRuntimeApiAttribute' } |
                Select-Object -First 1
            if ($a) { $m = [int]([KestrunApiContext]$a.Contexts) }
        }

        if ($m -eq 0) { continue }
        if ($notMask -ne 0 -and ($m -band $notMask) -ne 0) { continue }  # exclude forbidden bits
        if (& $match $m) { $c }
    }
}

