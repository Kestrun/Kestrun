<#
    .SYNOPSIS
        Retrieves a string value from the session by key.
    .DESCRIPTION
        This function accesses the current HTTP context's session and retrieves the string value
        associated with the specified key.
    .PARAMETER Key
        The key of the session item to retrieve.
    .EXAMPLE
        $value = Get-KrSessionString -Key "username"
        Retrieves the string value associated with the key "username" from the session.
    .OUTPUTS
        Returns the string value associated with the specified key, or $null if not found.
#>
function Get-KrSessionString {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][string]$Key
    )
    if ($null -ne $Context.Session) {
        return [Microsoft.AspNetCore.Http.SessionExtensions]::GetString($Context.Session, $Key)
    }
}
