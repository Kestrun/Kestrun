
<#
.SYNOPSIS
    Sets the Kestrun environment for the current PowerShell session.
.DESCRIPTION
    Sets the Kestrun environment for the current PowerShell session.
    This affects how Kestrun behaves, for example in terms of error handling and logging.
.PARAMETER Name
    The name of the environment to set. Valid values are 'Development', 'Staging', and 'Production'.
.PARAMETER WhatIf
    Shows what would happen if the cmdlet runs. The cmdlet is not run.
.PARAMETER Confirm
    Prompts you for confirmation before running the cmdlet.
.OUTPUTS
    The name of the currently set environment after applying the change.
.EXAMPLE
    Set-KrEnvironment -Name 'Development'
    Sets the Kestrun environment to 'Development'.
    This enables detailed error messages and logging for development purposes.
.EXAMPLE
    Set-KrEnvironment -Name 'Production'
    Sets the Kestrun environment to 'Production'.
    This enables optimized settings for a production environment.
#>
function Set-KrEnvironment {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Auto', 'Development', 'Staging', 'Production')]
        [string]$Name
    )
    if ($PSCmdlet.ShouldProcess('Kestrun Environment', "Set -> $Name")) {
        if ($Name -eq 'Auto') {
            [Kestrun.Runtime.EnvironmentHelper]::ClearOverride()
        } else {
            [Kestrun.Runtime.EnvironmentHelper]::SetOverrideName($Name)
        }
        [Kestrun.Runtime.EnvironmentHelper]::Name
    }
}
