<#
    .SYNOPSIS
        Shows where a variable exists (private/local/parent/script/global) and which scope wins.
    .DESCRIPTION
        For each variable name, probes variable:PRIVATE/LOCAL/1..N/SCRIPT/GLOBAL.
        Returns an object with EffectiveScope, ScopesPresent, and (optionally) Values by scope.
    .PARAMETER Name
        Variable name(s). Accepts with or without a leading '$'. Supports pipeline.
    .PARAMETER MaxParents
        How many numbered parent scopes to probe (default 12).
    .PARAMETER IncludeValue
        Include the value captured in each scope in the Details property.
    .EXAMPLE
        Get-KrVariableScopeInfo foo
    .EXAMPLE
        'foo','bar' | Get-KrVariableScopeInfo -IncludeValue
#>
function Get-KrVariableScopeInfo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0, ValueFromPipeline, ValueFromPipelineByPropertyName)]
        [Alias('Var')]
        [string[]]$Name,

        [int]$MaxParents = 12,

        [switch]$IncludeValue
    )

    begin {
        $resolutionOrder = @('private', 'local') + (1..$MaxParents | ForEach-Object { "$_" }) + @('script', 'global')
        <#
            .SYNOPSIS
                Tests if a variable exists in a specific scope.
            .PARAMETER Scope
                The scope to test (private/local/1..N/script/global).
            .PARAMETER Name
                The name of the variable to test.
            .OUTPUTS
                PSObject representing the variable if it exists, otherwise $null.
        #>
        function TestVar([string]$scope, [string]$n) {
            $path = "variable:$($scope):$($n)"
            if (Test-Path $path) {
                $item = Get-Item $path
                [pscustomobject]@{ Scope = $scope; Value = $item.Value }
            }
        }
    }

    process {
        foreach ($raw in $Name) {
            $n = ($raw -replace '^\$', '')  # allow "$foo" or "foo"

            # probe all scopes
            $found = @()
            foreach ($s in $resolutionOrder) {
                $hit = TestVar $s $n
                if ($hit) { $found += $hit }
            }

            # first in resolution order is the effective binding
            $effective = $null
            foreach ($s in $resolutionOrder) {
                $m = $found | Where-Object { $_.Scope -eq $s } | Select-Object -First 1
                if ($m) { $effective = $m; break }
            }

            # shape result
            [pscustomobject]@{
                Name = $n
                EffectiveScope = $effective.Scope
                ScopesPresent = $found.Scope
                ResolutionOrder = $resolutionOrder
                Details = if ($IncludeValue) { $found } else { $found | Select-Object Scope }
            }
        }
    }
}
