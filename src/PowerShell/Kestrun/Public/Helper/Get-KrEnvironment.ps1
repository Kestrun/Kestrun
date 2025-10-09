<#
.SYNOPSIS
    Gets the current Kestrun environment for the PowerShell session.
.DESCRIPTION
    Gets the current Kestrun environment for the PowerShell session.
    This reflects how Kestrun behaves, for example in terms of error handling and logging.
.EXAMPLE
    Get-KrDebugContext
    Returns the current Kestrun environment, e.g. 'Development'.
    This reflects how Kestrun behaves, for example in terms of error handling and logging.
#>
function Get-KrDebugContext {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param()
    return [Kestrun.Runtime.EnvironmentHelper]::Name
}
