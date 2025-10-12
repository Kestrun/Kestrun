<#
    .SYNOPSIS
        Retrieves a byte array value from the session by key.
    .DESCRIPTION
        This function accesses the current HTTP context's session and retrieves the byte array value
        associated with the specified key.
    .PARAMETER Key
        The key of the session item to retrieve.
    .EXAMPLE
        $value = Get-KrSessionByte -Key "profileImage"
        Retrieves the byte array value associated with the key "profileImage" from the session.
    .OUTPUTS
        Returns the byte array value associated with the specified key, or $null if not found.
#>
function Get-KrSessionByte {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding()]
    [OutputType([System.byte[]])]
    param(
        [Parameter(Mandatory)]
        [string]$Key
    )
    if ($null -ne $Context.Session) {
        return [Microsoft.AspNetCore.Http.SessionExtensions]::Get($Context.Session, $Key)
    }
}
