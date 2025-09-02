<#
    .SYNOPSIS
        Clears Kestrun variables that are not in the baseline or excluded list.
    .DESCRIPTION
        This function removes variables from the global scope that are not part of the Kestrun baseline or specified in the ExcludeVariables parameter.
    .PARAMETER ExcludeVariables
        An array of variable names to exclude from removal.
    .OUTPUTS
        None
    .EXAMPLE
        Clear-KsVariable -ExcludeVariables @('MyVariable1', 'MyVariable2')
        This example clears all Kestrun variables except 'MyVariable1' and 'MyVariable2'.
    .EXAMPLE
        Clear-KsVariable
        This example clears all Kestrun variables.
    .NOTES
        This function is useful for cleaning up the global scope in Kestrun scripts, ensuring that only relevant variables remain.
#>
function Clear-KsVariable {
    [CmdletBinding()]
    param(
        [string[]]$ExcludeVariables
    )
    $baseline = [Kestrun.KestrunHostManager]::VariableBaseline
    Get-Variable | Where-Object {
        $baseline -notcontains $_.Name -and
        $ExcludeVariables -notcontains $_.Name -and
        $_.Name -notmatch '^PS' -and
        $_.Name -notin @(
            '__psEditorServices_userInput', '__psEditorServices_CallStack', '__VSCodeState', '?', '^', '$', 'args', 'ConfirmPreference', 'd', 'DebugPreference', 'EnabledExperimentalFeatures',
            'Error', 'ErrorActionPreference', 'ErrorView', 'ExecutionContext', 'false', 'FormatEnumerationLimit', 'HOME', 'Host', 'InformationPreference',
            'input', 'IsCoreCLR', 'IsLinux', 'IsMacOS', 'IsWindows', 'Matches', 'MaximumHistoryCount', 'MyInvocation', 'NestedPromptLevel', 'null', 'OutputEncoding',
            'PID', 'PROFILE', 'ProgressPreference', 'PWD', 'ShellId', 'StackTrace', 'true', 'VerbosePreference', 'WarningPreference', 'WhatIfPreference',
            'assemblyLoadPath', 'moduleRootPath', 'netVersion', 'codeAnalysisVersion', 'inRouteRunspace' , 'sysfuncs', 'sysaliases', 'funcs', 'aliases', '_', 'switch'
        )
    } | ForEach-Object {
        if ( (Get-KrVariableScopeInfo -Name $_.Name).EffectiveScope -eq 'script') {
            Remove-Variable -Name $_.Name -Scope Global -Force -ErrorAction SilentlyContinue
        }
    }
}