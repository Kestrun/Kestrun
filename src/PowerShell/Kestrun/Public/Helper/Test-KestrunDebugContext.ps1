
<#
.SYNOPSIS
    Tests if the current PowerShell session is in a debugging context.
.DESCRIPTION
    Tests if the current PowerShell session is in a debugging context.
    This is determined by checking if a managed debugger is attached,
.PARAMETER IgnorePSBoundParameters
    If set, ignores the presence of the -Debug switch in the current command's bound parameters.
.PARAMETER IgnoreDebugPreference
    If set, ignores the current value of $DebugPreference.
.PARAMETER IgnorePSDebugContext
    If set, ignores whether the session is currently paused at a breakpoint or step.
.PARAMETER IgnoreHostDebuggerEnabled
    If set, ignores whether the host's debugger is enabled (e.g., in VS Code).
.EXAMPLE
    Test-KestrunDebugContext
    Returns $true if a managed debugger is attached, the -Debug switch is used,
    or the KESTRUN_DEBUG environment variable is set to a truthy value; otherwise, $false.
#>
function Test-KestrunDebugContext {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [outputType([bool])]
    param (
        [Parameter(Mandatory = $false)]
        [switch]$IgnorePSBoundParameters,
        [Parameter(Mandatory = $false)]
        [switch]$IgnoreDebugPreference,
        [Parameter(Mandatory = $false)]
        [switch]$IgnorePSDebugContext,
        [Parameter(Mandatory = $false)]
        [switch]$IgnoreHostDebuggerEnabled
    )
    return ((-not $IgnoreDebugPreference.IsPresent) -and ($DebugPreference -ne 'SilentlyContinue')) -or
    ((-not $IgnorePSBoundParameters.IsPresent) -and $PSBoundParameters.Debug.IsPresent) -or
    ((-not $IgnorePSDebugContext.IsPresent) -and ($PSDebugContext)) -or
    ((-not $IgnoreHostDebuggerEnabled.IsPresent) -and $Host.DebuggerEnabled)
}
