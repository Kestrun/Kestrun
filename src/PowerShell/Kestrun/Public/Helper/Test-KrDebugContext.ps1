
<#
.SYNOPSIS
    Tests if the current PowerShell session is in a debugging context.
.DESCRIPTION
    Tests if the current PowerShell session is in a debugging context.
    This is determined by checking if a managed debugger is attached,
.PARAMETER IgnorePSDebugContext
    If set, ignores whether the session is currently paused at a breakpoint or step.
.PARAMETER IgnoreHostDebuggerEnabled
    If set, ignores whether the host's debugger is enabled (e.g., in VS Code).
.EXAMPLE
    Test-KrDebugContext
    Returns $true if a managed debugger is attached, the -Debug switch is used,
    or the KESTRUN_DEBUG environment variable is set to a truthy value; otherwise, $false.
#>
function Test-KrDebugContext {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [outputType([bool])]
    param (
        [Parameter(Mandatory = $false)]
        [switch]$IgnorePSDebugContext,
        [Parameter(Mandatory = $false)]
        [switch]$IgnoreHostDebuggerEnabled
    )
    return ((-not $IgnorePSDebugContext.IsPresent) -and ($PSDebugContext)) -and
    ((-not $IgnoreHostDebuggerEnabled.IsPresent) -and $Host.DebuggerEnabled)
}
