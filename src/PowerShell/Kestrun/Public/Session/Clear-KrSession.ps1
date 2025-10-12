<#
    .SYNOPSIS
        Clears all items from the session.
    .DESCRIPTION
        This function accesses the current HTTP context's session and clears all items stored in it.
    .EXAMPLE
        Clear-KrSession
        Clears all items from the current session.
    .OUTPUTS
        None. This function performs a state-changing operation on the session.
#>
function Clear-KrSession {
    [KestrunRuntimeApi('Route')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    param()
    if ($null -ne $Context.Session) {
        $Context.Session.Clear()
    }
}
