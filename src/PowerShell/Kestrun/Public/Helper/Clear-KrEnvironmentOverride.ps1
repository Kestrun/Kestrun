<#
.SYNOPSIS
    Clears any Kestrun environment override for the current PowerShell session.
.DESCRIPTION
    Clears any Kestrun environment override for the current PowerShell session.
    This reverts Kestrun to use the default environment, which is 'Production' unless
    changed by the KESTRUN_ENVIRONMENT environment variable.
.EXAMPLE
    Clear-KrEnvironmentOverride
    Clears any Kestrun environment override, reverting to the default environment.
    This is useful for resetting the environment after running tests or examples.
#>
function Clear-KrEnvironmentOverride {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(SupportsShouldProcess)]
    param()
    if ($PSCmdlet.ShouldProcess('Kestrun Environment', 'Clear override')) {
        [Kestrun.Runtime.EnvironmentHelper]::ClearOverride()
        [Kestrun.Runtime.EnvironmentHelper]::Name
    }
}
