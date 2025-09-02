
<#
    .SYNOPSIS
        Gets the default Kestrun PowerShell variables.
    .DESCRIPTION
        This function retrieves the default Kestrun PowerShell variables that are part of the PowerShell system variables.
    .OUTPUTS
        System.Management.Automation.PSVariable
    .EXAMPLE
        Get-KrPSDefaultVariable
#>
function Get-KrPSDefaultVariable {

    (Get-Variable |
        Where-Object {
            $_.Name -match '^PS' -or
            $_.Name -in @(
                '__psEditorServices_userInput', '__psEditorServices_CallStack', '__VSCodeState', '?', '^', '$', 'args', 'ConfirmPreference', 'd', 'DebugPreference', 'EnabledExperimentalFeatures',
                'Error', 'ErrorActionPreference', 'ErrorView', 'ExecutionContext', 'false', 'FormatEnumerationLimit', 'HOME', 'Host', 'InformationPreference',
                'input', 'IsCoreCLR', 'IsLinux', 'IsMacOS', 'IsWindows', 'Matches', 'MaximumHistoryCount', 'MyInvocation', 'NestedPromptLevel', 'null', 'OutputEncoding',
                'PID', 'PROFILE', 'ProgressPreference', 'PWD', 'ShellId', 'StackTrace', 'true', 'VerbosePreference', 'WarningPreference', 'WhatIfPreference',
                'assemblyLoadPath', 'moduleRootPath', 'netVersion', 'codeAnalysisVersion', 'inRouteRunspace' , 'sysfuncs', 'sysaliases', 'funcs', 'aliases', '_', 'switch'
            )
        }).Name
}